namespace ProviderImplementation.TheGamma

open System
open FSharp.Data
open FSharp.Data.Runtime
open FSharp.Data.Runtime.StructuralTypes

open Microsoft.FSharp.Quotations
open Microsoft.FSharp.Core.CompilerServices
open ProviderImplementation.ProvidedTypes

// --------------------------------------------------------------------------------------------------------------------
// JSON type inference using F# Data under the cover
// --------------------------------------------------------------------------------------------------------------------

module JsonInference = 
  /// Infer type of a JSON value - this simple function is copied from F# data and it uses
  /// the structural inference from F# Data to do all the interesting work.
  let rec inferType inferTypesFromValues cultureInfo parentName json =
    let inline inrange lo hi v = (v >= decimal lo) && (v <= decimal hi)
    let inline integer v = Math.Round(v:decimal) = v

    match json with
    // Null and primitives without subtyping hiearchies
    | JsonValue.Null -> InferedType.Null
    | JsonValue.Boolean _ -> InferedType.Primitive(typeof<bool>, None, false)
    | JsonValue.String s when inferTypesFromValues -> StructuralInference.getInferedTypeFromString cultureInfo s None
    | JsonValue.String _ -> InferedType.Primitive(typeof<string>, None, false)
    // For numbers, we test if it is integer and if it fits in smaller range
    | JsonValue.Number 0M when inferTypesFromValues -> InferedType.Primitive(typeof<Bit0>, None, false)
    | JsonValue.Number 1M when inferTypesFromValues -> InferedType.Primitive(typeof<Bit1>, None, false)
    | JsonValue.Number n when inferTypesFromValues && inrange Int32.MinValue Int32.MaxValue n && integer n -> 
        InferedType.Primitive(typeof<int>, None, false)
    | JsonValue.Number n when inferTypesFromValues && inrange Int64.MinValue Int64.MaxValue n && integer n -> 
        InferedType.Primitive(typeof<int64>, None, false)
    | JsonValue.Number _ -> InferedType.Primitive(typeof<decimal>, None, false)
    | JsonValue.Float _ -> InferedType.Primitive(typeof<float>, None, false)
    // More interesting types 
    | JsonValue.Array ar -> 
        StructuralInference.inferCollectionType (*allowEmptyValues*)false 
          (Seq.map (inferType inferTypesFromValues cultureInfo (NameUtils.singularize parentName)) ar)
    | JsonValue.Record properties ->
        let name = if String.IsNullOrEmpty parentName then None else Some parentName
        let props = 
          [ for propName, value in properties -> 
              let t = inferType inferTypesFromValues cultureInfo propName value
              { Name = propName; Type = t } ]
        InferedType.Record(name, props, false)

// --------------------------------------------------------------------------------------------------------------------
//
// --------------------------------------------------------------------------------------------------------------------

type JsonGenerationContext =
  { TypeProviderType : ProvidedTypeDefinition
    UniqueNiceName : string -> string }
  static member Create(tpType) =
    { TypeProviderType = tpType
      UniqueNiceName = NameUtils.uniqueGenerator NameUtils.nicePascalName }

type GenerationResult = 
  { ConvertedType : System.Type 
    Converter : Expr<obj> -> Expr<obj> }

module JsonGenerator =
  let makeLambda f =
    let v = Var.Global("x", typeof<obj>)
    Expr.Cast<obj -> obj>(Expr.Lambda(v, f (Expr.Cast(Expr.Var v))))

  let dropNullFromCollection = function
    | InferedType.Collection(order, types) ->
        InferedType.Collection (List.filter ((<>) InferedTypeTag.Null) order, Map.remove InferedTypeTag.Null types)
    | x -> x

  let rec generateJsonType ctx inferedType =
    match dropNullFromCollection inferedType with
    | InferedType.Primitive(inferedType, _, optional) ->
        // Option<T> if optional
        let inferedType = 
          if inferedType = typeof<Bit> || inferedType = typeof<Bit0> || inferedType = typeof<Bit1> 
            then typeof<bool> else inferedType
        let converter = 
          if inferedType = typeof<bool> then fun e -> <@ box(if (unbox %e) then true else false) @>
          elif inferedType = typeof<int> then fun e -> <@ box(1 * (unbox %e)) @>
          elif inferedType = typeof<float> then fun e -> <@ box(1.0 * (unbox %e)) @>
          elif inferedType = typeof<decimal> then fun e -> <@ box(1.0 * (unbox %e)) @>
          else id 

        let fieldTyp, converter =
          if not optional then inferedType, converter
          else
            typedefof<option<_>>.MakeGenericType [| inferedType |],
            fun e -> <@ box(if JS.Helpers.isNull %e then None else Some(%(converter e))) @>

        { ConvertedType = fieldTyp 
          Converter = converter }

    | InferedType.Top 
    | InferedType.Null -> 
        { ConvertedType = typeof<obj>
          Converter = fun e -> e }

    | InferedType.Collection (_, SingletonMap(_, (_, typ)))
    | InferedType.Collection (_, EmptyMap InferedType.Top typ) -> 
        let elemRes = generateJsonType ctx typ
        let conv = fun e -> <@ unbox<obj[]> %e |> Array.map (%makeLambda elemRes.Converter) |> box @>
        { ConvertedType = elemRes.ConvertedType.MakeArrayType()
          Converter = conv }

    | InferedType.Record(name, props, _) -> 
        
        // Generate new type for the record
        let name = ctx.UniqueNiceName(defaultArg name "Record")
        let objectTy = ProvidedTypeDefinition(name, Some(typeof<obj>), HideObjectMethods=true, NonNullable=true)
        ctx.TypeProviderType.AddMember(objectTy)

        // Add all record fields as properties
        let members = 
          [ for prop in props ->
              let propResult = generateJsonType ctx prop.Type
              let propName = prop.Name
              let getter = fun (Singleton v) -> propResult.Converter <@ JS.Helpers.getProperty (%%v) propName @> :> Expr
              let convertedType = propResult.ConvertedType
              ProvidedProperty(prop.Name, convertedType, GetterCode=getter) ]

        objectTy.AddMembers(members)
        { ConvertedType = objectTy
          Converter = id } 

    | InferedType.Collection (_, types) -> 

        // Generate new type for the collection
        let name = ctx.UniqueNiceName("Collection")
        let colTy = ProvidedTypeDefinition(name, Some(typeof<obj>), HideObjectMethods=true, NonNullable=true)
        ctx.TypeProviderType.AddMember(colTy)

        let members = 
          [ for KeyValue(tag, (multiplicity, typ)) in types ->
              let caseRes = generateJsonType ctx typ
              let tagName = 
                match tag with
                | InferedTypeTag.Record _ -> "Record"
                | _ -> tag.NiceName
              let typ, getter = 
                match multiplicity with 
                | InferedMultiplicity.Single ->
                    caseRes.ConvertedType, fun (Singleton v) -> 
                      caseRes.Converter <@ JS.Json.getArrayMemberByTag tagName (unbox (%%v:obj)) @> :> Expr
                | InferedMultiplicity.Multiple ->
                    caseRes.ConvertedType.MakeArrayType(), fun (Singleton v) ->
                      <@ JS.Json.getArrayMembersByTag tagName (unbox (%%v:obj)) 
                         |> Array.map (%makeLambda caseRes.Converter)
                         |> box @> :> Expr
                | _ -> failwith "Optional"
              ProvidedProperty(tagName, caseRes.ConvertedType, GetterCode=getter) ]
        colTy.AddMembers(members)
        { ConvertedType = colTy
          Converter = id }
    
    | _ -> failwith "Json provider: Heterogeneous is todo"
                 
(*
        // Generate a choice type that calls either `GetArrayChildrenByTypeTag`
        // or `GetArrayChildByTypeTag`, depending on the multiplicity of the item
        generateMultipleChoiceType ctx types (*forCollevyction*)true nameOverride (fun multiplicity result tagCode ->
          match multiplicity with
          | InferedMultiplicity.Single -> fun (Singleton jDoc) -> 
              // Generate method that calls `GetArrayChildByTypeTag`
              let jDoc = ctx.Replacer.ToDesignTime jDoc
              let cultureStr = ctx.CultureStr
              result.GetConverter ctx <@@ JsonRuntime.GetArrayChildByTypeTag(%%jDoc, cultureStr, tagCode) @@>
          
          | InferedMultiplicity.Multiple -> fun (Singleton jDoc) -> 
              // Generate method that calls `GetArrayChildrenByTypeTag` 
              // (unlike the previous easy case, this needs to call conversion function
              // from the runtime similarly to options and arrays)
              let cultureStr = ctx.CultureStr
              ctx.JsonRuntimeType?GetArrayChildrenByTypeTag (result.ConvertedTypeErased ctx) (jDoc, cultureStr, tagCode, result.ConverterFunc ctx)
          
          | InferedMultiplicity.OptionalSingle -> fun (Singleton jDoc) -> 
              // Similar to the previous case, but call `TryGetArrayChildByTypeTag`
              let cultureStr = ctx.CultureStr
              ctx.JsonRuntimeType?TryGetArrayChildByTypeTag (result.ConvertedTypeErased ctx) (jDoc, cultureStr, tagCode, result.ConverterFunc ctx))

    | InferedType.Heterogeneous types -> getOrCreateType ctx inferedType <| fun () ->

        // Generate a choice type that always calls `TryGetValueByTypeTag`
        let types = types |> Map.map (fun _ v -> InferedMultiplicity.OptionalSingle, v)
        generateMultipleChoiceType ctx types (*forCollection*)false nameOverride (fun multiplicity result tagCode -> fun (Singleton jDoc) -> 
          assert (multiplicity = InferedMultiplicity.OptionalSingle)
          let cultureStr = ctx.CultureStr
          ctx.JsonRuntimeType?TryGetValueByTypeTag (result.ConvertedTypeErased ctx) (jDoc, cultureStr, tagCode, result.ConverterFunc ctx))
*)

// --------------------------------------------------------------------------------------------------------------------
//
// --------------------------------------------------------------------------------------------------------------------

[<TypeProvider>]
type public Json(cfg:TypeProviderConfig) as this =
  inherit TypeProviderForNamespaces()

  let asm = System.Reflection.Assembly.GetExecutingAssembly()
  let ns = "TheGamma"
  let iniType = ProvidedTypeDefinition(asm, ns, "json", Some(typeof<obj>))
  let parameter = ProvidedStaticParameter("sample", typeof<string>)

  do iniType.DefineStaticParameters([parameter], fun typeName args ->

    // Read the JSON sample and run the type inference on it
    let sample = 
      let value = args.[0] :?> string
      try Async.RunSynchronously(Cache.asyncDownload (Uri(value).ToString()))
      with _ -> args.[0] :?> string
    
    let culture = System.Globalization.CultureInfo.InvariantCulture
    let inferedType = JsonInference.inferType true culture "root" (JsonValue.Parse(sample))

    // 
    let resTy = ProvidedTypeDefinition(asm, ns, typeName, Some typeof<obj>)
    let ctx = JsonGenerationContext.Create(resTy)
    let topLevelTy = JsonGenerator.generateJsonType ctx inferedType
    
    let parseM = 
      ProvidedMethod
        ( "parse", [ProvidedParameter("json", typeof<string>)], topLevelTy.ConvertedType,
          IsStaticMethod = true, InvokeCode = fun (Singleton arg) -> 
            <@@  JS.Json.parseJson(%%arg) @@> )  
    let wrapM = 
      ProvidedMethod
        ( "wrap", [ProvidedParameter("object", typeof<obj>)], topLevelTy.ConvertedType,
          IsStaticMethod = true, InvokeCode = fun (Singleton arg) -> arg )
    resTy.AddMembers [parseM; wrapM]
    resTy)
 

  // Register the main (parameterized) type with F# compiler
  do this.AddNamespace(ns, [ iniType ])

[<assembly:TypeProviderAssembly>]
do()