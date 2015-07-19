// --------------------------------------------------------------------------------------------------------------------
// Google chart API
// --------------------------------------------------------------------------------------------------------------------
namespace TheGamma.GoogleCharts

open TheGamma.Series
open FunScript.TypeScript

type ChartData =
  { data : Async<google.visualization.DataTable> }

type Chart = interface end

[<ReflectedDefinition>]
module Helpers =
  [<FunScript.JSEmitInline("undefined")>]
  let undefined<'T>() : 'T = failwith "!"

  [<FunScript.JSEmitInline("{0}==null")>]
  let isNull(o:obj) : bool = failwith "never"

  [<FunScript.JSEmitInline("{0}[{1}]")>]
  let getProperty<'T> (obj:obj) (name:string) : 'T = failwith "never"

  let copy o prop =
    if isNull o then undefined<_>() else getProperty o prop

  let orDefault newValue =
    match newValue with
    | Some a -> a
    | _ -> undefined<_>()

  let right o prop newValue =
    match newValue with
    | Some a -> a
    | _ when isNull o -> undefined<_>()
    | _ -> getProperty o prop

  [<FunScript.JSEmit("drawChart({0}, {1}, outputElementID, blockCallback);")>]
  let drawChart<'T> (chart:obj) data : unit = failwith "!"

  let showChart(chart:#Chart) =
    async {
      try
        let! dt = (getProperty<ChartData> chart "data").data
        drawChart chart dt
      with e ->
        Globals.window.alert("SOmething went wrong: " + unbox e) }
      |> Async.StartImmediate


[<ReflectedDefinition; CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module ChartData =
  let oneKeyValue keyType (v:series<'k, float>) = { data = async {
    let data = google.visualization.DataTable.Create()
    data.addColumn(keyType, v.keyName) |> ignore
    data.addColumn("number", v.seriesName) |> ignore
    let! vals = v.mapPairs(fun k v -> [| box k; box v |]).data
    vals |> Array.map snd |> data.addRows |> ignore
    return data } }

  let oneKeyTwoValues keyType (v:series<'k, float * float>) = { data = async {
    let data = google.visualization.DataTable.Create()
    data.addColumn(keyType, v.keyName) |> ignore
    data.addColumn("number", v.seriesName) |> ignore
    data.addColumn("number", v.seriesName) |> ignore
    let! vals = v.mapPairs(fun k (v1, v2) -> [| box k; box v1; box v2 |]).data
    vals |> Array.map snd |> data.addRows |> ignore
    return data } }

  let oneKeyNValues keyType (v:seq<series<'k, float>>) = { data = async {
    let data = google.visualization.DataTable.Create()
    let v = Array.ofSeq v
    data.addColumn(keyType, v.[0].keyName) |> ignore
    for i in 0 .. v.Length - 1 do
      data.addColumn("number", v.[i].seriesName) |> ignore

    let head = v.[0].map(fun v -> Map.ofList [0,v])
    let tail = SeriesInternals.slice 1 (v.Length-1) v |> Array.mapi (fun i v -> i+1, v)
    let all = (head,tail) ||> Array.fold (fun s1 (i, s2) ->
      s1.joinOuter(s2).map(fun (l, r) ->
        match defaultArg l Map.empty, r with
        | lm, Some r -> Map.add i r lm
        | lm, None -> lm ))

    let! vals = all.mapPairs(fun k vals ->
      let data = Array.init v.Length (fun i -> box (defaultArg (Map.tryFind i vals) (Helpers.undefined<_>())))
      Array.append [| box k |] data).data
    vals |> Array.map snd |> data.addRows |> ignore
    return data } }

  let twoValues (v1:series<'k, float>) (v2:series<'k,float>) = { data = async {
    let data = google.visualization.DataTable.Create()
    data.addColumn("number", v1.seriesName) |> ignore
    data.addColumn("number", v2.seriesName) |> ignore
    let! vals = v1.joinInner(v2).map(fun (v1,v2) -> [| box v1; box v2 |]).data
    vals |> Array.map snd |> data.addRows |> ignore
    return data } }
