[<AutoOpen>]
module RadioBrowser.Services

open System
open System.Collections.Generic
open System.Net
open System.Net.NetworkInformation
open System.Diagnostics
open System.Runtime.InteropServices
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Options
open FSharp.Data

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
            if RuntimeInformation.IsOSPlatform(OSPlatform.Windows) then
                Windows
            elif RuntimeInformation.IsOSPlatform(OSPlatform.Linux) then
                Linux
            elif RuntimeInformation.IsOSPlatform(OSPlatform.OSX) then
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
                let hostEntry = Dns.GetHostEntry(searchUrl)

                if not (String.IsNullOrEmpty(hostEntry.HostName)) then
                    searchUrl <- hostEntry.HostName

                searchUrl
            with ex ->
                Debug.WriteLine(ex)
                logger.LogError(ex, "Error while getting api url")
                fallbackUrl // Return fallback URL on error

type StationsProvider =
    JsonProvider<
        Sample="../src/json/Stations.json",
        SampleIsList=true,
        RootName="Stations",
        Encoding="utf-8",
        EmbeddedResource="RadioBrowser, RadioBrowser.json.Stations.json"
     >

type CountriesProvider =
    JsonProvider<
        Sample="../src/json/Countries.json",
        SampleIsList=true,
        RootName="Countries",
        Encoding="utf-8",
        EmbeddedResource="RadioBrowser, RadioBrowser.json.Countries.json"
     >

type LanguagesProvider =
    JsonProvider<
        Sample="../src/json/Languages.json",
        SampleIsList=true,
        RootName="Languages",
        Encoding="utf-8",
        EmbeddedResource="RadioBrowser, RadioBrowser.json.Languages.json"
     >

type NameAndCountProvider =
    JsonProvider<
        Sample="../src/json/NameAndCount.json",
        SampleIsList=true,
        RootName="NameAndCount",  // |> Array.tryFind (fun x -> x.Key = id)
        // |> Option.map (fun x -> x.Value)
        // |> Option.defaultValue false
        Encoding="utf-8",
        EmbeddedResource="RadioBrowser, RadioBrowser.json.NameAndCount.json"
     >

type GetStationParameters(offset: int, limit: int, hidebroken: bool) =
    member this.Offset = offset
    member this.Limit = limit
    member this.Hidebroken = hidebroken

type SearchStationParameters
    (
        name: string option,
        nameExact: bool option,
        countryCode: string option,
        language: string option,
        tag: string option,
        tagExact: bool option,
        codec: string option
    ) =
    member this.Name = name
    member this.NameExact = nameExact
    member this.CountryCode = countryCode
    member this.Language = language
    member this.Tag = tag
    member this.tagExact = tagExact
    member this.Codec = codec

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
    abstract member Remove: Guid -> unit
    abstract member IsFavorites: Guid array -> Map<Guid, bool>

type FavoritesDataAccess() =
    let database (connectionString: string) =
        new LiteDB.LiteDatabase(connectionString)

    let favorites (db: LiteDB.LiteDatabase) = db.GetCollection<Station>("favorites")

    interface IFavoritesDataAccess with
        member this.Add(station) =
            use db = database AppSettings.DataBasePath

            if not (favorites(db).Exists(fun x -> x.Id = station.Id)) then
                station.IsFavorite <- true
                favorites(db).Insert(station) |> ignore

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
            favorites(db).Delete(LiteDB.BsonValue(id)) |> ignore

        member this.IsFavorites(ids: Guid array) =
            let exists (id: Guid, favorites: LiteDB.ILiteCollection<Station>) = favorites.Exists(fun x -> x.Id = id)
            use db = database AppSettings.DataBasePath
            let favorites = favorites (db)

            ids |> Array.map (fun id -> (id, exists (id, favorites))) |> Map.ofArray

type IHttpHandler =
    abstract member GetJsonStringAsync: url: string * parameters: list<string * string> -> Async<string>

type HttpHandler(apiUrlProvider: IApiUrlProvider, logger: ILogger<HttpHandler>) =
    interface IHttpHandler with
        member this.GetJsonStringAsync(url: string, parameters: (string * string) list) : Async<string> =
            async {
                let! result =
                    Http.AsyncRequestString(
                        url = $"https://{apiUrlProvider.GetUrl()}/json/{url}",
                        query = parameters,
                        httpMethod = "GET",
                        responseEncodingOverride = "utf-8"
                    )
                    |> Async.Catch

                return
                    match result with
                    | Choice1Of2 result -> result
                    | Choice2Of2 ex ->
                        Debug.WriteLine(ex)

                        let queryString =
                            parameters |> List.map (fun (k, v) -> $"{k}={v}") |> String.concat "&"

                        let message =
                            $"Error while getting stations. Url: https://{apiUrlProvider.GetUrl()}/json/{url}?{queryString}"

                        logger.LogError(ex, message)
                        "[]"
            }

type IStationsService =
    abstract member GetStationsByClicks: parameters: GetStationParameters -> Async<Station array>
    abstract member GetStationsByVotes: parameters: GetStationParameters -> Async<Station array>
    abstract member GetFavoriteStations: parameters: GetStationParameters -> Async<Station array>

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
          if searchParameters.Language.IsSome then
              "language", searchParameters.Language.Value
          if searchParameters.Tag.IsSome then
              "tag", searchParameters.Tag.Value
          if searchParameters.tagExact.IsSome then
              "tagExact", string searchParameters.tagExact.Value
          if searchParameters.Codec.IsSome then
              "codec", searchParameters.Codec.Value
          "offset", string parameters.Offset
          "limit", string parameters.Limit
          "hidebroken", string parameters.Hidebroken ]

    let getStations (json: string) =
        let response = StationsProvider.ParseList(json)

        let favorites =
            response |> Array.map (fun x -> x.Stationuuid) |> dataAccess.IsFavorites

        let isFavorite (id: Guid) =
            match favorites.ContainsKey id with
            | true -> favorites.[id]
            | false -> false

        response
        |> Array.map (fun x -> StationMapper.toStation (x, (isFavorite x.Stationuuid)))

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
            async { return dataAccess.GetFavorites(parameters) }

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
        member this.GetTags() = getNameAndCounts "tags"

        member this.GetCountries() =
            async {
                let! jsonString = handler.GetJsonStringAsync("countries", [])
                return CountriesProvider.ParseList(jsonString)
            }

        member this.GetLanguages() =
            async {
                let! jsonString = handler.GetJsonStringAsync("languages", [])
                return LanguagesProvider.ParseList(jsonString)

            }
