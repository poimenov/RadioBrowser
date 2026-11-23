[<AutoOpen>]
module RadioBrowser.LinkOpeningService

open System
open System.Diagnostics
open Microsoft.Extensions.Logging

type LinkOpeningService
    (logger: ILogger<LinkOpeningService>) =
    interface ILinkOpeningService with
        member _.OpenUrl url =
            match Uri.TryCreate(url, UriKind.Absolute) with
            | true, uri
                when String.Equals(url, url.Trim(), StringComparison.Ordinal)
                     && uri.IsWellFormedOriginalString()
                     && (uri.Scheme = Uri.UriSchemeHttp || uri.Scheme = Uri.UriSchemeHttps)
                     && not (String.IsNullOrWhiteSpace uri.Host)
                     && String.IsNullOrEmpty uri.UserInfo ->
                try
                    let startInfo = ProcessStartInfo(FileName = uri.AbsoluteUri, UseShellExecute = true)
                    use browserProcess = Process.Start startInfo
                    browserProcess |> ignore
                    ()
                with ex ->
                    Debug.WriteLine ex
                    logger.LogError(ex, "Error while opening url = {url}", uri.AbsoluteUri)
            | _ ->
                logger.LogWarning("Refusing to open an invalid or unsupported URL")
