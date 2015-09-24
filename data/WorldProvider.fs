namespace ProviderImplementation.TheGamma

open System
open FSharp.Data
open FSharp.Data.Runtime
open FSharp.Data.Runtime.StructuralTypes

open Microsoft.FSharp.Quotations
open Microsoft.FSharp.Core.CompilerServices
open ProviderImplementation.ProvidedTypes

open TheGamma.Series

// --------------------------------------------------------------------------------------------------------------------
//
// --------------------------------------------------------------------------------------------------------------------

type Indicators = JsonProvider<"http://api.worldbank.org/indicator?per_page=120&format=json">
type Countries = JsonProvider<"http://api.worldbank.org/country?per_page=100&format=json">
type Regions = JsonProvider<"http://api.worldbank.org/regions?per_page=100&format=json">
type Data = JsonProvider<"http://api.worldbank.org/countries/indicators/SP.POP.TOTL?per_page=1000&date=2010:2010&format=json">

type IndicatorInfo =
  { Topics : (string * Indicators.Record[])[] }

module WorldBankGenerator =
//  JsonProvider<"""{"a":"1.0"}""", Culture="pl-PL">.GetSample().
 
  let googleChartsRegions = 
    [ "015", (*Northern Africa*) [ "DZ"; "EG"; "EH"; "LY"; "MA"; "SD"; "TN" ]  
      "011", (*Western Africa*) [ "BF"; "BJ"; "CI"; "CV"; "GH"; "GM"; "GN"; "GW"; "LR"; "ML"; "MR"; "NE"; "NG"; "SH"; "SL"; "SN"; "TG" ]
      "017", (*Middle Africa*) [ "AO"; "CD"; "ZR"; "CF"; "CG"; "CM"; "GA"; "GQ"; "ST"; "TD" ]
      "014", (*Eastern Africa*) [ "BI"; "DJ"; "ER"; "ET"; "KE"; "KM"; "MG"; "MU"; "MW"; "MZ"; "RE"; "RW"; "SC"; "SO"; "TZ"; "UG"; "YT"; "ZM"; "ZW" ]
      "018", (*Southern Africa*) [ "BW"; "LS"; "NA"; "SZ"; "ZA" ]
      "154", (*Northern Europe*) [ "GG"; "JE"; "AX"; "DK"; "EE"; "FI"; "FO"; "GB"; "IE"; "IM"; "IS"; "LT"; "LV"; "NO"; "SE"; "SJ" ]
      "155", (*Western Europe*) [ "AT"; "BE"; "CH"; "DE"; "DD"; "FR"; "FX"; "LI"; "LU"; "MC"; "NL" ]
      "151", (*Eastern Europe*) [ "BG"; "BY"; "CZ"; "HU"; "MD"; "PL"; "RO"; "RU"; "SU"; "SK"; "UA" ]
      "039", (*Southern Europe*) [ "AD"; "AL"; "BA"; "ES"; "GI"; "GR"; "HR"; "IT"; "ME"; "MK"; "MT"; "CS"; "RS"; "PT"; "SI"; "SM"; "VA"; "YU" ]
      "021", (*Northern America*) [ "BM"; "CA"; "GL"; "PM"; "US" ]
      "029", (*Caribbean*) [ "AG"; "AI"; "AN"; "AW"; "BB"; "BL"; "BS"; "CU"; "DM"; "DO"; "GD"; "GP"; "HT"; "JM"; "KN"; "KY"; "LC"; "MF"; "MQ"; "MS"; "PR"; "TC"; "TT"; "VC"; "VG"; "VI" ]
      "013", (*Central America*) [ "BZ"; "CR"; "GT"; "HN"; "MX"; "NI"; "PA"; "SV" ]
      "005", (*South America*) [ "AR"; "BO"; "BR"; "CL"; "CO"; "EC"; "FK"; "GF"; "GY"; "PE"; "PY"; "SR"; "UY"; "VE" ]
      "143", (*Central Asia*) [ "TM"; "TJ"; "KG"; "KZ"; "UZ" ]
      "030", (*Eastern Asia*) [ "CN"; "HK"; "JP"; "KP"; "KR"; "MN"; "MO"; "TW" ]
      "034", (*Southern Asia*) [ "AF"; "BD"; "BT"; "IN"; "IR"; "LK"; "MV"; "NP"; "PK" ]
      "035", (*South-Eastern Asia*) [ "BN"; "ID"; "KH"; "LA"; "MM"; "BU"; "MY"; "PH"; "SG"; "TH"; "TL"; "TP"; "VN" ]
      "145", (*Western Asia*) [ "AE"; "AM"; "AZ"; "BH"; "CY"; "GE"; "IL"; "IQ"; "JO"; "KW"; "LB"; "OM"; "PS"; "QA"; "SA"; "NT"; "SY"; "TR"; "YE"; "YD" ]
      "053", (*Australia and New Zealand*) [ "AU"; "NF"; "NZ" ]
      "054", (*Melanesia*) [ "FJ"; "NC"; "PG"; "SB"; "VU" ]
      "057", (*Micronesia*) [ "FM"; "GU"; "KI"; "MH"; "MP"; "NR"; "PW" ]
      "061", (*Polynesia*) [ "AS"; "CK"; "NU"; "PF"; "PN"; "TK"; "TO"; "TV"; "WF"; "WS" ] ]
    |> List.collect (fun (region, countries) ->
        [for c in countries -> c, region])
    |> dict

  let private sources = 
    set [ "World Development Indicators"; "Global Financial Development" ]

  let private getCountries region = async {
    let region = match region with None -> "" | Some region -> "&region=" + region
    let! first = Cache.asyncDownload("http://api.worldbank.org/country?per_page=100&format=json" + region) 
    let first = Countries.Parse(first)          
    let! records = 
      [ for p in 1 .. first.Record.Pages -> async {
          let url = "http://api.worldbank.org/country?per_page=100&format=json&page=" + (string p) + region
          let! data = Cache.asyncDownload url
          let page = Countries.Parse(data)
          return page.Array } ] |> Async.Parallel
    return Array.concat records }
  
  let private getIndicators() = async {
    let! first = Cache.asyncDownload("http://api.worldbank.org/indicator?per_page=100&format=json")
    let first = Indicators.Parse(first)
    let! records = 
      [ for p in 1 .. first.Record.Pages -> async {
          let url = "http://api.worldbank.org/indicator?per_page=100&format=json&page=" + (string p)
          let! data = Cache.asyncDownload url
          let page = Indicators.Parse(data)
          return page.Array } ] |> Async.Parallel
    return Array.concat records }

  type CountryInfo = 
    { Capital : string
      Region : string
      IncomeLevel : string 
      Population : string }

  let countryInfoLookup = 
    async { 
      let! countries = getCountries None
      let! pop = Cache.asyncDownload "http://api.worldbank.org/countries/indicators/SP.POP.TOTL?per_page=1000&date=2010:2010&format=json"
      let pop = Data.Parse(pop)
      let popLookup = dict [ for c in pop.Array -> c.Country.Value, c.Value ]
      let inline asStr v = match v with Some v -> System.Web.HttpUtility.JavaScriptStringEncode(string v) | _ -> ""
      return
        [ for c in countries do
           if c.Region.Id <> "NA" then 
            let details = 
              { Population = asStr (match popLookup.TryGetValue(c.Name) with true, v -> v | _ -> None) 
                Region = c.Region.Value; Capital = asStr c.CapitalCity; 
                IncomeLevel = c.IncomeLevel.Value }
            yield c.Name, details ] |> dict }
    |> Async.StartAsTask 
  
  let getCountryXmlComment (c:Countries.Record) =
    let info = countryInfoLookup.Result.[c.Name]
    let region = 
      match googleChartsRegions.TryGetValue(c.Iso2Code) with
      | true, code -> code
      | _ -> "world"
    sprintf """
      [JAVASCRIPT]
      function(el) { showCountryDocumentation(el, 
      {regionCode:'%s', name:'%s', capital:'%s', income:'%s', population:'%s', region:'%s' }); }""" 
      region c.Name info.Capital info.IncomeLevel info.Population info.Region

  let getCountryMeta () = async {
    let! countries = getCountries None
    return [| for c in countries do 
                if c.Region.Value <> "Aggregates" then yield c |] }

  let getIndicatorMeta () = async {
    let! allIndicators = getIndicators()
    let indicators = allIndicators |> Array.filter (fun i -> sources.Contains i.Source.Value)
    let topics = 
      indicators 
      |> Seq.collect (fun ind -> [ for t in ind.Topics -> t.Value ]) 
      |> Seq.distinct
      |> Seq.choose (function Some(t) when t.Trim() <> "" -> Some(t) | _ -> None)
      |> Seq.map (fun topic ->
          let inds = indicators |> Seq.filter (fun i -> i.Topics |> Seq.exists (fun t -> t.Value = Some topic))
          topic.Trim(), Array.ofSeq inds )
    let topics = Seq.append ["All indicators", indicators] topics
    return { Topics = Array.ofSeq topics } }


// --------------------------------------------------------------------------------------------------------------------
//
// --------------------------------------------------------------------------------------------------------------------

[<TypeProvider>]
type public WorldBank(cfg:TypeProviderConfig) as this =
  inherit TypeProviderForNamespaces()

  do this.RegisterRuntimeAssemblyLocationAsProbingFolder(cfg)
  
  let asm = System.Reflection.Assembly.GetExecutingAssembly()
  let ns = "TheGamma"
  let wbType = ProvidedTypeDefinition(asm, ns, "world", Some(typeof<obj>))
  do wbType.AddXmlDoc("""
      <h2>World Bank</h2>
      <div style="text-align:center">
        <img src="/content/thegamma/worldbank.png" style="width:120px;margin:10px 0px 20px 0px" />
      </div>
      <p>The World Bank is an international financial institution that provides 
        loans to developing countries for capital programs.</p>
      <p>This type provides access to the World Bank Open Data, giving you 
        access to thousands indicators about countries all over the world.</p>
      <p>For more information see <a href="http://data.worldbank.org/">data.worldbank.org</a></p>
  """)

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

  do generate()

  // Register the main (parameterized) type with F# compiler
  do this.AddNamespace(ns, [ wbType ])

[<assembly:TypeProviderAssembly>]
do()