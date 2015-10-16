#r "System.Xml.Linq.dll"
#r "packages/FAKE/tools/FakeLib.dll"
#r "packages/FSharp.Data/lib/net40/FSharp.Data.dll"
open Fake
open System
open System.IO
open FSharp.Data

System.Environment.CurrentDirectory <- __SOURCE_DIRECTORY__

let project = "../TheGamma.Data.sln"
let tempBin = "bin"
let outBin = "../web/thegamma/bin"
let stopFile = "../web/.stop"
let fsc = __SOURCE_DIRECTORY__ @@ "packages/FSharp.Compiler.Tools/tools/fsc.exe"

type FsProj = XmlProvider<"TheGamma.World.fsproj">

/// Build the specified type provider (but only when some of the files have changed)
let buildLibrary (name:string) refs =
  let fsproj = FsProj.Load(name)
  let files =
    [ for it in fsproj.ItemGroups do
        for c in it.Compiles do yield c.Include ]
  
  let projRefs =
    [ for it in fsproj.ItemGroups do
        for pr in it.ProjectReferences do yield tempBin @@ pr.Name + ".dll" ]

  let out = tempBin @@ (Path.ChangeExtension(name, "dll"))
  let outWrite = File.GetLastWriteTime(__SOURCE_DIRECTORY__ @@ out)

  let anyFileChanged = 
    not (File.Exists(out)) || 
    ( List.append files projRefs
      |> List.map (fun f -> File.GetLastWriteTime(__SOURCE_DIRECTORY__ @@ f))
      |> List.exists (fun dt -> dt > outWrite) )
  
  if anyFileChanged then
    trace (name + " has been changed. Recompiling...")
    let files = files |> String.concat " "
    let refs = projRefs @ refs |> List.map (sprintf "--reference:%s") |> String.concat " "
    let res = TimeSpan.MaxValue |> ExecProcessAndReturnMessages (fun ps ->
      let args = refs + (sprintf " -g --debug:full --optimize- --out:%s --target:library %s" out files)
      ps.WorkingDirectory <- __SOURCE_DIRECTORY__
      ps.FileName <- fsc
      ps.Arguments <- args)
    res.Messages |> Seq.iter trace
    res.Errors |> Seq.iter traceError
  else
    trace (name + " has not been changed. Skipping...")


let reloadWebApp () =
  File.WriteAllText(stopFile, "stop")
  let rec loop n =
    System.Threading.Thread.Sleep(n * 1000)
    traceImportant (sprintf "Copying compiled binaries... (%d)" n)
    try
      CleanDir(outBin)
      CopyFiles outBin (!! (tempBin @@ "*.*"))
    with _ ->
      if n = 10 then reraise() else loop (n + 1)
  loop 1
  traceImportant "Deleting .stop file to restart the app..."
  File.Delete(stopFile)

let buildProviders () =
  let refs =
    [ yield "packages/FSharp.Data/lib/net40/FSharp.Data.dll"
      yield! Directory.GetFiles("lib") ]

  CopyFiles tempBin (!! ("lib/*.*"))
  CopyFiles tempBin (!! ("packages/FSharp.Data/lib/net40/*"))
  buildLibrary "TheGamma.Data.fsproj" refs
  buildLibrary "TheGamma.Json.fsproj" refs
  buildLibrary "TheGamma.Html.fsproj" refs
  buildLibrary "TheGamma.World.fsproj" refs
  reloadWebApp()

Target "Build" (fun _ ->
  buildProviders()
)

Target "Run" (fun _ ->
  buildProviders()
  let sources = { BaseDirectory = __SOURCE_DIRECTORY__; Includes = [ "**/*.fs*" ]; Excludes = [ "obj/**" ] }
  use watcher = sources |> WatchChanges (fun _ -> buildProviders())
  traceImportant "Running in the background, press any key to stop"
  Console.ReadLine() |> ignore
)

Target "Rebuild" (fun _ ->
  !! project
  |> MSBuildDebug "" "Rebuild"
  |> Log "AppBuild-Output: "
)

Target "Deploy" ignore
"Build" ==> "Deploy"

RunTargetOrDefault "Run"
