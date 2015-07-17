module TheGamma.Server.Config

open System
open System.IO

// This script is implicitly inserted before every source code we get
let loadScript =
  [| "#load \"load.fsx\"\n"
     "open TheGamma\n"
     "open TheGamma.Series\n"
     "open TheGamma.GoogleCharts\n" |]

let loadScriptString =
  String.Concat(loadScript)

// Directory with FunScript binaries and 'Fun3D.fsx'
let gammaFolder = Path.Combine(__SOURCE_DIRECTORY__, "..", "thegamma")
let scriptFile = Path.Combine(__SOURCE_DIRECTORY__, "..", "thegamma", "script.fsx")
let scriptFileModule = "Script"