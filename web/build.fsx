// --------------------------------------------------------------------------------------
// A simple FAKE build script that:
//  1) Hosts Suave server locally & reloads web part that is defined in 'app.fsx'
//  2) Deploys the web application to Azure web sites when called with 'build deploy'
// --------------------------------------------------------------------------------------

#r "packages/FSharp.Compiler.Service/lib/net45/FSharp.Compiler.Service.dll"
#r "packages/Suave/lib/net40/Suave.dll"
#r "packages/FAKE/tools/FakeLib.dll"
#load "paket-files/matthid/Yaaf.FSharp.Scripting/src/source/Yaaf.FSharp.Scripting/YaafFSharpScripting.fs"

open Fake
open Suave
open Suave.Web
open Suave.Types
open Yaaf.FSharp.Scripting

// --------------------------------------------------------------------------------------
// When `app.fsx` changes, we `#load "app.fsx"` using the F# Interactive service
// and then get the `App.app` value (top-level value defined using `let app = ...`).
// --------------------------------------------------------------------------------------

let internal fsiSession = ScriptHost.CreateNew()

let reloadScript () =
  try
    traceImportant "Reloading app.fsx script..."
    let appFsx = __SOURCE_DIRECTORY__ @@ "app.fsx"
    fsiSession.EvalInteraction(sprintf "#load @\"%s\"" appFsx)
    fsiSession.EvalInteraction("open App")
    Some(fsiSession.EvalExpression<WebPart>("app"))
  with :? FsiEvaluationException as e ->
    traceError "Reloading app.fsx script failed."
    traceError (sprintf "Message: %s\nError: %s" e.Message e.Result.Error.Merged)
    None

// --------------------------------------------------------------------------------------
// Suave server that redirects all request to currently loaded WebPart. We watch for
// changes & reload automatically. The WebPart is then hosted at http://localhost:8087
// --------------------------------------------------------------------------------------

let currentApp = ref (fun _ -> async { return None })

let serverConfig =
  { defaultConfig with
      homeFolder = Some __SOURCE_DIRECTORY__
      logger = Logging.Loggers.saneDefaultsFor Logging.LogLevel.Debug
      bindings = [ HttpBinding.mk' HTTP  "127.0.0.1" 8087] }

let reloadAppServer () =
  reloadScript() |> Option.iter (fun app ->
    currentApp.Value <- app
    traceImportant "New version of app.fsx loaded!" )

Target "Run" (fun _ ->
  let app ctx = currentApp.Value ctx
  let _, server = startWebServerAsync serverConfig app

  // Start Suave & open web browser with the site
  reloadAppServer()
  Async.Start(server)
  //System.Diagnostics.Process.Start("http://localhost:8087") |> ignore

  // Watch for changes & reload when app.fsx changes
  let sources = { BaseDirectory = __SOURCE_DIRECTORY__; Includes = [ "**/*.fs*" ]; Excludes = [] }
  use watcher = sources |> WatchChanges (fun _ -> reloadAppServer())
  traceImportant "Waiting for app.fsx edits. Press any key to stop."

  async {
    while not(System.IO.File.Exists(__SOURCE_DIRECTORY__ @@ ".stop")) do
      do! Async.Sleep(500)
    System.Diagnostics.Process.GetCurrentProcess().Kill() }
  |> Async.Start

  System.Console.ReadLine() |> ignore
)

Target "Build" ignore

// --------------------------------------------------------------------------------------
// Minimal Azure deploy script - just overwrite old files with new ones
// --------------------------------------------------------------------------------------

Target "Deploy" (fun _ ->
  let sourceDirectory = __SOURCE_DIRECTORY__
  let wwwrootDirectory = __SOURCE_DIRECTORY__ @@ "../../wwwroot"
  try
    DeleteDir wwwrootDirectory
    CreateDir wwwrootDirectory
  with e ->
    printfn "Could not delete all files in %s" wwwrootDirectory
  try
    CopyRecursive sourceDirectory wwwrootDirectory false |> ignore
  with e ->
    printfn "Copying files failed with: %A" e
    reraise()
)

RunTargetOrDefault "Run"
