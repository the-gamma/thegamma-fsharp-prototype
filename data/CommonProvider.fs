namespace ProviderImplementation.TheGamma

open System
open FSharp.Data

// --------------------------------------------------------------------------------------------------------------------
// 
// --------------------------------------------------------------------------------------------------------------------

[<AutoOpen>]
module ActivePatterns =

  /// Helper active pattern that can be used when constructing InvokeCode
  /// (to avoid writing pattern matching or incomplete matches).
  let (|Singleton|) = function [l] -> l | _ -> failwith "Parameter mismatch"

  /// Takes a map and succeeds if it is empty
  let (|EmptyMap|_|) result (map:Map<_,_>) = if map.IsEmpty then Some result else None
    
  /// Takes a map and succeeds if it contains exactly one value
  let (|SingletonMap|_|) (map:Map<_,_>) = 
      if map.Count <> 1 then None else
          let (KeyValue(k, v)) = Seq.head map
          Some(k, v)

// --------------------------------------------------------------------------------------------------------------------
//
// --------------------------------------------------------------------------------------------------------------------

open System.Diagnostics
open System.IO
open System.Security.Cryptography
open System.Text

module Cache = 

  /// Get hash code of a string - used to determine cache file
  let private hashString (plainText:string) = 
    let plainTextBytes = Encoding.UTF8.GetBytes(plainText)
    let hash = new SHA1Managed()
    let hashBytes = hash.ComputeHash(plainTextBytes)        
    let s = Convert.ToBase64String(hashBytes)
    s.Replace("ab","abab").Replace("\\","ab")

  // %UserProfile%\AppData\Local\Microsoft\Windows\INetCache
  let cacheFolder =
    if Environment.OSVersion.Platform = PlatformID.Unix
    then Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "/.cache/thegamma"
    else Environment.GetFolderPath(Environment.SpecialFolder.InternetCache)

  // Try to create directory, if it does not exist
  let downloadCache = Path.Combine(cacheFolder, "thegamma")
  if not (Directory.Exists downloadCache) then
    Directory.CreateDirectory downloadCache |> ignore

  // Get file name for a given string (using hash)
  let cacheFile key = 
    let sha1 = hashString key 
    let encoded = Uri.EscapeDataString sha1
    Path.Combine(downloadCache, encoded + ".txt")

  let private cache = System.Collections.Concurrent.ConcurrentDictionary<_, _>()

  let expiration = TimeSpan.FromDays(30.0)

  let saveFileCache url data = 
    let cacheFile = cacheFile url
    File.WriteAllText(cacheFile, data)

  let tryFileCache url =
    let cacheFile = cacheFile url
    if File.Exists cacheFile && File.GetLastWriteTimeUtc cacheFile - DateTime.UtcNow < expiration then
      let result = File.ReadAllText cacheFile
      if not (String.IsNullOrWhiteSpace(result)) then Some result else None
    else None

  let asyncDownload (url:string) = async {
    match cache.TryGetValue(url.ToString()) with
    | true, res -> return res
    | _ ->
        let! res = async {
          match tryFileCache url with 
          | Some res -> return res
          | None -> 
             let! res = Http.AsyncRequestString(url.ToString()) 
             saveFileCache url res
             return res }
        cache.TryAdd(url.ToString(), res) |> ignore
        return res }

