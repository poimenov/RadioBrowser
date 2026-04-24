[<AutoOpen>]
module RadioBrowser.LinkOpeningService

open System.Diagnostics
open Microsoft.Extensions.Logging

type LinkOpeningService
    (platformService: IPlatformService, processService: IProcessService, logger: ILogger<LinkOpeningService>) =
    interface ILinkOpeningService with
        member _.OpenUrl url =
            try
                match platformService.GetPlatform() with
                | Windows -> processService.Run("cmd", $"/c start \"\" \"{url}\"")
                | Linux -> processService.Run("xdg-open", url)
                | MacOS -> processService.Run("open", url)
                | _ -> ()
            with ex ->
                Debug.WriteLine ex
                logger.LogError(ex, "Error while opening next url = {url}")
