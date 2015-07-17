namespace ProviderImplementation.TheGamma.JS

open FunScript
open FunScript.TypeScript

[<FunScript.JS>]
module Async = 
  let PseudoParallel work = 
    Async.FromContinuations(fun (cont, econt, ccont) ->
      let workItems = Array.ofSeq work
      if workItems.Length = 0 then cont [||] else
      let results = workItems |> Array.map (fun _ -> None)
      let count = ref 0
      for i in 0 .. workItems.Length - 1 do
        async { let! res = workItems.[i]
                results.[i] <- Some(res)
                count.contents <- count.contents + 1
                if count.contents = results.Length then cont(Array.map Option.get results) }
        |> Async.StartImmediate )

[<FunScript.JS>]
module Helpers =
  [<JSEmit("return typeof({0});")>]
  let jsTypeOf(o:obj) : string = failwith "never"
  
  [<JSEmit("return Array.isArray({0});")>]
  let isArray(o:obj) : bool = failwith "never"
  
  [<JSEmit("return {0}==null;")>]
  let isNull(o:obj) : bool = failwith "never"
  
  [<JSEmit("return typeof({0}[{1}]) != \"undefined\";")>]
  let hasProperty(o:obj,name:string) : bool = failwith "never"
  
  [<JSEmit("return {0}[{1}];")>]
  let getProperty obj (name:string) : obj = failwith "never"

  [<JS; JSEmit("return encodeURIComponent({0});")>]
  let encodeURIComponent(s:string) : string = failwith "never"

  [<JS; JSEmit("""
    $.ajax({
      url: {0},
      dataType: 'jsonp',
      jsonp: 'prefix',
      jsonpCallback: 'wb_prefix_' + Math.random().toString().substr(2),
      error: function(jqXHR, textStatus, errorThrown){
          console.log(textStatus + errorThrown);
      },
      success: function(data){
          {1}(data);
      }
    });
  """)>]
  let getJSONPrefix (url:string, callback : string -> unit) : unit = failwith "never"

  [<JSEmit("return window[{0}];")>]
  let getGlobal (name:string) = failwith "!"

  [<JSEmit("window[{0}] = {1};")>]
  let setGlobal (name:string) value : unit = failwith "!"

  let tryGetGlobal name = 
    let v = getGlobal name
    if isNull(v) then None
    else Some(v)
