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
  let rec tokenizeLine (lineStr:string) line acc (tokenizer:FSharpLineTokenizer) state = 
    match tokenizer.ScanToken(state) with
    | Some tok, state ->
        let acc = (tok.TokenName, lineStr.[tok.LeftColumn .. tok.RightColumn], (line, tok.LeftColumn), (line, tok.RightColumn))::acc
        tokenizeLine lineStr line acc tokenizer state
    | None, state -> state, acc
  
  let rec tokenizeLines state count acc lines = 
    match lines with
    | line::lines ->
        let tokenizer = sourceTok.CreateLineTokenizer(line)
        let state, acc = tokenizeLine line count acc tokenizer state
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
  |> List.filter (function "WHITESPACE", _, _, _ -> false | _ -> true)
  |> findMatches (function
      | ("IDENT", _, ps, pe)::("DOT", _, _, _)::("IDENT", _, ms, me)::_ -> 
          Some((ps, pe), (ms, me)) | _ -> None)
  |> List.ofSeq

type GetChainState = Waiting | Ident of (string list)*(int*int)*(int*int) | Dot of (string list)*(int*int)*(int*int)

/// Find all bits that look like a definition of a list 
/// (this really should use the TAST, but the below works good enough for a demo)
let chainsInAList minIdents sourceInfo =

  let rec findRBrack acc input = 
    match input with
    | ("RBRACK", _, _, _)::rest -> Some(List.rev acc, rest)
    | x::rest -> findRBrack (x::acc) rest
    | [] -> None
  let (|UntilRBrack|_|) s = findRBrack [] s

  let rec getChains acc state body =
    match state, body with 
    | Waiting, ("IDENT", s, is, ie)::body -> getChains acc (Ident([s], is, ie)) body
    | Waiting, _::body -> getChains acc Waiting body
    | Ident(p, is,ie), ("DOT",_, _, _)::body -> getChains acc (Dot(p, is, ie)) body
    | Ident(p, is,ie), ("IDENT", s, ns, ne)::body -> getChains ((p, is, ie)::acc) (Ident([s], ns, ne)) body
    | Ident(p, is,ie), _::body -> getChains acc (Ident(p, is, ie)) body
    | Dot(p, is,ie), ("IDENT", s, ns, ne)::body -> getChains acc (Ident(s::p, is, ne)) body
    | Dot(p, is,ie), _::body -> getChains acc (Dot(p, is, ie)) body
    | Ident(p, is, ie), [] -> (p, is, ie)::acc |> List.rev
    | _ -> acc |> List.rev

  getTokensWithLocations sourceInfo
  |> findMatches (function
      | ("LBRACK", _, _, _)::(UntilRBrack(body, _)) -> Some(body)
      | _ -> None)
  |> Seq.map (getChains [] Waiting)
  |> Seq.filter (fun l -> Seq.length l > minIdents)


/// Type check and extract uses from a document
let checkAndGetUses sourceInfo checker = async {
  let! parse, check = Editor.checkFile sourceInfo checker

  // The code is implicitly in a module based on the file name
  let scriptEntity = 
    check.PartialAssemblySignature.Entities 
    |> Seq.find (fun e -> e.DisplayName = Config.scriptFileModule)

  // Get uses of all symbols in the file 
  let! uses = check.GetAllUsesOfAllSymbolsInFile()
  return [ for u in uses -> (u.RangeAlternate.EndLine, u.RangeAlternate.EndColumn), u ] |> dict }


// COnfiguration
let minOptionCount = 5
let minListCount = 3


/// Drop down for <parent>.<member> kind of experssions
type SingleLevelDropdown = 
  { Range : (int * int) * (int * int)
    Initial : string
    Options : list<string * string> }

let tokToSymLoc (l,c) = (l, c+1)

let (|Symbol|) (us:FSharpSymbolUse) = us.Symbol

let getSingleLevelVisualizers sourceInfo (checker:FSharpChecker) = async {
  let! usesByRangeEnd = checkAndGetUses sourceInfo checker
  let editors = 
    // Find all "<foo>.<bar>" where both <foo> and <bar> are members/functions
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

/// Select element for a list of [ <foo>.<bar1>; <foo>.<bar2>; ... ] things
type ListSelect =
  { Range : (int*int) * (int*int) // from the start of the first ident to the end of the last ident
    Initial : list<string>
    Prefix : list<string>
    Options : list<string * string> }

let getListVisualizers sourceInfo (checker:FSharpChecker) = async {
  let! usesByRangeEnd = checkAndGetUses sourceInfo checker
  let editors = 
    [ for idents in chainsInAList minListCount sourceInfo do
        let prefix = idents |> Seq.map (fun (l, _, _) -> List.tail l)
        if (Seq.length (Seq.distinct prefix) = 1) then
          let prefix = Seq.distinct prefix |> Seq.head |> List.rev

          // Get info about the members that are accessed
          let members =
            [ for _, _, endl in idents do
                match usesByRangeEnd.TryGetValue(tokToSymLoc(endl)) with
                | true, nestUs & (Symbol(:? FSharpMemberOrFunctionOrValue as nestVal)) ->
                    yield nestVal.DisplayName, nestVal
                | _ -> () ]
          if members.Length > 0 && (members |> Seq.forall (fun (_, t) -> 
                t.FullType = (snd members.Head).FullType)) &&
              members.Length = idents.Length then
            let startl = idents |> Seq.map (fun (_,f,_) -> f) |> Seq.min
            let endl = idents |> Seq.map (fun (_,_,s) -> s) |> Seq.max
            let opts = 
                [ for child in (snd members.Head).EnclosingEntity.MembersFunctionsAndValues do
                    if child.FullType = (snd members.Head).FullType then
                      yield child.DisplayName, String.concat "\n" child.XmlDoc ]
            yield { Prefix = prefix; Range = startl, endl; Initial = [ for m, _ in members -> m ]; Options = opts } ]  
  return editors }

// ------------------------------------------------------------------------------------------------
//
// ------------------------------------------------------------------------------------------------

open Suave.Http
open Suave.Http.Applicatives

type JsonTypes = JsonProvider<"""{
    "visualizers":
      { "hash":123, 
        "list":
          [ {"initial":["Ident1","Ident2"], "prefix":["foo","bar"], "range":[1,2, 3,4], "options":
              [ {"member": "Member", "documentation": "Text"} ] } ],
        "singleLevel":
          [ {"initial":"Ident", "range":[1,2, 3,4], "options":
              [ {"member": "Member", "documentation": "Text"} ] } ] 
      } }""">

let webPart (checker:ResourceAgent<_>) =
  path "/visualizers" >>= 
  Writers.setHeader "Access-Control-Allow-Origin" "*" >>= 
  withRequestParams (fun (_, _, source) ctx -> async { 
    let! sl = checker.Process (getSingleLevelVisualizers (Config.scriptFile, Config.loadScriptString + source))
    let! ls = checker.Process (getListVisualizers (Config.scriptFile, Config.loadScriptString + source))
    let lsJson = 
      [| for v in ls ->
          let (l1,c1), (l2,c2) = v.Range 
          let range = [| l1-Config.loadScript.Length; c1; l2-Config.loadScript.Length; c2 |]
          let options = [| for id, doc in v.Options -> JsonTypes.Option(id, doc) |]
          JsonTypes.List(Array.ofSeq v.Initial, Array.ofSeq v.Prefix, range, options) |]
    let slJson = 
      [| for v in sl do
          let (l1,c1), (l2,c2) = v.Range 
          // Skip single dropdown visualizers that are inside area covered by multiline
          if not (ls |> Seq.exists (fun lsvis -> 
              (fst lsvis.Range) <= (fst v.Range) && (snd lsvis.Range) >= (snd v.Range))) then
            let range = [| l1-Config.loadScript.Length; c1; l2-Config.loadScript.Length; c2 |]
            let options = [| for id, doc in v.Options -> JsonTypes.Option(id, doc) |]
            yield JsonTypes.SingleLevel(v.Initial, range, options) |]

    let res = JsonTypes.Visualizers(hash (ls,sl), lsJson, slJson)
    //printfn "Returning:\n %s" (res.JsonValue.ToString())
    return! ctx |> noCacheSuccess (res.JsonValue.ToString()) })