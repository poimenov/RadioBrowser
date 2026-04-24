[<AutoOpen>]
module RadioBrowser.MetadataService

open System
open System.Net
open System.Net.Http
open System.Text
open System.Text.RegularExpressions
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Options

type MetadataService
    (httpClientFactory: IHttpClientFactory, options: IOptions<AppSettings>, logger: ILogger<MetadataService>) =
    let extractStreamTitle (text: string) =
        let m = Regex.Match(text, "StreamTitle='([^']*)'")

        let trimLastDelimeter (s: string) =
            if s.EndsWith("-") then
                s.Substring(0, s.Length - 1).Trim()
            else
                s

        if not m.Success then
            Ok None
        else
            let raw = m.Groups.[1].Value.Trim()
            // We check if the string contains attributes of the type key="value"
            let attrRegex = Regex "(\\w+)=\"([^\"]*)\""
            let matches = attrRegex.Matches raw

            if matches.Count = 0 then
                // A common case Artist - Track
                let title = trimLastDelimeter raw

                if String.IsNullOrWhiteSpace title then
                    Ok None
                else
                    Ok(Some title)
            else
                // select the "left part" (artist/title up to the first key="value")
                let firstAttrIndex = raw.IndexOf(matches.[0].Value)

                let mainTitle =
                    if firstAttrIndex > 0 then
                        trimLastDelimeter (raw.Substring(0, firstAttrIndex).Trim())
                    else
                        ""

                let attrs =
                    matches
                    |> Seq.cast<Match>
                    |> Seq.map (fun m -> m.Groups.[1].Value, m.Groups.[2].Value)
                    |> Map.ofSeq

                let title =
                    if attrs.ContainsKey "text" then
                        let t = attrs.["text"]

                        if String.IsNullOrWhiteSpace mainTitle then
                            t
                        else
                            sprintf "%s - %s" mainTitle t
                    else
                        mainTitle

                if String.IsNullOrWhiteSpace title then
                    Ok None
                else
                    Ok(Some title)

    interface IMetadataService with
        member _.GetTitleAsync(url: string) =
            async {
                try
                    let client = httpClientFactory.CreateClient(radioBrowserHttpClientName)

                    if client.Timeout > TimeSpan.FromMilliseconds(float options.Value.GetTitleDelay) then
                        client.Timeout <- TimeSpan.FromMilliseconds(float options.Value.GetTitleDelay - 100.0)

                    use req = new HttpRequestMessage(Http.HttpMethod.Get, url)
                    // Request metadata explicitly
                    req.Headers.TryAddWithoutValidation("Icy-MetaData", "1") |> ignore

                    use! response =
                        client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead)
                        |> Async.AwaitTask

                    response.EnsureSuccessStatusCode() |> ignore

                    let tryGetHeader (name: string) =
                        match response.Headers.TryGetValues name with
                        | true, values -> Seq.tryHead values
                        | _ ->
                            match response.Content.Headers.TryGetValues name with
                            | true, values2 -> Seq.tryHead values2
                            | _ -> None

                    let metaInt =
                        match tryGetHeader "icy-metaint" with
                        | Some value ->
                            match Int32.TryParse value with
                            | true, v -> v
                            | _ -> 0
                        | None -> 0

                    if metaInt > 0 then
                        let buffer = Array.zeroCreate<byte> metaInt
                        use! stream = response.Content.ReadAsStreamAsync() |> Async.AwaitTask
                        let! _ = stream.ReadExactlyAsync(buffer, 0, buffer.Length).AsTask() |> Async.AwaitTask
                        let lenByte = stream.ReadByte()

                        if lenByte >= 0 then
                            let metaLength = lenByte * 16

                            if metaLength > 0 then
                                let metaBuffer = Array.zeroCreate<byte> metaLength
                                let! metaRead = stream.ReadAsync(metaBuffer, 0, metaLength) |> Async.AwaitTask

                                return
                                    Encoding.UTF8.GetString(metaBuffer, 0, metaRead).TrimEnd('\u0000')
                                    |> extractStreamTitle
                            else
                                return Ok None
                        else
                            return Ok None
                    else
                        return Ok None
                with
                | :? HttpRequestException as ex -> return Error ex
                | ex ->
                    logger.LogError(ex, "Error while getting metadata from {0}", url)
                    return Error ex
            }
