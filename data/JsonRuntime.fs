namespace ProviderImplementation.TheGamma.JS

open FunScript
open FunScript.TypeScript

[<FunScript.JS>]
module Json =
  open Helpers

  let getArrayMembersByTag tag input =
    input |> Array.choose (fun value ->
      match tag, jsTypeOf(value) with
      | "Boolean", "boolean" -> Some(value)
      | "Number", "number" -> Some(value)
      | "String", "string" -> Some(value)
      | "DateTime", "string" -> Some(box (System.DateTime.Parse(unbox value)))
      | "Array", _ when isArray(value) -> Some(value)
      | "Record", "object" when not (isArray(value)) -> Some(value)
      | _ -> None)

  let getArrayMemberByTag tag input =
    (getArrayMembersByTag tag input).[0]

  let parseJson str = Globals.JSON.parse(str)