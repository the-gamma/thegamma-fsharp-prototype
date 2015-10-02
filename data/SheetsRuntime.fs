namespace ProviderImplementation.TheGamma.JS

open FunScript
open FunScript.TypeScript
(*
[<FunScript.JS>]
module WorldBank = 
  open Helpers

  type DataResponse = TheGamma.json<"http://api.worldbank.org/countries/CZE/indicators/AG.LND.FRST.ZS?date=1900:2050&format=json">
  type CountryResponse = TheGamma.json<"http://api.worldbank.org/country?per_page=100&format=json">

  type Indicator = { id : string; name : string }
  type Country = { id : string; name : string }
  type Year = { year : int }

  let getCountry id name = 
    { Country.id = id; name = name }

  let getYear year = 
    { Year.year = year }

  let worldBankUrl (functions: string list) (props: (string * string) list) = 
    "http://api.worldbank.org/" +
    (functions |> List.map (fun m -> "/" + encodeURIComponent(m)) |> String.concat "") +
    "?per_page=1000&format=jsonp" +
    (props |> List.map (fun (key, value) -> "&" + key + "=" + encodeURIComponent(value:string)) |> String.concat "")

  let worldBankDownload functions props = 
    Async.FromContinuations(fun (cont, econt, ccont) ->
      getJSONPrefix(worldBankUrl functions props, fun json ->
        cont json ))

  let worldBankDownloadAll functions props = async {
    let! json = worldBankDownload functions props        
    let first = DataResponse.wrap(json)
    let! alldata = 
      [ for p in 2 .. first.Record.pages -> async { 
          let! json = worldBankDownload functions (("page", string p)::props)
          let res = DataResponse.wrap(json)
          return res.Array } ]
      |> Async.PseudoParallel
    return Array.append [| first.Array |] alldata |> Array.concat }

  let getCountryIds () = async {
    let key = "thegamma_worldbank_country_ids"
    match tryGetGlobal key with 
    | Some v -> return getGlobal key
    | None -> 
        let! json = worldBankDownload ["country"] []
        let data = CountryResponse.wrap(json)
        let res = data.Array |> Array.filter (fun a -> a.region.id <> "NA") |> Array.map (fun a -> a.iso2Code) |> set
        setGlobal key res
        return res }

  let getByYear (year:Year) id : Async<(string*float)[]> = async {
    let! countries = getCountryIds()
    let! data = 
      worldBankDownloadAll
        [ "countries"; "indicators"; id ]
        [ "date", string year.year + ":" + string year.year ]  
    return data |> Array.choose (fun v ->
        if v.value.IsNone then None
        elif not (countries.Contains v.country.id) then None
        else Some(v.country.value, float v.value.Value)) }

  let getByCountry (country:Country) id : Async<(int * float)[]> = async {
    let! data = 
      worldBankDownloadAll 
        [ "countries"; country.id; "indicators"; id ] 
        [ "date", "1900:2050" ]
    return data |> Array.choose (fun v ->
      if v.value.IsNone then None
      else Some(v.date, float v.value.Value)) }
*)