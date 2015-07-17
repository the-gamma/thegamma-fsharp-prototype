// --------------------------------------------------------------------------------------
// Start the 'app' WebPart defined in 'app.fsx' on Azure using %HTTP_PLATFORM_PORT%
// --------------------------------------------------------------------------------------
printfn "[Azure] starting"

#r "packages/FAKE/tools/FakeLib.dll"
#load "app.fsx"
open App
open Fake
open System
open Suave

printfn "[Azure] loaded"

let serverConfig =
  let port = int (getBuildParam "port")
  { Web.defaultConfig with
      homeFolder = Some __SOURCE_DIRECTORY__
      logger = Logging.Loggers.saneDefaultsFor Logging.LogLevel.Verbose
      bindings = [ Types.HttpBinding.mk' Types.HTTP "127.0.0.1" port ] }

printfn "[Azure] config created"

try
  Web.startWebServer serverConfig app
with e ->
  printfn "[Failed] %A" e
  reraise()
