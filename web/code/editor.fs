module TheGamma.Server.Editor

open Microsoft.FSharp.Compiler.SourceCodeServices
open Microsoft.FSharp.Compiler.Interactive.Shell
open Microsoft.FSharp.Compiler.Ast
open System.Text
open System.IO

open TheGamma.Server.Common

// ------------------------------------------------------------------------------------------------
// F# compiler service wrapper
// ------------------------------------------------------------------------------------------------

/// Extracts all consecutive identifiers to the left of the charIndex for a specified line of code
let extractIdentTokens line charIndex =
    let sourceTok = SourceTokenizer([], "/home/test.fsx")
    let tokenizer = sourceTok.CreateLineTokenizer(line)

    let rec gatherTokens (tokenizer:FSharpLineTokenizer) state = seq {
      match tokenizer.ScanToken(state) with
      | Some tok, state ->
          yield tok
          yield! gatherTokens tokenizer state
      | None, state -> () }

    let tokens = gatherTokens tokenizer 0L |> Seq.toArray
    let idx = tokens |> Array.tryFindIndex(fun x ->
      charIndex > x.LeftColumn && charIndex <= x.LeftColumn + x.FullMatchedLength)

    match idx with
    | Some(endIndex) ->
        let startIndex =
            tokens.[0..endIndex]
            |> Array.rev
            |> Array.tryFindIndex (fun x -> x.TokenName <> "IDENT" && x.TokenName <> "DOT")
            |> Option.map (fun x -> endIndex - x)
        let startIndex = defaultArg startIndex 0
        let idents = tokens.[startIndex..endIndex] |> Array.filter (fun x -> x.TokenName = "IDENT")
        Some tokens.[endIndex], idents

    | None -> None, Array.empty

/// Parses the line of F# code and builds a list of identifier names in order
/// to be passed into the `GetDeclarations`, `GetMethods`, or other functions
///
/// For tooltips and overlodas, set identOffset=0; For completion set identOffset=1
let extractNames line charIndex identOffset =
    let charToken, tokens = extractIdentTokens line charIndex
    match charToken with
    | None -> 0, 0, []
    | Some(charToken) ->
        let names = tokens |> Array.map (fun x ->
          line.Substring(x.LeftColumn, x.FullMatchedLength).Trim('`'))
        let takeSize = tokens.Length - identOffset
        let finalList =
          if charToken.TokenName = "IDENT" && Array.length(tokens) > takeSize then
            names |> Seq.take takeSize |> Seq.toList
          else
            names |> Seq.toList
        (charToken.LeftColumn, charToken.LeftColumn + charToken.FullMatchedLength, finalList)

// Mostly boring code to format tooltips reported from method overloads
let htmlEncode (s:string) = s.Trim().Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")
let formatComment cmt (sb:StringBuilder) =
    match cmt with
    | FSharpXmlDoc.Text(s) -> sb.AppendLine(s.Trim()) |> ignore
    | FSharpXmlDoc.XmlDocFileSignature(file, signature) -> ()
    | FSharpXmlDoc.None -> ()
let formatTipElement isSingle el (sbSig:StringBuilder) (sbText:StringBuilder) =
    match el with
    | FSharpToolTipElement.None -> ()
    | FSharpToolTipElement.Single(it, comment) ->
        sbSig.AppendLine(htmlEncode it) |> ignore
        formatComment comment sbText
    | FSharpToolTipElement.Group(items) ->
        let items, msg =
          if items.Length > 10 then
            (items |> Seq.take 10 |> List.ofSeq),
            sprintf "   (+%d other overloads)" (items.Length - 10)
          else items, ""
        if isSingle && items.Length > 1 then
          sbSig.AppendLine("Multiple overloads") |> ignore
        for (it, comment) in items do
          sbSig.AppendLine(it) |> ignore
          formatComment comment sbText
        if msg <> null then sbSig.AppendFormat(msg) |> ignore
    | FSharpToolTipElement.CompositionError(err) ->
        sbText.Append("Composition error: " + err) |> ignore
let formatTip tip =
  let sbSig = StringBuilder()
  let sbText = StringBuilder()
  match tip with
  | FSharpToolTipText([single]) -> formatTipElement true single sbSig sbText
  | FSharpToolTipText(its) -> for item in its do formatTipElement false item sbSig sbText
  sbSig.ToString().Trim('\n', '\r'),
  sbText.ToString().Trim('\n', '\r')

/// Check specified file and return parsing & type checking results
let checkFile (fileName, source) (checker:FSharpChecker) = async {
    let! options = checker.GetProjectOptionsFromScript(fileName, source)
    match checker.TryGetRecentTypeCheckResultsForFile(fileName, options, source) with
    | Some(parse, check, _) -> return parse, check
    | None ->
        let! parse = checker.ParseFileInProject(fileName, source, options)
        let! answer = checker.CheckFileInProject(parse, fileName, 0, source, options)
        match answer with
        | FSharpCheckFileAnswer.Succeeded(check) -> return parse, check
        | FSharpCheckFileAnswer.Aborted -> return failwith "Parsing did not finish" }

/// Get declarations (completion) at the specified line & column (lines are 1-based)
let getDeclarations (fileName, source) (line, col) (checker:FSharpChecker) = async {
    let! parse, check = checkFile (fileName, source) checker
    let textLine = getLines(source).[line-1]
    let _, _, names = extractNames textLine col 1
    printfn "Names: %A" names
    let! decls = check.GetDeclarationListInfo(Some parse, line, col, textLine, names, "")
    return [ for it in decls.Items -> it.Name, it.Glyph, formatTip it.DescriptionText ] }

/// Get method overloads (for the method before '('). Lines are 1-based
let getMethodOverloads (fileName, source) (line, col) (checker:FSharpChecker) = async {
    let! parse, check = checkFile (fileName, source) checker
    let textLine = getLines(source).[line-1]
    match extractNames textLine col 0 with
    | _, _, [] -> return List.empty
    | _, _, names ->
        let! methods = check.GetMethodsAlternate(line, col, textLine, Some names)
        return [ for m in methods.Methods -> formatTip m.Description ] }

// ------------------------------------------------------------------------------------------------
// Suave.io web server
// ------------------------------------------------------------------------------------------------

open System
open Suave
open Suave.Web
open Suave.Http
open Suave.Types
open FSharp.Data

/// Types of JSON values that we are returning from F# Compiler Service calls
type JsonTypes = JsonProvider<"""{
    "declarations":
      {"declarations":[ {"name":"Method", "glyph":1, "signature":"Text", "documentation":"Text"} ]},
    "errors":
      {"errors":[ {"startLine":1, "startColumn":1, "endLine":1, "endColumn":1, "message":"error"} ]},
    "methods":
      {"methods":[ "first info", "second info" ] }
  }""">

// This script is implicitly inserted before every source code we get
let loadScript =
  [| "#load \"load.fsx\"\n"
     "open TheGamma\n"
     "open TheGamma.Series\n"
     "open TheGamma.GoogleCharts\n" |]

let loadScriptString =
  String.Concat(loadScript)

/// The main handler for Suave server!
let webPart (checker:ResourceAgent<FSharpChecker>) =
  Writers.setHeader "Access-Control-Allow-Origin" "*" >>= 
  Writers.setHeader "Access-Control-Allow-Headers" "Accept, Content-Type" >>= 
  Writers.setHeader "Access-Control-Allow-Method" "POST, GET" >>= fun ctx -> async {
  match ctx.request.url.LocalPath, getRequestParams ctx with

  // Type-check the source code & return list with error information
  | "/check", (_, _, source) ->
      let! _, check =
        checkFile (Config.scriptFile, loadScriptString + source)
        |> checker.Process
      let res =
        [| for err in check.Errors ->
            JsonTypes.Error
              ( err.StartLineAlternate-1-loadScript.Length, err.StartColumn,
                err.EndLineAlternate-1-loadScript.Length, err.EndColumn, err.Message ) |]
      return! ctx |> noCacheSuccess (JsonTypes.Errors(res).JsonValue.ToString())

  // Get method overloads & parameter info at the specified location in the source
  | "/methods", (Some line, Some col, source) ->
      printfn "Get method overloads: %d,%d" line col
      let! meths =
        getMethodOverloads (Config.scriptFile, loadScriptString + source)
                           (line + loadScript.Length, col)
        |> checker.Process
      let res = JsonTypes.Methods(Array.ofSeq (Seq.map (fun (s1, s2) -> s1 + s2) meths))
      return! ctx |> noCacheSuccess (res.JsonValue.ToString())

  // Get auto-completion for the specified location
  | "/declarations", (Some line, Some col, source) ->
      printfn "Get declarations: %d,%d" line col
      let! decls =
        getDeclarations (Config.scriptFile, loadScriptString + source)
                        (line + loadScript.Length, col)
        |> checker.Process
      decls |> Seq.iter (fun (n,g,_) -> printfn "  - %s (%d)" n g)
      let res = 
        [| for name, glyph, (sg, info) in decls do
             if not (info.Contains("[OMIT]")) then 
                yield JsonTypes.Declaration(name, glyph, sg, info) |]
      return! ctx |> noCacheSuccess (JsonTypes.Declarations(res).JsonValue.ToString())

  | _ -> return None }
