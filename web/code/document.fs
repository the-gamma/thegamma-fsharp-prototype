module TheGamma.Server.Document

// ------------------------------------------------------------------------------------------------
// Suave.io web server
// ------------------------------------------------------------------------------------------------

open System
open System.IO
open Suave
open Suave.Web
open Suave.Http
open Suave.Types
open FSharp.Markdown

let invalidChars = set(Path.GetInvalidFileNameChars())
let pageTemplatePath = Path.Combine(__SOURCE_DIRECTORY__, "..", "web", "page-template.html")
let editorTemplatePath = Path.Combine(__SOURCE_DIRECTORY__, "..", "web", "editor-template.html")
let docpartTemplatePath = Path.Combine(__SOURCE_DIRECTORY__, "..", "web", "docpart-template.html")

type DocumentChunk =
  | Code of string
  | Text of MarkdownParagraphs

let rec chunkByCodeBlocks acc pars = seq {
  match pars with
  | [] ->
      if acc <> [] then yield Text(List.rev acc)
  | CodeBlock(code, _, _) :: pars ->
      if acc <> [] then yield Text(List.rev acc)
      yield Code(code)
      yield! chunkByCodeBlocks [] pars
  | par :: pars ->
      yield! chunkByCodeBlocks (par::acc) pars }

let formatMarkdownSpan span (sb:Text.StringBuilder) =
  match span with
  | MarkdownSpan.Literal(s) -> sb.Append(s)
  | MarkdownSpan.InlineCode(s) -> sb.Append("`" + s + "`")
  | span -> sb.Append(sprintf "ERROR: %A" span)

let formatMarkdownSpans spans sb =
  spans |> Seq.iter (fun s -> formatMarkdownSpan s sb |> ignore); sb

let formatMarkdownPar par (sb:Text.StringBuilder) =
  match par with
  | MarkdownParagraph.Paragraph(spans) ->
      (formatMarkdownSpans spans sb).Append("\n\n")
  | MarkdownParagraph.Heading(n, body) ->
      if n = 1 || n = 2 then
        let lengthBefore = sb.Length
        (formatMarkdownSpans body sb).Append("\n")
          .Append(String.replicate (sb.Length - lengthBefore) (if n = 1 then "=" else "-")) |> ignore
      else
        sb.Append(String.replicate n "#").Append(" ")
        |> formatMarkdownSpans body |> ignore
      sb.Append("\n\n")
  | span -> sb.Append(sprintf "ERROR: %A" span)

let formatMarkdownPars pars sb =
  pars |> Seq.iter (fun s -> formatMarkdownPar s sb |> ignore); sb

let transformBlock (doc:MarkdownDocument) fsi counter block = async {
  incr counter
  let id = "output_" + (string counter.Value)

  match block with
  | Text pars ->
      let html = Markdown.WriteHtml(MarkdownDocument(pars, doc.DefinedLinks))
      let docpartTemplate = File.ReadAllText(docpartTemplatePath)
      let source = (formatMarkdownPars pars (Text.StringBuilder())).ToString()
      let encoded = System.Web.HttpUtility.JavaScriptStringEncode(source)
      return docpartTemplate.Replace("[ID]", id).Replace("[BODY]", html).Replace("[SOURCE]", encoded)

  | Code code ->
      let! js = Evaluator.evaluate fsi code
      match js with
      | Choice1Of2(js) ->
          let editorTemplate = File.ReadAllText(editorTemplatePath)
          let encoded = System.Web.HttpUtility.JavaScriptStringEncode(code)
          return editorTemplate.Replace("[ID]", id).Replace("[SCRIPT]", js).Replace("[SOURCE]", encoded)
      | Choice2Of2(err) -> return err.ToString() }

let transform fsi path = async {
  let pageTemplate = File.ReadAllText(pageTemplatePath)
  let doc = Markdown.Parse(File.ReadAllText(path))
  let blocks = doc.Paragraphs |> chunkByCodeBlocks [] |> List.ofSeq

  let! newPars = Common.asyncMap (transformBlock doc fsi (ref 0)) blocks
  return pageTemplate.Replace("[BODY]", String.concat "\n" newPars) }

let renderMarkdown md =
  Markdown.TransformHtml(md)

let renderDocument fsi ctx = async {
  let file = ctx.request.url.LocalPath
  if file.[0] <> '/' || (Seq.exists invalidChars.Contains file.[1 ..]) then return None else
  let path = Path.Combine(__SOURCE_DIRECTORY__, "..", "demos", file.Substring(1) + ".md")
  if File.Exists(path) then
    let! html = transform fsi path
    return! ctx |> Successful.OK(html)
  else return None }

open Suave.Http.Applicatives

let webPart fsi =
  choose
    [ renderDocument fsi
      path "/markdown" >>= Common.withRequestParams (fun (_, _, body) ->
        printfn "Transform markdown: %s" body
        Successful.OK(renderMarkdown body)) ]
