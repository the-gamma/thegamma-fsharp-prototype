#r "packages/Suave/lib/net40/Suave.dll"
open Suave
open Suave.Http

(*
#r "thegamma/FunScript.dll"
#r "thegamma/FunScript.Interop.dll"
#r "thegamma/FunScript.TypeScript.Binding.jquery.dll"
#r "thegamma/FunScript.TypeScript.Binding.lib.dll"
#r "thegamma/FunScript.TypeScript.Binding.google_visualization.dll"

#r "thegamma/TheGamma.Data.dll"
#r "thegamma/TheGamma.Json.dll"
#r "thegamma/TheGamma.World.dll"

open TheGamma

open FunScript
open FunScript.TypeScript
open FunScript.TypeScript.google
open FunScript.TypeScript.google.visualization

type packages = { packages:string[] }

#load "client/series.fs"
#load "client/google/core.fs"
#load "client/google/options.fs"
#load "client/google/extensions.fs"
#load "client/google/charts.fs"

open TheGamma
open TheGamma.Series
open TheGamma.GoogleCharts

[<JS>]
module JS = 
  let foo() =
    google.Globals.load("visualization", "1.0", {packages=[| "corechart" |]})
    google.Globals.setOnLoadCallback(fun () -> 
      let x = world.byYear.``2010``.``Economy & Growth``.``GDP per capita (current US$)``.map(log10)
      let y = world.byYear.``2010``.Health.``Life expectancy at birth, total (years)``
      let ch = 
        chart.scatter(x, y)
          .set(width=1000.0, height=1000.0)
          .set(title="GDP vs Life expectancy.,,?", pointSize=3.0, colors=["#3B8FCC"])
          .hAxis(title="Log of " + x.seriesName)
          .vAxis(title=y.seriesName)
          .trendlines([options.trendline(opacity=0.5, lineWidth=10.0, color="#C0D9EA")])
      
      let co2 = world.byYear.``2010``.``Climate Change``.``CO2 emissions (kt)``
      let pop = world.byYear.``2010``.``Climate Change``.``Total Population (in number of people)``

      let ch1 = 
        chart.geo(co2)
          .colorAxis(colors=["#6CC627";"#DB9B3B";"#DB7532";"#DD5321";"#DB321C";"#E00B00"])

      let co2pcp = co2.joinInner(pop).map(fun (co2, pop) -> co2 / pop)
      let ch2 = 
        chart.geo(co2pcp)
          .colorAxis(colors=["#6CC627";"#DB9B3B";"#DB7532";"#DD5321";"#DB321C";"#E00B00"])

      let tops = world.byYear.``2010``.``Climate Change``.``CO2 emissions (kt)``.sortValues(reverse=true)
      let tall = tops.take(6).append("Rest", tops.skip(10).sum())

      let ch3 = chart.pie(tall).set(width=1000.0,height=1000.0)
      

      let polluters = 
        [ world.byCountry.China
          world.byCountry.India
          world.byCountry.``United States``
          world.byCountry.``Russian Federation``
          world.byCountry.Germany
          world.byCountry.``United Kingdom``
          world.byCountry.Canada
          world.byCountry.Brazil 
        ].series().mapTask(fun p -> p.``Climate Change``.``Population growth (annual %)``.first()).sortValues(reverse=true).set(seriesName="Population growth (annual %)")

      let ch4 = chart.column(polluters).set(width=1000.0, height=1000.0).set(colors=["#DB9B3B"])
      chart.show(ch4)
    )
*)
//#r "packages/FSharp.Data/lib/net40/FSharp.Data.dll"
#if INTERACTIVE
#r "thegamma/bin/FSharp.Data.dll"
#r "packages/FSharp.Compiler.Service/lib/net40/FSharp.Compiler.Service.dll"
#load "packages/FSharp.Formatting/FSharp.Formatting.fsx"
#load "code/config.fs"
#load "code/common.fs"
#load "code/evaluator.fs"
#load "code/document.fs"
#load "code/editor.fs"
#load "code/visualizers.fs"
#endif
open System.IO
open Suave.Types
open Suave.Http.Applicatives
open TheGamma.Server


let staticWebFile ctx = async {
  let local = ctx.request.url.LocalPath
  let file = if local = "/" then "index.html" else local.Substring(1)
  let actualFile = Path.Combine(ctx.runtime.homeDirectory, "web", file)
  if not(File.Exists(actualFile)) then return None else
  let mime = Suave.Http.Writers.defaultMimeTypesMap(Path.GetExtension(actualFile))
  let setMime = 
    match mime with 
    | None -> fun c -> async { return None }
    | Some mime -> Suave.Http.Writers.setMimeType mime.name
  return! ctx |> ( setMime >>= Successful.ok(File.ReadAllBytes actualFile)) }
(*
  let webDir = Path.Combine(ctx.runtime.homeDirectory, "web")
  let subRuntime = { ctx.runtime with homeDirectory = webDir }
  let webPart =
    if ctx.request.url.LocalPath <> "/" then Files.browseHome
    else Files.browseFileHome "index.html"
  return! webPart { ctx with runtime = subRuntime } }
*)

(*
let html = System.IO.File.ReadAllText(__SOURCE_DIRECTORY__ + "/web/demo.html")

let demo ctx = async {
  try
    let js = FunScript.Compiler.Compiler.Compile(<@ JS.foo() @>)
    return! ctx |> Successful.OK(html.Replace("[SCRIPT]", js))
  with e ->
    return! ctx |> Successful.OK(e.ToString()) }
*)

open System
open TheGamma.Server.Common
open Microsoft.FSharp.Compiler.SourceCodeServices

let checker = 
  ResourceAgent("FSharpChecker", Int32.MaxValue, fun () -> FSharpChecker.Create())
let fsi =
  ResourceAgent("FsiSession", 50,
    (fun () -> Evaluator.startSession Config.gammaFolder Config.loadScriptString),
    (fun fsi -> (fsi.Session :> IDisposable).Dispose()) )

let app = 
  choose
    [ Editor.webPart checker
      Document.webPart fsi
      Evaluator.webPart fsi
      Visualizers.webPart checker
      //path "/demo" >>= demo 
      path "/"
      staticWebFile ]

// -------------------------------------------------------------------------------------------------
// To run the web site, you can use `build.sh` or `build.cmd` script, which is nice because it
// automatically reloads the script when it changes. But for debugging, you can also use run or
// run with debugger in VS or XS. This runs the code below.
// -------------------------------------------------------------------------------------------------
(*
#if INTERACTIVE
#else
open Suave.Web
let cfg =
  { defaultConfig with
      bindings = [ HttpBinding.mk' HTTP  "127.0.0.1" 8011 ]
      homeFolder = Some __SOURCE_DIRECTORY__ }
let _, server = startWebServerAsync cfg app
Async.Start(server)
System.Diagnostics.Process.Start("http://localhost:8011")
System.Console.ReadLine() |> ignore
#endif
*)