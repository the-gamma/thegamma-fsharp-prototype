namespace ProviderImplementation.TheGamma

open System
open System.Collections.Generic

open FSharp.Data
open FSharp.Data.Runtime
open FSharp.Data.Runtime.StructuralTypes

open Microsoft.FSharp.Quotations
open Microsoft.FSharp.Core.CompilerServices
open ProviderImplementation.ProvidedTypes

// --------------------------------------------------------------------------------------------------------------------
//
// --------------------------------------------------------------------------------------------------------------------

(*
type JsonGenerationContext =
  { TypeProviderType : ProvidedTypeDefinition
    UniqueNiceName : string -> string }
  static member Create(tpType) =
    { TypeProviderType = tpType
      UniqueNiceName = NameUtils.uniqueGenerator NameUtils.nicePascalName }

type GenerationResult = 
  { ConvertedType : System.Type 
    Converter : Expr<obj> -> Expr<obj> }
*)

module HtmlGenerator =
  open TheGamma.Series

  let createTableType (contTy:ProvidedTypeDefinition) (table:HtmlTable) = 
    let columns = table.InferedProperties.Value
    
    let tableTy = ProvidedTypeDefinition(table.Name, None, HideObjectMethods = true, NonNullable = true)
    let rowTy = ProvidedTypeDefinition(table.Name + " Row", None, HideObjectMethods = true, NonNullable = true)
    contTy.AddMember(tableTy)
    contTy.AddMember(rowTy)

    for field in columns do
      let p = ProvidedProperty(field.Name, rowTy)
      p.GetterCode <- fun _ -> <@@ ((failwith "!") : obj) @@>
      p |> tableTy.AddMember 

    id, typedefof<series<_, _>>.MakeGenericType [| typeof<int>; tableTy :> Type |]


  let createDefinitionListType (t:HtmlDefinitionList) = 
    id, ProvidedTypeDefinition(t.Name, None, HideObjectMethods = true, NonNullable = true)


  let createListType (t:HtmlList) =
    id, ProvidedTypeDefinition(t.Name, None, HideObjectMethods = true, NonNullable = true)


  let rec generateHtmlTypes (contTy:ProvidedTypeDefinition) (resTy:ProvidedTypeDefinition) (htmlObjects:HtmlObject list) =    
    let getPropertyName = NameUtils.uniqueGenerator id

    for htmlObj in htmlObjects do
        match htmlObj with
        | Table table ->
            let create, tableType = createTableType contTy table
            resTy.AddMember <| ProvidedProperty(getPropertyName table.Name, tableType, GetterCode = fun (Singleton doc) -> create doc)
        | List list ->
            let create, tableType = createListType list
            contTy.AddMember tableType
            resTy.AddMember <| ProvidedProperty(getPropertyName list.Name, tableType, GetterCode = fun (Singleton doc) -> create doc)
        | DefinitionList definitionList ->
            let create, tableType = createDefinitionListType definitionList
            contTy.AddMember tableType
            resTy.AddMember <| ProvidedProperty(getPropertyName definitionList.Name, tableType, GetterCode = fun (Singleton doc) -> create doc)

// --------------------------------------------------------------------------------------------------------------------
//
// --------------------------------------------------------------------------------------------------------------------

[<TypeProvider>]
type public Html(cfg:TypeProviderConfig) as this =
  inherit TypeProviderForNamespaces()

  let asm = System.Reflection.Assembly.GetExecutingAssembly()
  let ns = "TheGamma"
  let iniType = ProvidedTypeDefinition(asm, ns, "html", Some(typeof<obj>))
  let parameter = ProvidedStaticParameter("sample", typeof<string>)

  do iniType.DefineStaticParameters([parameter], fun typeName args ->

    // Read the JSON sample and run the type inference on it
    let sample = 
      let value = args.[0] :?> string
      try Async.RunSynchronously(Cache.asyncDownload (Uri(value).ToString()))
      with _ -> args.[0] :?> string

    let unitsOfMeasureProvider = 
      { new StructuralInference.IUnitsOfMeasureProvider with
          member x.SI(str) = ProvidedMeasureBuilder.Default.SI str
          member x.Product(measure1, measure2) = ProvidedMeasureBuilder.Default.Product(measure1, measure2)
          member x.Inverse(denominator): Type = ProvidedMeasureBuilder.Default.Inverse(denominator) }
    
    let inferenceParameters : HtmlInference.Parameters = 
      { MissingValues = TextRuntime.GetMissingValues ""
        CultureInfo = System.Globalization.CultureInfo.InvariantCulture
        UnitsOfMeasureProvider = unitsOfMeasureProvider
        PreferOptionals = true }

    let htmlType = 
      HtmlDocument.Parse sample
      |> HtmlRuntime.getHtmlObjects (Some inferenceParameters) false
      |> List.map (function
          | Table table when table.InferedProperties = None ->
              let ip =
                HtmlInference.inferColumns 
                  inferenceParameters 
                  table.HeaderNamesAndUnits.Value 
                  (if table.HasHeaders.Value then table.Rows.[1..] else table.Rows)
              Table { table with InferedProperties = Some ip }
          | html -> html)

    //|> HtmlGenerator.generateTypes asm ns typeName (inferenceParameters, missingValuesStr, cultureStr) replacer

    // 
    let resTy = ProvidedTypeDefinition(asm, ns, typeName, Some typeof<obj>, HideObjectMethods = true)
    let contTy = ProvidedTypeDefinition("Types", None)
    contTy.AddXmlDoc("[OMIT]")
    resTy.AddMember(contTy)
    //let ctx = JsonGenerationContext.Create(resTy)
    HtmlGenerator.generateHtmlTypes contTy resTy htmlType
    
    let loadM = 
      ProvidedMethod
        ( "read", [], resTy,
          IsStaticMethod = true, InvokeCode = fun (Singleton arg) -> 
            <@@  JS.Json.parseJson(%%arg) @@> )  
    let parseM = 
      ProvidedMethod
        ( "parse", [ProvidedParameter("json", typeof<string>)], resTy,
          IsStaticMethod = true, InvokeCode = fun (Singleton arg) -> 
            <@@  JS.Json.parseJson(%%arg) @@> )  
    resTy.AddMembers [parseM; loadM]
    resTy)
 

  // Register the main (parameterized) type with F# compiler
  do this.AddNamespace(ns, [ iniType ])

[<assembly:TypeProviderAssembly>]
do()