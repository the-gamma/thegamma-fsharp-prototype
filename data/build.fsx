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
let fsc = @"C:\Program Files (x86)\Microsoft SDKs\F#\3.1\Framework\v4.0\fsc.exe"

type FsProj = XmlProvider<"TheGamma.World.fsproj">

let buildLibrary (name:string) refs = 
  let fsproj = FsProj.Load(name)
  let files = 
    [ for it in fsproj.ItemGroups do 
        for c in it.Compiles do yield c.Include ]
  let projRefs = 
    [ for it in fsproj.ItemGroups do
        for pr in it.ProjectReferences do yield tempBin @@ pr.Name + ".dll" ]

  let files = files |> String.concat " "
  let refs = projRefs @ refs |> List.map (sprintf "--reference:%s") |> String.concat " "
  let out = tempBin @@ (Path.ChangeExtension(name, "dll"))
  let res = TimeSpan.MaxValue |> ExecProcessAndReturnMessages (fun ps -> 
    let args = refs + (sprintf " -g --debug:full --optimize- --out:%s --target:library %s" out files)
    ps.WorkingDirectory <- __SOURCE_DIRECTORY__
    ps.FileName <- fsc
    ps.Arguments <- args)
  res.Messages |> Seq.iter trace
  res.Errors |> Seq.iter traceError

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
  buildLibrary "TheGamma.World.fsproj" refs
  reloadWebApp()

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

RunTargetOrDefault "Run"
