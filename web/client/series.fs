﻿namespace TheGamma.Series

open FunScript

// --------------------------------------------------------------------------------------------------------------------
// Series helpers - various JavaScript functions needed for simple series implementation
// --------------------------------------------------------------------------------------------------------------------

[<JS>]
module SeriesInternals =
  open System.Collections.Generic

  [<FunScript.JSEmitInline("({0}==null)")>]
  let isNull(o:obj) : bool = failwith "never"

  [<FunScript.JSEmitInline("{0}[{1}]")>]
  let getProperty<'T> (obj:obj) (name:string) : 'T = failwith "never"

  [<JSEmitInline("({0} < {1} ? -1 : ({0} == {1} ? 0 : 1))")>]
  let compare (x:'a) (y:'a) : int = failwith "never" 

  let slice lo hi (arr:'T[]) =
    Array.init (hi - lo + 1) (fun i -> arr.[lo + i])        

  let dictAny (v:seq<'k*'v>) = unbox<IDictionary<'k,'v>> (dict (unbox<seq<obj * obj>> v))
   
  let zipUnsorted (arr1:_[]) (arr2:_[]) =
    let d1 = dictAny arr1
    let d2 = dictAny arr2
    let res = ResizeArray<_>()
    for kv1 in d1 do
      let v2 = 
        if d2.ContainsKey(kv1.Key) then Some(d2.[kv1.Key])
        else None
      res.Add(kv1.Key, (Some kv1.Value, v2))
    for kv2 in d2 do 
      if not (d1.ContainsKey(kv2.Key)) then
        res.Add(kv2.Key, (None, Some kv2.Value))
    Array.ofSeq res

  let isSortedUsing test proj (arr:_[]) =
    let rec loop i =
      if i = arr.Length then true
      else test (proj arr.[i-1]) (proj arr.[i]) && loop (i+1)
    arr.Length = 0 || loop 1    

  let zipSorted (arr1:('k*'v1)[]) (arr2:('k*'v2)[]) = 
    let mutable i1 = 0
    let mutable i2 = 0
    let inline (<.) (a:'k) (b:'k) = compare a b < 0
    let inline eq (a:'k) (b:'k) = compare a b = 0
    let res = ResizeArray<_>()
    while i1 < arr1.Length && i2 < arr2.Length do
      let (k1, v1), (k2, v2) = arr1.[i1], arr2.[i2] 
      if eq k1 k2 then 
        res.Add(k1, (Some v1, Some v2))
        i1 <- i1 + 1
        i2 <- i2 + 1
      elif k1 <. k2 then
        res.Add(k1, (Some v1, None))
        i1 <- i1 + 1
      elif k2 <. k1 then
        res.Add(k2, (None, Some v2))
        i2 <- i2 + 1
    while i1 < arr1.Length do
      let k1, v1 = arr1.[i1]
      res.Add(k1, (Some v1, None))
      i1 <- i1 + 1
    while i2 < arr2.Length do
      let k2, v2 = arr2.[i2]
      res.Add(k2, (None, Some v2))
      i2 <- i2 + 2
    Array.ofSeq res

  let zipAny (arr1:('k*'v1)[]) (arr2:('k*'v2)[]) = 
    let inline (<=.) (a:'k) (b:'k) = compare a b <= 0
    let inline (>=.) (a:'k) (b:'k) = compare a b >= 0
    if isSortedUsing (<=.) fst arr1 && isSortedUsing (<=.) fst arr2 then zipSorted arr1 arr2
    elif isSortedUsing (>=.) fst arr1 && isSortedUsing (>=.) fst arr2 then Array.rev (zipSorted (Array.rev arr1) (Array.rev arr2))
    else zipUnsorted arr1 arr2

// --------------------------------------------------------------------------------------------------------------------
// Async series library for TheGamma - implements type `series<'k, 'v>` with various operations
// --------------------------------------------------------------------------------------------------------------------

open SeriesInternals
open TheGamma.Series

type value<'k> = { value : Async<'k> }

[<AutoOpen; JS>]
module Operations = 
  let inline lift f (s:series<_, _>) = 
    s.set(async { 
      let! vs = s.data
      return f vs })

  let inline liftAggregation f (s:series<_, _>) = 
    { value = async { 
        let! vs = s.data
        return f vs } }

  type series<'k, 'v> with
    member s.sortKeys(?reverse) = 
      s |> lift (fun arr ->
        arr |> Array.sortWith (fun (k1, _) (k2, _) -> compare k1 k2) 
            |> (if reverse = Some true then Array.rev else id))
    
    member s.sortValues(?reverse) =
      s |> lift (fun arr ->
        arr |> Array.sortWith (fun (_,v1) (_,v2) -> compare v1 v2) 
            |> (if reverse = Some true then Array.rev else id))

    member s.reverse() = 
      s |> lift (Array.rev)

    member s.take(count) = 
      s |> lift (fun arr -> slice 0 ((min arr.Length count)-1) arr)

    member s.skip(count) = 
      s |> lift (fun arr -> slice (min arr.Length count) (arr.Length-1) arr)

    member s.map(f) =
      s |> lift (Array.map (fun (k, v) -> k, f v))

    member s.mapTask(f:'v -> value<'r>) =
      s.set(async {
        let! arr = s.data
        let res = Array.init arr.Length (fun _ -> None)
        for i in 0 .. arr.Length-1 do
          let! r = (f(snd arr.[i])).value
          res.[i] <- Some r
        return Array.init arr.Length (fun i -> fst arr.[i], res.[i].Value)
      })

    member s.mapPairs(f) =
      s |> lift (Array.map (fun (k, v) -> k, f k v))

    member s.filter(f) =
      s |> lift (Array.filter (snd >> f))

    member s.choose(f) =
      s |> lift (Array.choose (fun (k, v) -> match f v with None -> None | Some r -> Some(k, r)))

    member s.joinOuter<'v2>(s2:series<'k, 'v2>) : series<'k, 'v option * 'v2 option>=
      let data = async {
        let! v1 = s.data
        let! v2 = s2.data
        return zipAny v1 v2 }
      series.create(data, s.keyName, "Values", s.seriesName + " and " + s2.seriesName)

    member s.joinInner<'v2>(s2:series<'k, 'v2>) : series<'k, 'v * 'v2>=
      s.joinOuter(s2).choose(function Some(v1), Some(v2) -> Some((v1, v2)) | _ -> None)

    [<CompiledName("appendScalar")>]
    member s.append(key:'k, value:'v) = 
      s |> lift (fun arr -> Array.append arr [| key, value |])

    [<CompiledName("appendValue")>]
    member s.append(key:'k, value:value<'v>) = 
      s.set(async {
        let! arr = s.data
        let! v = value.value
        return Array.append arr [| key, v |] })

    member s.append(s2:series<'k, 'v>) = 
      s.set(async {
        let! arr1 = s.data
        let! arr2 = s2.data
        return Array.append arr1 arr2 })

    member s.last() = 
      s |> liftAggregation (fun arr -> snd arr.[arr.Length - 1])

    member s.first() = 
      s |> liftAggregation (fun arr -> snd arr.[0])

open System.Runtime.CompilerServices

[<Extension; JS>]
type SeriesExtensions = 
  [<Extension>]
  static member sum(s:series<'k, float>) = 
    s |> liftAggregation (Array.sumBy snd)

  [<Extension>]
  static member series(values:seq<'v>) = 
    let getKey i (v:'v) =
      let name = getProperty<string> v "name"
      let id = getProperty<string> v "id"
      if not (isNull name) then name 
        elif not (isNull id) then id
          else string i
    let data = async { return values |> Array.ofSeq |> Array.mapi (fun i v -> getKey i v, v) }
    series.create(data, "Key", "Value", "Series")

  [<Extension>]
  static member series(values:list<'v>) =
     SeriesExtensions.series(values :> seq<_>) 

[<Extension; JS>]
type TupleExtensions = 
  [<Extension>]
  static member map((a,b), f) = (f a, f b)
  [<Extension>]
  static member map((a,b,c), f) = (f a, f b, f c)
  [<Extension>]
  static member map((a,b,c,d), f) = (f a, f b, f c, f d)
  [<Extension>]
  static member map((a,b,c,d,e), f) = (f a, f b, f c, f d, f e)
  [<Extension>]
  static member map((a,b,c,d,e,g), f) = (f a, f b, f c, f d, f e, f g)
  [<Extension>]
  static member map((a,b,c,d,e,g,h), f) = (f a, f b, f c, f d, f e, f g, f h)

