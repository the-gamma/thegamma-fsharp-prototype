module TheGamma.Server.Visualizers

open System.IO
open System.Text
open Microsoft.FSharp.Compiler.Ast
open Microsoft.FSharp.Compiler.SourceCodeServices
open Microsoft.FSharp.Compiler.Interactive.Shell

open FSharp.Data
open TheGamma.Server.Common

// ------------------------------------------------------------------------------------------------
// Extracting information for visualizers/editors from source code
// ------------------------------------------------------------------------------------------------


/// Returns a sequence of tokens in the source file, together with the 
/// location right after their end. Lines are 1-based, columns 0-based
let getTokensWithLocations (fileName, source) =
  let sourceTok = FSharpSourceTokenizer([], fileName)
  let rec tokenizeLine line acc (tokenizer:FSharpLineTokenizer) state = 
    match tokenizer.ScanToken(state) with
    | Some tok, state ->
        let acc = (tok.TokenName, (line, tok.LeftColumn), (line, tok.RightColumn))::acc
        tokenizeLine line acc tokenizer state
    | None, state -> state, acc
  
  let rec tokenizeLines state count acc lines = 
    match lines with
    | line::lines ->
        let tokenizer = sourceTok.CreateLineTokenizer(line)
        let state, acc = tokenizeLine count acc tokenizer state
        tokenizeLines state (count+1) acc lines
    | [] -> List.rev acc

  Common.getLines source
  |> List.ofSeq
  |> tokenizeLines 0L 1 []


/// Find matches based on all suffixes of the token stream
/// (for example, if we want to find IDENT DOT IDENT sequence)
let rec findMatches pattern tokens = seq {
  match pattern tokens with
  | Some res -> yield res 
  | None -> ()
  match tokens with
  | [] -> ()
  | _::tokens -> yield! findMatches pattern tokens }


/// Find all occurrences of IDENT DOT IDENT (ignoring whitespace)
/// and return the range of the first and second identifiers
let identDotIdent sourceInfo = 
  getTokensWithLocations sourceInfo
  |> List.filter (function "WHITESPACE", _, _ -> false | _ -> true)
  |> findMatches (function
      | ("IDENT", ps, pe)::("DOT", _, _)::("IDENT", ms, me)::_ -> 
          Some((ps, pe), (ms, me)) | _ -> None)
  |> List.ofSeq


type SingleLevelDropdown = 
  { Range : (int * int) * (int * int)
    Initial : string
    Options : list<string * string> }

let tokToSymLoc (l,c) = (l, c+1)

let (|Symbol|) (us:FSharpSymbolUse) = us.Symbol

let minOptionCount = 5

let getSingleLevelVisualizers sourceInfo (checker:FSharpChecker) = async {
  let! parse, check = Editor.checkFile sourceInfo checker

  // The code is implicitly in a module based on the file name
  let scriptEntity = 
    check.PartialAssemblySignature.Entities 
    |> Seq.find (fun e -> e.DisplayName = Config.scriptFileModule)

  // Get uses of all symbols in the file 
  let! uses = check.GetAllUsesOfAllSymbolsInFile()
  let usesByRangeEnd = 
    [ for u in uses -> (u.RangeAlternate.EndLine, u.RangeAlternate.EndColumn), u ]
    |> dict

  // Find all "<foo>.<bar>" where both <foo> and <bar> are members/functions
  let editors = 
    [ for mem, nest in identDotIdent sourceInfo do
        match usesByRangeEnd.TryGetValue(tokToSymLoc(snd mem)), 
              usesByRangeEnd.TryGetValue(tokToSymLoc(snd nest)) with
        | (true, memUs & (Symbol(:? FSharpMemberOrFunctionOrValue as memVal))), 
          (true, nestUs & (Symbol(:? FSharpMemberOrFunctionOrValue as nestVal))) ->
            let opts = 
              [ for child in memVal.FullType.TypeDefinition.MembersFunctionsAndValues do
                  if child.FullType = nestVal.FullType then
                    yield child.DisplayName, String.concat "\n" child.XmlDoc ]
            if opts.Length >= minOptionCount then
              yield { Range = nest; Initial = nestVal.DisplayName; Options = opts }
        | _ -> () ]
  return editors }

// ------------------------------------------------------------------------------------------------
//
// ------------------------------------------------------------------------------------------------

open Suave.Http
open Suave.Http.Applicatives

type JsonTypes = JsonProvider<"""{
    "visualizers":
      {"hash":123, "visualizers":[ {"initial":"Ident", "range":[1,2, 3,4], "options":
          [ {"member": "Member", "documentation": "Text"} ] } ] }
  }""">

let webPart (checker:ResourceAgent<_>) =
  path "/visualizers" >>= withRequestParams (fun (_, _, source) ctx -> async { 
    printfn "Get visualizers for %s" source
    let! vis = checker.Process (getSingleLevelVisualizers (Config.scriptFile, Config.loadScriptString + source))
    vis |> Seq.iter (fun v -> printfn "%A: %s" v.Range v.Initial)
    let vis = 
      [| for v in vis -> 
          let (l1,c1), (l2,c2) = v.Range 
          let range = [| l1-Config.loadScript.Length; c1; l2-Config.loadScript.Length; c2 |]
          let options = [| for id, doc in v.Options -> JsonTypes.Option(id, doc) |]
          JsonTypes.Visualizer(v.Initial, range, options) |]
    let res = JsonTypes.Visualizers(hash vis, vis)
    printfn "Returning:\n %s" (res.JsonValue.ToString())
    return! ctx |> noCacheSuccess (res.JsonValue.ToString()) })