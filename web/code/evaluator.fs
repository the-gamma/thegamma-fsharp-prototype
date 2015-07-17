module TheGamma.Server.Evaluator

open Microsoft.FSharp.Compiler.Ast
open Microsoft.FSharp.Compiler.Interactive.Shell
open Microsoft.FSharp.Compiler.SourceCodeServices
open System
open System.IO
open System.Text
open TheGamma.Server.Common

// ------------------------------------------------------------------------------------------------
// FunScript + F# Compiler Service Evaluator
// ------------------------------------------------------------------------------------------------

type FsiSession =
  { Session : FsiEvaluationSession
    ErrorString : StringBuilder }

/// Start F# Interactive, reference all assemblies in `refFolder`
/// evaluate the initial `loadScript` and return running 'FsiSession'
let startSession refFolder loadScript =
  let sbOut = new StringBuilder()
  let sbErr = new StringBuilder()
  let inStream = new StringReader("")
  let outStream = new StringWriter(sbOut)
  let errStream = new StringWriter(sbErr)

  // Start the F# Interactive service process
  let refFiles = Directory.GetFiles(refFolder, "*.dll")
  let fsiConfig = FsiEvaluationSession.GetDefaultConfiguration()
  let fsiSession =
    FsiEvaluationSession.Create
      ( fsiConfig, [| "/temp/fsi.exe"; "--noninteractive" |],
        inStream, outStream, errStream, collectible = true )

  // Load referenced libraries & run initialization script
  try
    fsiSession.EvalInteraction(sprintf "#I @\"%s\"" refFolder)
    for lib in refFiles do fsiSession.EvalInteraction(sprintf "#r @\"%s\"" lib)
    fsiSession.EvalInteraction(loadScript)
    { Session = fsiSession; ErrorString = sbErr }
  with _ -> failwithf "F# Interactive initialization failed: %s" (sbErr.ToString())


/// Check that the user didn't do anything to escape quoted expression
/// (i.e. they are not trying to run any code on our server..)
let checkScriptStructure (scriptFile, source) (checker:FSharpChecker) = async {
  let! options = checker.GetProjectOptionsFromScript(scriptFile, source)
  let! parsed = checker.ParseFileInProject(scriptFile, source, options)
  match parsed.ParseTree with
  | Some tree ->
      match tree with
      // Expecting: single file containing single module named "Script"
      | ParsedInput.ImplFile
          (ParsedImplFileInput(_,_,_,_,_,[SynModuleOrNamespace([id],_,decls,_,_,_,_)],_))
            when id.idText = "Script" ->
        match decls with
        // Expecting: FunScript.Compiler.Compiler.Compile(<@ .. @>)
        // (if all user code is inside quotation, it does not get run)
        | [ SynModuleDecl.DoExpr
              (_, SynExpr.App
                    ( _, _, SynExpr.LongIdent _,
                      SynExpr.Paren(SynExpr.Quote _, _, _, _), _), _) ] -> ()
        | _ -> failwith "Unexpected AST!"
      | _ -> failwith "Unexpected AST!"
  | _ -> failwith "Could not parse the specified AST" }

/// Pass the specified code to FunScript and return JavaScript that we'll
/// send back to the client (so that they can run it themselves)
let evalFunScript code { Session = fsiSession; ErrorString = sbErr } = async {
  let allCode =
    [ yield "FunScript.Compiler.Compiler.Compile(<@"
      for line in getLines code do yield "  " + line
      yield "@>)" ]
    |> String.concat "\n"
  printfn "Evaluating: %s" allCode
  do! checkScriptStructure (Config.scriptFile, allCode) fsiSession.InteractiveChecker

  try
    match fsiSession.EvalExpression(allCode) with
    | Some value -> return Choice1Of2(value.ReflectionValue.ToString())
    | None -> return Choice2Of2(new Exception("Evaluating expression produced no output."))
  with e ->
    let errors = sbErr.ToString()
    return Choice2Of2(new Exception("Evaluation failed: " + errors, e)) }

// ------------------------------------------------------------------------------------------------
// Start F# interactive and expose a web part
// ------------------------------------------------------------------------------------------------

open Suave.Http
open Suave.Http.Applicatives

let evaluate (fsi:ResourceAgent<FsiSession>) code = 
  fsi.Process(evalFunScript code)
 
let webPart fsi =
  path "/run" >>= withRequestParams (fun (_, _, source) ctx -> async { 
      // Transform F# `source` into JavaScript and return it
      let! jscode = evaluate fsi source
      match jscode with
      | Choice1Of2 jscode -> return! ctx |> noCacheSuccess jscode
      | Choice2Of2 e -> 
          printfn "Evalutaiton failed: %s" (e.ToString())
          return! ctx |> RequestErrors.BAD_REQUEST "evaluation failed" })
