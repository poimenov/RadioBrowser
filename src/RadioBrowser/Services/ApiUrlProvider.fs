[<AutoOpen>]
module RadioBrowser.ApiUrlProvider

open System
open System.Net
open System.Net.NetworkInformation
open System.Diagnostics
open Microsoft.Extensions.Logging

type ApiUrlProvider(logger: ILogger<ApiUrlProvider>) =
    let baseUrl = "all.api.radio-browser.info"
    let fallbackUrl = "de2.api.radio-browser.info"

    interface IApiUrlProvider with
        member _.GetUrl() =
            // Get fastest ip of dns
            try
                let ips = Dns.GetHostAddresses(baseUrl)
                let mutable lastRoundTripTime = Int64.MaxValue
                let mutable searchUrl = fallbackUrl // Fallback

                for ipAddress in ips do
                    use ping = new Ping()
                    let reply = ping.Send ipAddress
                    ping.Dispose()

                    if reply <> null && reply.RoundtripTime < lastRoundTripTime then
                        lastRoundTripTime <- reply.RoundtripTime
                        searchUrl <- ipAddress.ToString()

                // Get clean name
                let hostEntry = Dns.GetHostEntry searchUrl

                if not (String.IsNullOrEmpty hostEntry.HostName) then
                    searchUrl <- hostEntry.HostName

                searchUrl
            with ex ->
                Debug.WriteLine ex
                logger.LogError(ex, "Error while getting api url")
                fallbackUrl // Return fallback URL on error
