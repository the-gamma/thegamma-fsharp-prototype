#r @"C:\Programs\Development\Google\Google Data API SDK\Redist\Google.GData.Apps.dll"
#r @"C:\Programs\Development\Google\Google Data API SDK\Redist\Google.GData.Client.dll"
#r @"C:\Programs\Development\Google\Google Data API SDK\Redist\Google.GData.Extensions.dll"
#r @"C:\Programs\Development\Google\Google Data API SDK\Redist\Google.GData.Spreadsheets.dll"
open Google.GData.Client
open Google.GData.Spreadsheets


let service = new SpreadsheetsService("The Gamma")
let parameters = new OAuth2Parameters()
parameters.ClientId <- "403608197456-gour66g2r88iaslppf2rdhlb7tc56tmm.apps.googleusercontent.com"
parameters.ClientSecret <- "8Uqqv_v2FQLXW6AH_u16AI0x"
parameters.RedirectUri <- "http://localhost/oauth2callback" // "http://thegamma.net/oauth2callback"
parameters.Scope <- "https://spreadsheets.google.com/feeds"

let authorizationUrl = OAuthUtil.CreateOAuth2AuthorizationUrl(parameters)
System.Diagnostics.Process.Start authorizationUrl

parameters.AccessCode <- "4/9O9xN2ZW8bx5lIBD_LEM7TJXdF2iJcY-XfNdvJcq2nE"

////////////////////////////////////////////////////////////////////////////
// STEP 4: Get the Access Token
////////////////////////////////////////////////////////////////////////////

// Once the user authorizes with Google, the request token can be exchanged
// for a long-lived access token.  If you are building a browser-based
// application, you should parse the incoming request token from the url and
// set it in OAuthParameters before calling GetAccessToken().
OAuthUtil.GetAccessToken(parameters)
let accessToken = parameters.AccessToken

////////////////////////////////////////////////////////////////////////////
// STEP 5: Make an OAuth authorized request to Google
////////////////////////////////////////////////////////////////////////////

// Initialize the variables needed to make the request
let requestFactory =
    new GOAuth2RequestFactory(null, "The Gamma", parameters)
service.RequestFactory <- requestFactory

// Instantiate a SpreadsheetQuery object to retrieve spreadsheets.
let query = new SpreadsheetQuery("https://spreadsheets.google.com/feeds/spreadsheets/private/basic")

// Make a request to the API and get all spreadsheets.
let feed = service.Query(query)

// Iterate through all of the spreadsheets returned
for entry in feed.Entries do //|> Seq.truncate 1 do
  printfn "%s" entry.Title.Text
  let s = entry :?> SpreadsheetEntry
  printfn "%s" s.Id.AbsoluteUri
  
  //service.Query(WorksheetQuery("https://spreadsheets.google.com/feeds/spreadsheets/private/full/10Oh-IdEFbpDLP4w2rDoVcqSltvJzmlDtIN1t96D0DoE")).Entries |> Seq.length

  //s.Parse |> Seq.iter(fun c -> printfn " - %s" c.)
  try
    for w in (entry :?> SpreadsheetEntry).Worksheets.Entries do
      let ww = (w :?> WorksheetEntry)
      printfn " - %s (%d x %d)" w.Title.Text ww.Rows ww.Cols
      (*
      let cellFeed = service.Query(CellQuery(ww.CellFeedLink))
      for cell in cellFeed.Entries do
        let cell = cell :?> CellEntry
        printfn "%A" cell.Title.Text
        printfn "%A" (cell.Id.Uri.Content.Substring(cell.Id.Uri.Content.LastIndexOf("/") + 1))
        printfn "%A" cell.InputValue
        printfn "%A" cell.NumericValue
        printfn "%A" cell.Value
        printfn ""*)
  with e ->
    printfn " ???? "

