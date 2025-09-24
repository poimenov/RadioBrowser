[<AutoOpen>]
module RadioBrowser.Services

open System
open System.Diagnostics
open System.Net
open System.Net.Http
open System.Net.NetworkInformation
open System.Linq
open System.Runtime.InteropServices
open System.Text
open System.Text.RegularExpressions
open System.Threading.Tasks
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Localization
open Microsoft.Extensions.Options
open Microsoft.FluentUI.AspNetCore.Components
open Microsoft.JSInterop
open FSharp.Data
open LiteDB

type Platform =
    | Windows
    | Linux
    | MacOS
    | Unknown

type IPlatformService =
    abstract member GetPlatform: unit -> Platform

type PlatformService() =
    interface IPlatformService with
        member _.GetPlatform() =
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
        member _.Run(command, arguments) =
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

type LinkOpeningService
    (platformService: IPlatformService, processService: IProcessService, logger: ILogger<LinkOpeningService>) =
    interface ILinkOpeningService with
        member _.OpenUrl url =
            try
                match platformService.GetPlatform() with
                | Windows -> processService.Run("cmd", $"/c start /b {url}")
                | Linux -> processService.Run("xdg-open", url)
                | MacOS -> processService.Run("open", url)
                | _ -> ()
            with ex ->
                Debug.WriteLine ex
                logger.LogError(ex, "Error while opening next url = {url}")

type IApiUrlProvider =
    abstract member GetUrl: unit -> string

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
                    let reply = (new Ping()).Send ipAddress

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

type StationsProvider =
    JsonProvider<Sample="../src/json/Stations.json", SampleIsList=true, RootName="Stations", Encoding="utf-8">

type CountriesProvider =
    JsonProvider<Sample="../src/json/Countries.json", SampleIsList=true, RootName="Countries", Encoding="utf-8">

type LanguagesProvider =
    JsonProvider<Sample="../src/json/Languages.json", SampleIsList=true, RootName="Languages", Encoding="utf-8">

type NameAndCountProvider =
    JsonProvider<Sample="../src/json/NameAndCount.json", SampleIsList=true, RootName="NameAndCount", Encoding="utf-8">

type GetStationParameters(offset: int, limit: int, hidebroken: bool) =
    member _.Offset = offset
    member _.Limit = limit
    member _.Hidebroken = hidebroken

type SearchStationParameters
    (name: string option, nameExact: bool option, countryCode: string option, tag: string option, tagExact: bool option)
    =
    member _.Name = name
    member _.NameExact = nameExact
    member _.CountryCode = countryCode
    member _.Tag = tag
    member _.tagExact = tagExact

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
    abstract member GetFavorites:
        name: string option * parameters: GetStationParameters -> Result<Station array, string>

    abstract member Exists: Guid -> bool
    abstract member Add: station: Station -> unit
    abstract member Update: stations: Station array -> Task<unit>
    abstract member Remove: Guid -> unit
    abstract member IsFavorites: Guid array -> Map<Guid, bool>
    abstract member FavoritesCount: unit -> int

type FavoritesDataAccess(logger: ILogger<FavoritesDataAccess>) =
    let mapper = BsonMapper.Global

    do mapper.Entity<Station>().Id(fun x -> x.Id) |> ignore

    let getFavorites (db: LiteDatabase) =
        let retVal = db.GetCollection<Station> "favorites"
        retVal.EnsureIndex((fun x -> x.Id), true) |> ignore
        retVal

    interface IFavoritesDataAccess with
        member _.Add(station: Station) =
            use db = new LiteDatabase(AppSettings.ConnectionString, mapper)
            let favorites = getFavorites db

            if not (favorites.Exists(fun x -> x.Id = station.Id)) then
                station.IsFavorite <- true
                favorites.Insert station |> ignore

        member _.Update(stations: Station array) : Task<unit> =
            async {
                use db = new LiteDatabase(AppSettings.ConnectionString, mapper)
                let favorites = getFavorites db

                stations
                |> Array.iter (fun station ->
                    if favorites.Exists(fun x -> x.Id = station.Id) then
                        station.IsFavorite <- true

                        if not (favorites.Update station) then
                            logger.LogError(
                                "Failed to update station {0} with id {1} in favorites.",
                                station.Name,
                                station.Id
                            ))
            }
            |> Async.StartAsTask

        member _.Exists(id: Guid) =
            use db = new LiteDatabase(AppSettings.ConnectionString, mapper)
            getFavorites(db).Exists(fun x -> x.Id = id)

        member _.GetFavorites(name: string option, parameters: GetStationParameters) =
            try
                use db = new LiteDatabase(AppSettings.ConnectionString, mapper)

                let retVal =
                    match name with
                    | None ->
                        getFavorites(db)
                            .FindAll()
                            .Skip(parameters.Offset)
                            .Take(parameters.Limit)
                            .ToArray()
                    | Some name when not (String.IsNullOrWhiteSpace name) ->
                        getFavorites(db)
                            .Find(
                                (fun x -> x.Name.ToLower().Contains(name.ToLower())),
                                parameters.Offset,
                                parameters.Limit
                            )
                            .ToArray()
                    | Some _ ->
                        getFavorites(db)
                            .FindAll()
                            .Skip(parameters.Offset)
                            .Take(parameters.Limit)
                            .ToArray()

                Ok retVal
            with ex ->
                Error ex.Message

        member _.Remove(id: Guid) =
            use db = new LiteDatabase(AppSettings.ConnectionString, mapper)

            if not (getFavorites(db).Delete(BsonValue id)) then
                logger.LogError("Failed to remove station with id {0} from favorites.", id)

        member _.IsFavorites(ids: Guid array) =
            use db = new LiteDatabase(AppSettings.ConnectionString, mapper)
            let favorites = getFavorites db
            let exists (id: Guid, fvs: ILiteCollection<Station>) = fvs.Exists(fun x -> x.Id = id)
            ids |> Array.map (fun id -> id, exists (id, favorites)) |> Map.ofArray

        member _.FavoritesCount() : int =
            use db = new LiteDatabase(AppSettings.ConnectionString, mapper)
            getFavorites(db).Count()

type IHttpHandler =
    abstract member GetJsonStringAsync: url: string * parameters: list<string * string> -> Async<Result<string, string>>

type HttpHandler(apiUrlProvider: IApiUrlProvider, logger: ILogger<HttpHandler>) =
    let writeError (ex: exn, url: string, parameters: (string * string) list) =
        let queryString =
            parameters |> List.map (fun (k, v) -> $"{k}={v}") |> String.concat "&"

        logger.LogError(
            ex,
            $"Error while getting responce. Url: https://{apiUrlProvider.GetUrl()}/json/{url}?{queryString}"
        )

    let rec fetchWithRetry (url: string, parameters: (string * string) list, retries: int, delay: int) =
        async {
            try
                let! retVal =
                    Http.AsyncRequestString(
                        url = $"https://{apiUrlProvider.GetUrl()}/json/{url}",
                        headers = [ "User-Agent", HttpHandler.UserAgent ],
                        query = parameters,
                        httpMethod = "GET",
                        responseEncodingOverride = "utf-8"
                    )

                return Ok retVal

            with
            | :? WebException as ex when ex.Status = WebExceptionStatus.ProtocolError ->
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

    static member UserAgent = "FSharp-RadioBrowser-App/1.0"

    interface IHttpHandler with
        member _.GetJsonStringAsync(url: string, parameters: (string * string) list) : Async<Result<string, string>> =
            fetchWithRetry (url, parameters, 3, 1000)

type IStationsService =
    abstract member GetStationsByClicks: parameters: GetStationParameters -> Async<Result<Station array, string>>
    abstract member GetStationsByVotes: parameters: GetStationParameters -> Async<Result<Station array, string>>

    abstract member GetFavoriteStations:
        name: string option * parameters: GetStationParameters -> Async<Result<Station array, string>>

    abstract member GetStations: Guid array -> Async<Result<Station array, string>>
    abstract member ClickStation: Guid -> unit
    abstract member VoteStation: Guid -> unit

    abstract member SearchStations:
        searchParameters: SearchStationParameters * parameters: GetStationParameters ->
            Async<Result<Station array, string>>

    abstract member Settings: AppSettings
    abstract member FavoritesDataAccess: IFavoritesDataAccess

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

    let getStations (result: Result<string, string>) =
        match result with
        | Error m -> Error m
        | Ok json ->
            let response = StationsProvider.ParseList json

            let favorites =
                response |> Array.map (fun x -> x.Stationuuid) |> dataAccess.IsFavorites

            let isFavorite (id: Guid) =
                match favorites.ContainsKey id with
                | true -> favorites.[id]
                | false -> false

            response
            |> Array.map (fun x -> StationMapper.toStation (x, isFavorite x.Stationuuid))
            |> Ok

    interface IStationsService with
        member _.GetStationsByClicks parameters =
            async {
                let! jsonString =
                    handler.GetJsonStringAsync($"{stations}/topclick/{parameters.Limit}", getQuery parameters)

                return getStations jsonString
            }

        member _.GetStationsByVotes parameters =
            async {
                let! jsonString =
                    handler.GetJsonStringAsync($"{stations}/topvote/{parameters.Limit}", getQuery parameters)

                return getStations jsonString
            }

        member _.SearchStations(searchParameters, parameters) =
            async {
                let! jsonString =
                    handler.GetJsonStringAsync($"{stations}/search", getSearchQuery (searchParameters, parameters))

                return getStations jsonString
            }

        member _.Settings: AppSettings = options.Value

        member _.GetFavoriteStations(name: string option, parameters: GetStationParameters) =
            async { return dataAccess.GetFavorites(name, parameters) }

        member _.GetStations(uuids: Guid array) : Async<Result<Station array, string>> =
            async {
                let query = [ "uuids", String.Join(",", uuids) ]

                let! jsonString = handler.GetJsonStringAsync($"{stations}/byuuid", query)

                return getStations jsonString
            }

        member _.FavoritesDataAccess: IFavoritesDataAccess = dataAccess

        member _.ClickStation(uuid: Guid) =
            handler.GetJsonStringAsync($"url/{uuid}", []) |> Async.Ignore |> Async.Start

        member _.VoteStation(uuid: Guid) =
            handler.GetJsonStringAsync($"vote/{uuid}", []) |> Async.Ignore |> Async.Start

type IListsService =
    abstract member GetCountries: unit -> Async<Result<CountriesProvider.Country array, string>>
    abstract member GetLanguages: unit -> Async<Result<LanguagesProvider.Language array, string>>
    abstract member GetCountryCodes: unit -> Async<Result<NameAndCountProvider.NameAndCount array, string>>
    abstract member GetCodecs: unit -> Async<Result<NameAndCountProvider.NameAndCount array, string>>
    abstract member GetTags: unit -> Async<Result<NameAndCountProvider.NameAndCount array, string>>

type ListsService(handler: IHttpHandler) =
    let getNameAndCounts (listName: string) =
        async {
            let! result = handler.GetJsonStringAsync(listName, [])

            match result with
            | Error m -> return Error m
            | Ok jsonString -> return Ok(NameAndCountProvider.ParseList jsonString)
        }

    interface IListsService with
        member _.GetCodecs() = getNameAndCounts "codecs"
        member _.GetCountryCodes() = getNameAndCounts "countrycodes"

        member _.GetTags() =
            async {
                let parameters =
                    [ "limit", "130"
                      "offset", "0"
                      "order", "stationcount"
                      "reverse", "true"
                      "hidebroken", "true" ]

                let! result = handler.GetJsonStringAsync("tags", parameters)

                match result with
                | Error m -> return Error m
                | Ok jsonString -> return Ok(NameAndCountProvider.ParseList jsonString)
            }

        member _.GetCountries() =
            async {
                let! result = handler.GetJsonStringAsync("countries", [])

                match result with
                | Error m -> return Error m
                | Ok jsonString -> return Ok(CountriesProvider.ParseList jsonString)
            }

        member _.GetLanguages() =
            async {
                let! result = handler.GetJsonStringAsync("languages", [])

                match result with
                | Error m -> return Error m
                | Ok jsonString -> return Ok(LanguagesProvider.ParseList jsonString)
            }

type SharedResources() = class end

type IMetadataService =
    abstract member GetTitleAsync: string -> Async<Result<string option, exn>>

type HistoryRecord =
    { StartTime: DateTime
      Title: string
      StationName: string }

type IHistoryDataAccess =
    abstract member GetHistory: unit -> Result<HistoryRecord list, string>
    abstract member Add: record: HistoryRecord -> unit

type IServices =
    abstract member ToastService: IToastService
    abstract member StationService: IStationsService
    abstract member FavoritesDataAccess: IFavoritesDataAccess
    abstract member HistoryDataAccess: IHistoryDataAccess
    abstract member LinkOpeningService: ILinkOpeningService
    abstract member Localizer: IStringLocalizer<SharedResources>
    abstract member MetadataService: IMetadataService
    abstract member JsRuntime: IJSRuntime

type Services
    (
        toastService: IToastService,
        stationService: IStationsService,
        linkOpeningService: ILinkOpeningService,
        localizer: IStringLocalizer<SharedResources>,
        metadataService: IMetadataService,
        jsRuntime: IJSRuntime,
        historyDataAccess: IHistoryDataAccess
    ) =
    interface IServices with
        member _.ToastService = toastService
        member _.FavoritesDataAccess: IFavoritesDataAccess = stationService.FavoritesDataAccess
        member _.StationService: IStationsService = stationService
        member _.LinkOpeningService: ILinkOpeningService = linkOpeningService
        member _.Localizer: IStringLocalizer<SharedResources> = localizer
        member _.MetadataService: IMetadataService = metadataService
        member _.JsRuntime: IJSRuntime = jsRuntime
        member _.HistoryDataAccess = historyDataAccess

type MetadataService(client: HttpClient, options: IOptions<AppSettings>, logger: ILogger<FavoritesDataAccess>) =

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
                            $"{mainTitle} - {t}"
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
                    if client.Timeout > TimeSpan.FromMilliseconds(float options.Value.GetTitleDelay) then
                        client.Timeout <- TimeSpan.FromMilliseconds(float options.Value.GetTitleDelay - 100.0)

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
                            return Ok None
                    else
                        return Ok None
                with ex ->
                    logger.LogError(ex, "Error while getting metadata from {0}", url)
                    return Error ex
            }

type HistoryDataAccess(options: IOptions<AppSettings>, logger: ILogger<FavoritesDataAccess>) =
    let mapper = BsonMapper.Global

    do mapper.Entity<HistoryRecord>().Id(fun x -> x.StartTime) |> ignore

    let getHistory (db: LiteDatabase) =
        let retVal = db.GetCollection<HistoryRecord> "history"
        retVal.EnsureIndex((fun x -> x.StartTime), true) |> ignore
        retVal

    interface IHistoryDataAccess with
        member _.Add(record: HistoryRecord) : unit =
            try
                use db = new LiteDatabase(AppSettings.ConnectionString, mapper)
                let history = getHistory db

                if not (history.Exists(fun x -> x.StartTime = record.StartTime)) then
                    history.Insert record |> ignore

                let totalCount = history.Count()

                if totalCount > options.Value.HistoryTruncateCount then
                    let oldestRecord =
                        history
                            .Query()
                            .OrderByDescending(fun x -> x.StartTime)
                            .Offset(99)
                            .Limit(1)
                            .ToList()
                        |> Seq.tryHead

                    match oldestRecord with
                    | Some record -> history.DeleteMany(fun x -> x.StartTime < record.StartTime) |> ignore
                    | None -> ()
            with ex ->
                logger.LogError(ex, "Error while adding history record")


        member _.GetHistory() : Result<HistoryRecord list, string> =
            try
                use db = new LiteDatabase(AppSettings.ConnectionString, mapper)
                let retVal = getHistory(db).FindAll().OrderBy(fun x -> x.StartTime) |> Seq.toList
                Ok retVal
            with ex ->
                logger.LogError(ex, "Error while getting history")
                Error ex.Message
