[<AutoOpen>]
module RadioBrowser.Interfaces

open System
open System.Threading.Tasks
open Microsoft.FluentUI.AspNetCore.Components
open Microsoft.Extensions.Localization
open Microsoft.JSInterop
open FSharp.Data

type Platform =
    | Windows
    | Linux
    | MacOS
    | Unknown

type IPlatformService =
    abstract member GetPlatform: unit -> Platform

type IProcessService =
    abstract member Run: command: string * arguments: string -> unit

type ILinkOpeningService =
    abstract member OpenUrl: url: string -> unit

type IApiUrlProvider =
    abstract member GetUrl: unit -> string

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

type IHttpHandler =
    abstract member GetJsonStringAsync: url: string * parameters: list<string * string> -> Async<Result<string, string>>

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

type IListsService =
    abstract member GetCountries: unit -> Async<Result<CountriesProvider.Country array, string>>
    abstract member GetLanguages: unit -> Async<Result<LanguagesProvider.Language array, string>>
    abstract member GetCountryCodes: unit -> Async<Result<NameAndCountProvider.NameAndCount array, string>>
    abstract member GetCodecs: unit -> Async<Result<NameAndCountProvider.NameAndCount array, string>>
    abstract member GetTags: unit -> Async<Result<NameAndCountProvider.NameAndCount array, string>>

type IMetadataService =
    abstract member GetTitleAsync: string -> Async<Result<string option, exn>>

type HistoryRecord =
    { StartTime: DateTime
      Title: string
      StationName: string }

type IHistoryDataAccess =
    abstract member GetHistory: unit -> Result<HistoryRecord list, string>
    abstract member Add: record: HistoryRecord -> unit

type SharedResources() = class end

type IServices =
    abstract member ToastService: IToastService
    abstract member StationsService: IStationsService
    abstract member FavoritesDataAccess: IFavoritesDataAccess
    abstract member HistoryDataAccess: IHistoryDataAccess
    abstract member LinkOpeningService: ILinkOpeningService
    abstract member Localizer: IStringLocalizer<SharedResources>
    abstract member MetadataService: IMetadataService
    abstract member JsRuntime: IJSRuntime
