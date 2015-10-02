namespace ProviderImplementation.TheGamma

open System
open FSharp.Data
open FSharp.Data.Runtime
open FSharp.Data.Runtime.StructuralTypes

open Microsoft.FSharp.Quotations
open Microsoft.FSharp.Core.CompilerServices
open ProviderImplementation.ProvidedTypes
open TheGamma.Series

open Google.GData.Client
open Google.GData.Spreadsheets

// --------------------------------------------------------------------------------------------------------------------
//
// --------------------------------------------------------------------------------------------------------------------

module SheetsHelpers = 
  let getOAuthParameters () =
    let service = new SpreadsheetsService("The Gamma")
    let parameters = new OAuth2Parameters()
    parameters.ClientId <- "403608197456-gour66g2r88iaslppf2rdhlb7tc56tmm.apps.googleusercontent.com"
    parameters.ClientSecret <- "8Uqqv_v2FQLXW6AH_u16AI0x"
    parameters.RedirectUri <- "http://localhost/oauth2callback" // "http://thegamma.net/oauth2callback"
    parameters.Scope <- "https://spreadsheets.google.com/feeds"
    parameters
  
  let getOAuthUrl () =
    let parameters = getOAuthParameters()
    OAuthUtil.CreateOAuth2AuthorizationUrl(parameters)
  
  let sheetsDataChanged = Event<unit>()

  let sheetsData = 
    MailboxProcessor<AsyncReplyChannel<int>>.Start(fun inbox -> 
      let rec loop n = async {
        sheetsDataChanged.Trigger()
        let! c = inbox.TryReceive(10000)
        match c with 
        | None -> return! loop (n + 1)
        | Some r ->
            r.Reply(n)
            return! loop n }
      loop 0)

// --------------------------------------------------------------------------------------------------------------------
//
// --------------------------------------------------------------------------------------------------------------------

[<TypeProvider>]
type public Sheets(cfg:TypeProviderConfig) as this =
  inherit TypeProviderForNamespaces()

  do this.RegisterRuntimeAssemblyLocationAsProbingFolder(cfg)
  
  let asm = System.Reflection.Assembly.GetExecutingAssembly()
  let ns = "TheGamma"
  let wbType = ProvidedTypeDefinition(asm, ns, "sheets", Some(typeof<obj>))
  do wbType.AddXmlDoc("""
      <h2>Google Sheets</h2>
      <div style="text-align:center">
        <img src="/content/thegamma/sheets.png" style="width:120px;margin:10px 0px 20px 0px" />
      </div>
      <p>
        Use Google Spreadsheets to import data from external data sources
        or to upload your data to TheGamma. Go to <a href="https://docs.google.com/spreadsheets">Google Spreadsheets</a>
        to create your spreadsheets. To access your data in TheGamma, follow the
        login link below:</p>
      <ul>
        <li><a href='""" + (SheetsHelpers.getOAuthUrl()) + 
      """'>Log in to access Google Spreadsheets</a></li></ul>""")

  (*
  let domType = ProvidedTypeDefinition("types", None)
  do domType.AddXmlDoc("[OMIT]")
  do wbType.AddMember(domType)

  let meta = WorldBankGenerator.getIndicatorMeta() |> Async.StartAsTask
  let countries = WorldBankGenerator.getCountryMeta() |> Async.StartAsTask

  let generateIndicators 
      (indicators:Indicators.Record[]) 
      (getter:Expr<'T> -> Expr<string> -> Expr<string> -> Expr<series<'K, float>>) = 
    [ for indicator in indicators ->
        let prop = ProvidedProperty(indicator.Name, typeof<series<'K, float>>)
        let id, name = indicator.Id, indicator.Name
        let topics = 
          if indicator.Topics.Length = 0 then ""
          else 
            (indicator.Topics |> Seq.choose (fun t -> t.Value) |> String.concat ", ")
            |> sprintf "<p><strong>Topics:</strong> %s</p>"
        sprintf 
          "<h2>%s</h2><p>%s</p><p><strong>Source:</strong> %s</p> %s" 
          indicator.Name indicator.SourceNote indicator.SourceOrganization topics
          |> prop.AddXmlDoc
        prop.GetterCode <- fun (Singleton this) -> upcast getter (Expr.Cast<'T>(this)) <@ id @> <@ name @>
        prop ]

  let generateTopicsType name 
      (getter:Expr<'T> -> Expr<string> -> Expr<string> -> Expr<series<'K, float>>) = 
    let baseType = typeof<'T>
    let indType = ProvidedTypeDefinition(name, Some(baseType), HideObjectMethods=true)
    domType.AddMember(indType)
    indType.AddMembersDelayed(fun () ->
      [ let domType = ProvidedTypeDefinition("types", None)
        yield domType :> Reflection.MemberInfo
        for name, indicators in meta.Result.Topics do
          let topicTy = ProvidedTypeDefinition(name, Some(baseType), HideObjectMethods=true)
          domType.AddMember(topicTy)
          topicTy.AddMembersDelayed(fun () -> generateIndicators indicators getter)
          let prop = ProvidedProperty(name, topicTy, GetterCode = fun (Singleton this) -> this) 
          
          let samples, more = 
            let names = indicators |> Array.map (fun i -> i.Name)
            if names.Length > 6 then 
              names.[0 .. 5], (sprintf "(and %d other indicators)" (names.Length-6))
            else names, ""

          prop.AddXmlDoc(
            "<h2>" + name + """</h2>
            <p>This topic includes for example the following indicators:</p><ul>""" +
            (String.concat "" [ for s in samples -> "<li>" + s + "</li>" ])  + "</ul>" + more)          
          yield prop :> _ ])
    indType

  let countryType = generateTopicsType "Country" (fun v id name -> 
    <@ series.create(JS.WorldBank.getByCountry %v %id, "Year", "Value", %name) @>)
  let yearType = generateTopicsType "Year" (fun v id name ->   
    <@ series.create(JS.WorldBank.getByYear %v %id, "Country", "Value", %name) @>)

  let generate() =
    let bcTyp = ProvidedTypeDefinition("byCountry", None)
    let bcProp = ProvidedProperty("byCountry", bcTyp, IsStatic=true, GetterCode = fun _ -> <@@ obj() @@>)
    bcProp.AddXmlDoc("""
          <h2>Indicators by country</h2>
          <p>Choose this option to first select a <em>country</em>
            and then choose one of the available indicators.
            The result is a series with data for all available years.</p>
          <p>For example:</p>
          <pre>world.byCountry
  .Greece.Education
  .``Labor force, total``</pre>
          <p>Returns a series with years as the keys and number of people
            in labor force as values:</p>
          <table>
            <tr><td>1990</td><td>4182950.0</td></tr>
            <tr><td>1991</td><td>4133310.0</td></tr>
            <tr><td>1992</td><td>4256628.0</td></tr>
            <tr><td>...</td><td>...</td></tr>
            <tr><td>2012</td><td>5039973.0</td></tr>
            <tr><td>2013</td><td>5007178.0</td></tr>
          </table>""")
    bcTyp.AddMembersDelayed(fun () ->
      [ for c in countries.Result ->
          let prop = ProvidedProperty(c.Name, countryType)
          prop.AddXmlDoc(WorldBankGenerator.getCountryXmlComment c)
          let code, name = c.Id, c.Name
          prop.GetterCode <- fun _ -> 
            <@@ JS.WorldBank.getCountry code name @@>
          prop ])

    let byTyp = ProvidedTypeDefinition("byYear", None)
    let byProp = ProvidedProperty("byYear", byTyp, IsStatic=true, GetterCode = fun _ -> <@@ obj() @@>)
    byProp.AddXmlDoc("""
          <h2>Indicators by year</h2>
          <p>Choose this option to first select a <em>year</em>
            and then choose one of the available indicators.
            The result is a series with data for all countries.</p>
          <p>For example:</p>
          <pre>world.byYear
  .``2010``.``Climate Change``
  .``CO2 emissions (kt)``</pre>
          <p>Returns a series with country names as the keys and carbon
            emission data as values:</p>
          <table>
            <tr><td>Aruba</td><td>2456.89</td></tr>
            <tr><td>Afghanistan</td><td>8470.77</td></tr>
            <tr><td>Angola</td><td>29743.037</td></tr>
            <tr><td>...</td><td>...</td></tr>
            <tr><td>Zambia</td><td>2673.243</td></tr>
            <tr><td>Zimbabwe</td><td>9028.154</td></tr>
          </table>""")
    byTyp.AddMembersDelayed(fun () ->
      [ for y in 1950 .. DateTime.Now.Year ->
          let prop = ProvidedProperty(string y, yearType)
          prop.GetterCode <- fun _ -> 
            <@@ JS.WorldBank.getYear y @@>
          prop ])
    domType.AddMembers [ bcTyp; byTyp ]
    wbType.AddMembers [ bcProp; byProp ]

  *)

  let generate() =
    SheetsHelpers.sheetsDataChanged.Publish.Add(fun () -> 
      this.Invalidate()
      printfn "Invalidate..."
    )
    wbType.AddMemberDelayed(fun () ->
      let r = SheetsHelpers.sheetsData.PostAndReply(id)
      ProvidedTypeDefinition(sprintf "Loading %d" r, None)
    )

  do generate()

  // Register the main (parameterized) type with F# compiler
  do this.AddNamespace(ns, [ wbType ])

[<assembly:TypeProviderAssembly>]
do()