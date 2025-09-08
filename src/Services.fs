[<AutoOpen>]
module RadioBrowser.Services

open System
open System.Diagnostics
open System.Net
open System.Net.NetworkInformation
open System.Runtime.InteropServices
open System.Threading.Tasks
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Localization
open Microsoft.Extensions.Options
open Microsoft.FluentUI.AspNetCore.Components
open Microsoft.JSInterop
open FSharp.Data
open System.Net.Http
open System.Text

type Platform =
    | Windows
    | Linux
    | MacOS
    | Unknown

type IPlatformService =
    abstract member GetPlatform: unit -> Platform

type PlatformService() =
    interface IPlatformService with
        member this.GetPlatform() =
            if RuntimeInformation.IsOSPlatform OSPlatform.Windows then
                Windows
            elif RuntimeInformation.IsOSPlatform OSPlatform.Linux then
                Linux
            elif RuntimeInformation.IsOSPlatform OSPlatform.OSX then
                MacOS
            else
                Unknown

type IProcessService =
    abstract member Run: command: string * arguments: string -> unit

type ProcessService() =
    interface IProcessService with
        member this.Run(command, arguments) =
            let psi = new ProcessStartInfo(command)
            psi.RedirectStandardOutput <- false
            psi.UseShellExecute <- false
            psi.CreateNoWindow <- false
            psi.Arguments <- arguments

            let p = new Process()
            p.StartInfo <- psi
            p.Start() |> ignore

type ILinkOpeningService =
    abstract member OpenUrl: url: string -> unit

type LinkOpeningService(platformService: IPlatformService, processService: IProcessService) =
    interface ILinkOpeningService with
        member this.OpenUrl(url) =
            match platformService.GetPlatform() with
            | Windows -> processService.Run("cmd", $"/c start {url}")
            | Linux -> processService.Run("xdg-open", url)
            | MacOS -> processService.Run("open", url)
            | _ -> ()

type IApiUrlProvider =
    abstract member GetUrl: unit -> string

type ApiUrlProvider(logger: ILogger<ApiUrlProvider>) =
    let baseUrl = "all.api.radio-browser.info"
    let fallbackUrl = "de2.api.radio-browser.info"

    interface IApiUrlProvider with
        member this.GetUrl() =
            // Get fastest ip of dns
            try
                let ips = Dns.GetHostAddresses(baseUrl)
                let mutable lastRoundTripTime = Int64.MaxValue
                let mutable searchUrl = fallbackUrl // Fallback

                for ipAddress in ips do
                    let reply = (new Ping()).Send(ipAddress)

                    if reply <> null && reply.RoundtripTime < lastRoundTripTime then
                        lastRoundTripTime <- reply.RoundtripTime
                        searchUrl <- ipAddress.ToString()

                // Get clean name
                let hostEntry = Dns.GetHostEntry searchUrl

                if not (String.IsNullOrEmpty hostEntry.HostName) then
                    searchUrl <- hostEntry.HostName

                searchUrl
            with ex ->
                Debug.WriteLine(ex)
                logger.LogError(ex, "Error while getting api url")
                fallbackUrl // Return fallback URL on error

type StationsProvider =
    JsonProvider<Sample="../src/json/Stations.json", SampleIsList=true, RootName="Stations", Encoding="utf-8">

type CountriesProvider =
    JsonProvider<Sample="../src/json/Countries.json", SampleIsList=true, RootName="Countries", Encoding="utf-8">

type LanguagesProvider =
    JsonProvider<Sample="../src/json/Languages.json", SampleIsList=true, RootName="Languages", Encoding="utf-8">

type NameAndCountProvider =
    JsonProvider<Sample="../src/json/NameAndCount.json", SampleIsList=true, RootName="NameAndCount", Encoding="utf-8">

type GetStationParameters(offset: int, limit: int, hidebroken: bool) =
    member this.Offset = offset
    member this.Limit = limit
    member this.Hidebroken = hidebroken

type SearchStationParameters
    (name: string option, nameExact: bool option, countryCode: string option, tag: string option, tagExact: bool option)
    =
    member this.Name = name
    member this.NameExact = nameExact
    member this.CountryCode = countryCode
    member this.Tag = tag
    member this.tagExact = tagExact

[<CLIMutable>]
type Station =
    { Id: Guid
      Name: string
      Url: string
      UrlResolved: string
      Homepage: string
      Favicon: string
      Tags: string
      Country: string
      CountryCode: string
      Language: string
      Codec: string
      Bitrate: int
      mutable IsFavorite: bool }

type StationMapper =
    static member toStation(station: StationsProvider.Station, isFavorite: bool) : Station =
        { Id = station.Stationuuid
          Name = station.Name
          Url = station.Url
          UrlResolved = station.UrlResolved
          Homepage = station.Homepage
          Favicon = station.Favicon
          Tags = station.Tags
          Country = station.Country
          CountryCode = station.Countrycode
          Language = station.Language
          Codec = station.Codec
          Bitrate = station.Bitrate
          IsFavorite = isFavorite }

type IFavoritesDataAccess =
    abstract member GetFavorites: parameters: GetStationParameters -> Station array
    abstract member Exists: Guid -> bool
    abstract member Add: station: Station -> unit
    abstract member Update: stations: Station array -> Task<unit>
    abstract member Remove: Guid -> unit
    abstract member IsFavorites: Guid array -> Map<Guid, bool>
    abstract member FavoritesCount: unit -> int

type FavoritesDataAccess(logger: ILogger<FavoritesDataAccess>) =
    let database (connectionString: string) =
        new LiteDB.LiteDatabase(connectionString)

    let favorites (db: LiteDB.LiteDatabase) = db.GetCollection<Station> "favorites"

    interface IFavoritesDataAccess with
        member this.Add(station: Station) =
            use db = database AppSettings.DataBasePath

            if not (favorites(db).Exists(fun x -> x.Id = station.Id)) then
                station.IsFavorite <- true
                favorites(db).Insert station |> ignore


        member this.Update(stations: Station array) : Task<unit> =
            async {
                use db = database AppSettings.DataBasePath

                stations
                |> Array.iter (fun station ->
                    if favorites(db).Exists(fun x -> x.Id = station.Id) then
                        station.IsFavorite <- true

                        if not (favorites(db).Update station) then
                            logger.LogError(
                                "Failed to update station {0} with id {1} in favorites.",
                                station.Name,
                                station.Id
                            ))
            }
            |> Async.StartAsTask


        member this.Exists(id: Guid) =
            use db = database AppSettings.DataBasePath
            favorites(db).Exists(fun x -> x.Id = id)

        member this.GetFavorites(parameters: GetStationParameters) =
            use db = database AppSettings.DataBasePath

            favorites(db)
                .Query()
                .Select(fun x -> x)
                .Limit(parameters.Limit)
                .Offset(parameters.Offset)
                .ToArray()

        member this.Remove(id: Guid) =
            use db = database AppSettings.DataBasePath

            if not (favorites(db).Delete(LiteDB.BsonValue id)) then
                logger.LogError("Failed to remove station with id {0} from favorites.", id)

        member this.IsFavorites(ids: Guid array) =
            let exists (id: Guid, favorites: LiteDB.ILiteCollection<Station>) = favorites.Exists(fun x -> x.Id = id)
            use db = database AppSettings.DataBasePath
            let favorites = favorites (db)

            ids |> Array.map (fun id -> id, exists (id, favorites)) |> Map.ofArray

        member this.FavoritesCount() : int =
            use db = database AppSettings.DataBasePath
            favorites(db).Count()

type IHttpHandler =
    abstract member GetJsonStringAsync: url: string * parameters: list<string * string> -> Async<string>

type HttpHandler(apiUrlProvider: IApiUrlProvider, logger: ILogger<HttpHandler>) =
    let writeErrorAndReturnEmptyArray (ex: exn, url: string, parameters: (string * string) list) =
        let queryString =
            parameters |> List.map (fun (k, v) -> $"{k}={v}") |> String.concat "&"

        logger.LogError(
            ex,
            $"Error while getting responce. Url: https://{apiUrlProvider.GetUrl()}/json/{url}?{queryString}"
        )

        "[]"

    let rec fetchWithRetry (url: string, parameters: (string * string) list, retries: int, delay: int) : Async<string> =
        async {
            try
                return!
                    Http.AsyncRequestString(
                        url = $"https://{apiUrlProvider.GetUrl()}/json/{url}",
                        query = parameters,
                        httpMethod = "GET",
                        responseEncodingOverride = "utf-8"
                    )

            with
            | :? WebException as ex when ex.Status = WebExceptionStatus.ProtocolError ->
                Debug.WriteLine(ex)

                if retries > 0 then
                    do! Async.Sleep delay
                    return! fetchWithRetry (url, parameters, retries - 1, delay * 2)
                else
                    return writeErrorAndReturnEmptyArray (ex, url, parameters)
            | ex -> return writeErrorAndReturnEmptyArray (ex, url, parameters)
        }

    interface IHttpHandler with
        member this.GetJsonStringAsync(url: string, parameters: (string * string) list) : Async<string> =
            fetchWithRetry (url, parameters, 3, 1000)

type IStationsService =
    abstract member GetStationsByClicks: parameters: GetStationParameters -> Async<Station array>
    abstract member GetStationsByVotes: parameters: GetStationParameters -> Async<Station array>
    abstract member GetFavoriteStations: parameters: GetStationParameters -> Async<Station array>
    abstract member GetStations: Guid array -> Async<Station array>

    abstract member SearchStations:
        searchParameters: SearchStationParameters * parameters: GetStationParameters -> Async<Station array>

    abstract member Settings: AppSettings

type StationsService(handler: IHttpHandler, dataAccess: IFavoritesDataAccess, options: IOptions<AppSettings>) =
    let stations = "stations"

    let getQuery (parameters: GetStationParameters) =
        [ "offset", string parameters.Offset
          "hidebroken", string parameters.Hidebroken ]

    let getSearchQuery (searchParameters: SearchStationParameters, parameters: GetStationParameters) =
        [ if searchParameters.Name.IsSome then
              "name", searchParameters.Name.Value
          if searchParameters.NameExact.IsSome then
              "nameExact", string searchParameters.NameExact.Value
          if searchParameters.CountryCode.IsSome then
              "countrycode", searchParameters.CountryCode.Value
          if searchParameters.Tag.IsSome then
              "tag", searchParameters.Tag.Value
          if searchParameters.tagExact.IsSome then
              "tagExact", string searchParameters.tagExact.Value
          if not (String.IsNullOrWhiteSpace options.Value.Codec) then
              "codec", options.Value.Codec
          if not (String.IsNullOrWhiteSpace options.Value.Language) then
              "language", options.Value.Language
          "offset", string parameters.Offset
          "limit", string parameters.Limit
          "order", options.Value.DefaultOrder
          "reverse", string options.Value.ReverseOrder
          "hidebroken", string parameters.Hidebroken ]

    let getStations (json: string) =
        let response = StationsProvider.ParseList json

        let favorites =
            response |> Array.map (fun x -> x.Stationuuid) |> dataAccess.IsFavorites

        let isFavorite (id: Guid) =
            match favorites.ContainsKey id with
            | true -> favorites.[id]
            | false -> false

        response
        |> Array.map (fun x -> StationMapper.toStation (x, isFavorite x.Stationuuid))

    interface IStationsService with
        member this.GetStationsByClicks(parameters) =
            async {
                let! jsonString =
                    handler.GetJsonStringAsync($"{stations}/topclick/{parameters.Limit}", getQuery parameters)

                return getStations jsonString
            }

        member this.GetStationsByVotes(parameters) =
            async {
                let! jsonString =
                    handler.GetJsonStringAsync($"{stations}/topvote/{parameters.Limit}", getQuery parameters)

                return getStations jsonString
            }

        member this.SearchStations(searchParameters, parameters) =
            async {
                let! jsonString =
                    handler.GetJsonStringAsync($"{stations}/search", getSearchQuery (searchParameters, parameters))

                return getStations jsonString
            }

        member this.Settings: AppSettings = options.Value

        member this.GetFavoriteStations(parameters: GetStationParameters) =
            async { return dataAccess.GetFavorites parameters }

        member this.GetStations(uuids: Guid array) : Async<Station array> =
            async {
                let query = [ "uuids", String.Join(",", uuids) ]

                let! jsonString = handler.GetJsonStringAsync($"{stations}/byuuid", query)

                return getStations jsonString
            }

type IListsService =
    abstract member GetCountries: unit -> Async<CountriesProvider.Country array>
    abstract member GetLanguages: unit -> Async<LanguagesProvider.Language array>
    abstract member GetCountryCodes: unit -> Async<NameAndCountProvider.NameAndCount array>
    abstract member GetCodecs: unit -> Async<NameAndCountProvider.NameAndCount array>
    abstract member GetTags: unit -> Async<NameAndCountProvider.NameAndCount array>

type ListsService(handler: IHttpHandler) =
    let getNameAndCounts (listName: string) =
        async {
            let! jsonString = handler.GetJsonStringAsync(listName, [])
            return NameAndCountProvider.ParseList(jsonString)
        }

    interface IListsService with
        member this.GetCodecs() = getNameAndCounts "codecs"
        member this.GetCountryCodes() = getNameAndCounts "countrycodes"

        member this.GetTags() =
            async {
                let parameters =
                    [ "limit", "130"
                      "offset", "0"
                      "order", "stationcount"
                      "reverse", "true"
                      "hidebroken", "true" ]

                let! jsonString = handler.GetJsonStringAsync("tags", parameters)
                return NameAndCountProvider.ParseList jsonString
            }

        member this.GetCountries() =
            async {
                let! jsonString = handler.GetJsonStringAsync("countries", [])
                return CountriesProvider.ParseList jsonString
            }

        member this.GetLanguages() =
            async {
                let! jsonString = handler.GetJsonStringAsync("languages", [])
                return LanguagesProvider.ParseList jsonString

            }

type SharedResources() = class end

type IMetadataService =
    abstract member GetTitleAsync: string -> Async<string option>

type IServices =
    abstract member ToastService: IToastService
    abstract member DataAccess: IFavoritesDataAccess
    abstract member LinkOpeningService: ILinkOpeningService
    abstract member Localizer: IStringLocalizer<SharedResources>
    abstract member MetadataService: IMetadataService
    abstract member JsRuntime: IJSRuntime

type Services
    (
        toastService: IToastService,
        dataAccess: IFavoritesDataAccess,
        linkOpeningService: ILinkOpeningService,
        localizer: IStringLocalizer<SharedResources>,
        metadataService: IMetadataService,
        jsRuntime: IJSRuntime
    ) =
    interface IServices with
        member this.ToastService = toastService
        member this.DataAccess: IFavoritesDataAccess = dataAccess
        member this.LinkOpeningService: ILinkOpeningService = linkOpeningService
        member this.Localizer: IStringLocalizer<SharedResources> = localizer
        member this.MetadataService: IMetadataService = metadataService
        member this.JsRuntime: IJSRuntime = jsRuntime


type MetadataService(client: HttpClient) =

    let extractStreamTitle (text: string) =
        let prefix = "StreamTitle='"

        if text.Contains prefix then
            let start = text.IndexOf prefix + prefix.Length
            let endIdx = text.IndexOf("';", start)

            if endIdx > start then
                Some(text.Substring(start, endIdx - start))
            else
                None
        else
            None

    interface IMetadataService with
        member this.GetTitleAsync(url: string) =
            async {
                use! resp =
                    client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead)
                    |> Async.AwaitTask

                resp.EnsureSuccessStatusCode() |> ignore

                let metaInt =
                    match resp.Headers.TryGetValues "icy-metaint" with
                    | true, values ->
                        let value = Seq.head values

                        match Int32.TryParse value with
                        | true, v -> v
                        | _ -> 0
                    | _ -> 0

                if metaInt > 0 then
                    let buffer = Array.zeroCreate<byte> metaInt
                    use! stream = resp.Content.ReadAsStreamAsync() |> Async.AwaitTask
                    let! _ = stream.ReadExactlyAsync(buffer, 0, buffer.Length).AsTask() |> Async.AwaitTask
                    let lenByte = stream.ReadByte()
                    let metaLength = lenByte * 16

                    if metaLength > 0 then
                        let metaBuffer = Array.zeroCreate<byte> metaLength
                        let! metaRead = stream.ReadAsync(metaBuffer, 0, metaLength) |> Async.AwaitTask

                        return
                            Encoding.UTF8.GetString(metaBuffer, 0, metaRead).TrimEnd('\u0000')
                            |> extractStreamTitle
                    else
                        return None
                else
                    return None
            }
