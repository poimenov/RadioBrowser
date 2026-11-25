[<AutoOpen>]
module RadioBrowser.LinkOpeningService

open System
open System.Diagnostics
open Microsoft.Extensions.Logging

type LinkOpeningService(logger: ILogger<LinkOpeningService>, processLauncher: IProcessLauncher) =
    interface ILinkOpeningService with
        member _.OpenUrl url =
            if String.IsNullOrWhiteSpace url || url <> url.Trim() then
                logger.LogWarning("Refusing to open an invalid or unsupported URL")
            else
                match Uri.TryCreate(url, UriKind.Absolute) with
                | true, uri when
                    (uri.Scheme = Uri.UriSchemeHttp || uri.Scheme = Uri.UriSchemeHttps)
                    && not (String.IsNullOrWhiteSpace uri.Host)
                    && String.IsNullOrEmpty uri.UserInfo
                    ->
                    try
                        let startInfo = ProcessStartInfo(FileName = uri.AbsoluteUri, UseShellExecute = true)
                        processLauncher.Start startInfo
                    with ex ->
                        Debug.WriteLine ex
                        logger.LogError(ex, "Error while opening url = {url}", url)
                | _ -> logger.LogWarning("Refusing to open an invalid or unsupported URL")
