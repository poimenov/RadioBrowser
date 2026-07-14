[<AutoOpen>]
module RadioBrowser.HttpHandler

open System
open System.Diagnostics
open System.Net
open System.Net.Http
open Microsoft.Extensions.Logging

let radioBrowserHttpClientName = "FSharp-RadioBrowser-App/1.0"

type HttpHandler(httpClientFactory: IHttpClientFactory, apiUrlProvider: IApiUrlProvider, logger: ILogger<HttpHandler>) =
    let writeError (ex: exn, url: string, parameters: (string * string) list) =
        let queryString =
            parameters |> List.map (fun (k, v) -> sprintf "%s=%s" k v) |> String.concat "&"

        logger.LogError(
            ex,
            sprintf "Error while getting responce. Url: https://%s/json/%s?%s" (apiUrlProvider.GetUrl()) url queryString
        )

    let buildUri (url: string, parameters: (string * string) list) =
        let ub = UriBuilder(sprintf "https://%s/json/%s" (apiUrlProvider.GetUrl()) url)

        if parameters.Length > 0 then
            ub.Query <-
                parameters
                |> List.map (fun (k, v) -> sprintf "%s=%s" (WebUtility.UrlEncode k) (WebUtility.UrlEncode v))
                |> String.concat "&"

        ub.Uri

    let rec fetchWithRetry (url: string, parameters: (string * string) list, retries: int, delay: int) =
        async {
            try
                let client = httpClientFactory.CreateClient radioBrowserHttpClientName
                let uri = buildUri (url, parameters)
                let! retVal = client.GetStringAsync(uri) |> Async.AwaitTask
                return Ok retVal
            with
            | :? HttpRequestException as ex ->
                Debug.WriteLine ex

                if retries > 0 then
                    do! Async.Sleep delay
                    return! fetchWithRetry (url, parameters, retries - 1, delay * 2)
                else
                    writeError (ex, url, parameters)
                    return Error ex.Message
            | ex ->
                writeError (ex, url, parameters)
                return Error ex.Message
        }

    interface IHttpHandler with
        member _.GetJsonStringAsync(url: string, parameters: (string * string) list) : Async<Result<string, string>> =
            fetchWithRetry (url, parameters, 3, 1000)
