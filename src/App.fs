[<AutoOpen>]
module RadioBrowser.App

open System
open System.Collections.Generic
open System.Globalization
open System.Linq
open Microsoft.AspNetCore.Components
open Microsoft.AspNetCore.Components.Web
open Microsoft.AspNetCore.Components.Routing
open Microsoft.Extensions.Localization
open Microsoft.Extensions.Options
open Microsoft.FluentUI.AspNetCore.Components
open Microsoft.JSInterop
open Fun.Blazor
open Fun.Blazor.Router
open FSharp.Data.Adaptive
open System.Threading.Tasks

type HistoryRecord =
    { StartTime: DateTime
      Title: string
      StationName: string }

type Github() =
    inherit
        Icon(
            "GitHub",
            IconVariant.Regular,
            IconSize.Size20,
            @"<path fill-rule=""evenodd"" clip-rule=""evenodd"" d=""M10.178 0C4.55 0 0 4.583 0 10.254c0 4.533 2.915 8.369 6.959 9.727 0.506 0.102 0.691 -0.221 0.691 -0.492 0 -0.238 -0.017 -1.053 -0.017 -1.901 -2.831 0.611 -3.421 -1.222 -3.421 -1.222 -0.455 -1.188 -1.129 -1.494 -1.129 -1.494 -0.927 -0.628 0.068 -0.628 0.068 -0.628 1.028 0.068 1.567 1.053 1.567 1.053 0.91 1.562 2.376 1.12 2.966 0.849 0.084 -0.662 0.354 -1.12 0.64 -1.375 -2.258 -0.238 -4.634 -1.12 -4.634 -5.059 0 -1.12 0.404 -2.037 1.045 -2.75 -0.101 -0.255 -0.455 -1.307 0.101 -2.716 0 0 0.859 -0.272 2.797 1.053a9.786 9.786 0 0 1 2.545 -0.34c0.859 0 1.735 0.119 2.544 0.34 1.938 -1.324 2.797 -1.053 2.797 -1.053 0.556 1.409 0.202 2.462 0.101 2.716 0.657 0.713 1.045 1.63 1.045 2.75 0 3.939 -2.376 4.804 -4.651 5.059 0.371 0.323 0.691 0.934 0.691 1.901 0 1.375 -0.017 2.479 -0.017 2.818 0 0.272 0.185 0.594 0.691 0.493 4.044 -1.358 6.959 -5.195 6.959 -9.727C20.356 4.583 15.789 0 10.178 0z""/>"
        )

type SelectedStation =
    | NotSelected
    | Selected of Station

type CurrentSearchMode =
    | Search of SearchStationParameters
    | Favorites
    | ByVotes
    | ByClicks
    | History

type IShareStore with
    member store.IsMenuOpen = store.CreateCVal(nameof store.IsMenuOpen, true)
    member store.Theme = store.CreateCVal(nameof store.Theme, DesignThemeModes.Light)

    member store.Stations =
        store.CreateCVal(nameof store.Stations, Enumerable.Empty<Station>())

    member store.SelectedStation =
        store.CreateCVal(nameof store.SelectedStation, NotSelected)

    member store.SelectedStationIsFavorite =
        store.CreateCVal(nameof store.SelectedStationIsFavorite, false)

    member store.Countries =
        store.CreateCVal(nameof store.Countries, Array.Empty<CountriesProvider.Country>())

    member store.Tags =
        store.CreateCVal(nameof store.Tags, Array.Empty<NameAndCountProvider.NameAndCount>())

    member store.Volume = store.CreateCVal(nameof store.Volume, 0.5)

    member store.SearchMode = store.CreateCVal(nameof store.SearchMode, ByVotes)

    member store.HeaderTitle = store.CreateCVal(nameof store.HeaderTitle, "")
    member store.IsPlaying = store.CreateCVal(nameof store.IsPlaying, false)
    member store.Title = store.CreateCVal<string option>(nameof store.Title, None)
    member store.GetTitleDelay = store.CreateCVal(nameof store.GetTitleDelay, 5000)

    member store.History = store.CreateCVal<HistoryRecord list>(nameof store.History, [])

    member store.HistoryTruncateCount =
        store.CreateCVal(nameof store.HistoryTruncateCount, 100)

let getParameters (offset: int, settings: AppSettings) =
    GetStationParameters(offset, settings.LimitCount, settings.HideBroken)

type AppCallbacks(store: IShareStore, stationsService: IStationsService, window: Photino.NET.PhotinoWindow) =
    [<JSInvokable>]
    member _.OnWindowResize(width: int, height: int) =
        task {
            store.IsMenuOpen.Publish(width > 800)

            let w = if width < 600 then 600 else width
            let h = if height < 400 then 400 else height

            if width < 700 || height < 500 then
                window.SetSize(w, h) |> ignore
        }

    [<JSInvokable>]
    member _.OnElementVisible() =
        task {
            let parameters =
                getParameters (store.Stations.Value.Count(), stationsService.Settings)

            let! newStations =
                match store.SearchMode.Value with
                | ByVotes -> stationsService.GetStationsByVotes parameters
                | ByClicks -> stationsService.GetStationsByClicks parameters
                | Favorites -> stationsService.GetFavoriteStations parameters
                | Search ssp -> stationsService.SearchStations(ssp, parameters)
                | _ -> Task.FromResult(Enumerable.Empty<Station>().ToArray()) |> Async.AwaitTask

            if newStations.Count() > 0 then
                let stations = store.Stations.Value.Concat newStations
                store.Stations.Publish stations
        }

let watchElementVisibleComponent =
    html.inject
        (fun
            (store: IShareStore,
             jsRuntime: IJSRuntime,
             hook: IComponentHook,
             stationsService: IStationsService,
             window: Photino.NET.PhotinoWindow) ->
            let elementId = "watched-div"

            hook.AddFirstAfterRenderTask(fun _ ->
                task {
                    let callback = AppCallbacks(store, stationsService, window)
                    let dotNetRef = DotNetObjectReference.Create callback
                    jsRuntime.InvokeVoidAsync("setCallbacks", elementId, dotNetRef) |> ignore
                })

            div { id elementId })

let stationIcon (iconUrl: string) =
    let defaultImageSrc = "./images/radio.svg"

    let imgSrc =
        if String.IsNullOrEmpty iconUrl then
            defaultImageSrc
        else
            iconUrl

    img {
        src imgSrc
        class' "favicon"
        loadingExperimental true
        onerror $"this.src = '{defaultImageSrc}';"
    }

let stationItem (station: Station, isFavorite: bool) =
    div {
        class' "station-item"
        stationIcon station.Favicon

        div {
            div {
                if not (String.IsNullOrEmpty station.CountryCode) then
                    img {
                        class' "flag-icon"
                        title' station.Country
                        src $"./images/flags/{station.CountryCode.ToLower()}.svg"
                        loadingExperimental true
                    }

                span {
                    class' "station-name"
                    title' station.Name
                    station.Name
                }
            }

            FluentStack'' {
                Orientation Orientation.Horizontal
                VerticalAlignment VerticalAlignment.Center
                HorizontalGap 4

                FluentIcon'' {
                    Value(
                        if isFavorite then
                            Icons.Filled.Size16.Heart() :> Icon
                        else
                            Icons.Regular.Size16.Heart() :> Icon
                    )
                }

                span { $"{station.Codec} {station.Bitrate} kbps " }
                span { station.Language }
            }

            div { station.Tags }
        }
    }

let stationsList (store: IShareStore, localizer: IStringLocalizer<SharedResources>) =
    div {
        style' "padding:10px;"

        adapt {

            let! stations = store.Stations
            let! selectedStation = store.SelectedStation
            let! selectedStationIsFavorite = store.SelectedStationIsFavorite

            let isCurrent (curr: Station) =
                match selectedStation with
                | NotSelected -> false
                | Selected item -> curr.Id = item.Id

            let getSelectedClass (curr: Station) =
                if isCurrent curr then
                    "station selected-station"
                else
                    "station"

            let isFavorite (curr: Station) =
                match selectedStation with
                | NotSelected -> curr.IsFavorite
                | Selected item ->
                    if curr.Id = item.Id then
                        selectedStationIsFavorite
                    else
                        curr.IsFavorite

            if stations.Any() then
                div {
                    class' "stations-list"

                    for station in stations.ToArray() do
                        div {
                            class' (getSelectedClass station)

                            onclick (fun _ ->
                                let selected = Selected station
                                store.SelectedStationIsFavorite.Publish station.IsFavorite
                                store.IsPlaying.Publish false
                                store.SelectedStation.Publish selected)

                            stationItem (station, isFavorite station)
                        }

                    watchElementVisibleComponent
                }
            else
                div {
                    style' "text-align:center;"

                    localizer["IsLoading"]
                }
        }
    }

let stationsByCountry (countryCode: string) =
    html.inject
        (fun
            (store: IShareStore,
             stationsService: IStationsService,
             localizer: IStringLocalizer<SharedResources>,
             hook: IComponentHook) ->
            let getStationsByCountryCode (code: string) =
                async {
                    let searchParams = SearchStationParameters(None, None, Some code, None, None)
                    store.SearchMode.Publish(Search searchParams)
                    let parameters = getParameters (0, stationsService.Settings)
                    return! stationsService.SearchStations(searchParams, parameters)
                }

            hook.AddFirstAfterRenderTask(fun _ ->
                task {
                    let! stations = getStationsByCountryCode countryCode
                    store.Stations.Publish stations
                    let regionInfo = RegionInfo countryCode
                    store.HeaderTitle.Publish $"""{localizer["StationsByCountry"]}: {regionInfo.EnglishName}"""
                    store.IsPlaying.Publish false
                })

            fragment {
                adapt {
                    let! searchString, setSearchString = cval("").WithSetter()

                    let searchEnabled (str: string) =
                        str.Trim().Length > 2 && str.Trim().Length < 36

                    let filterStations (name: string) =
                        async {
                            let searchParams =
                                SearchStationParameters(Some name, Some false, Some countryCode, None, None)

                            store.SearchMode.Publish(Search searchParams)
                            let parameters = getParameters (0, stationsService.Settings)
                            return! stationsService.SearchStations(searchParams, parameters)
                        }

                    let getStations (str: string) =
                        async {
                            if searchEnabled str then
                                return! filterStations searchString
                            else
                                return! getStationsByCountryCode countryCode
                        }

                    div {
                        style' "margin:10px;"

                        FluentStack'' {
                            Orientation Orientation.Horizontal
                            VerticalAlignment VerticalAlignment.Center

                            FluentTextField'' {
                                Label(string (localizer["FilterStations"]))
                                Placeholder(string (localizer["StationNamePlaceholder"]))
                                style' "width:250px;"
                                Immediate true
                                minlength 3
                                maxlength 35

                                onkeydown (fun e ->
                                    task {
                                        if e.Key = "Enter" then
                                            let! stations = getStations searchString
                                            store.Stations.Publish stations
                                    })

                                Value searchString
                                ValueChanged(fun s -> setSearchString s)
                            }

                            FluentButton'' {
                                IconStart(Icons.Regular.Size20.Search())
                                Title "Search"

                                OnClick(fun _ ->
                                    task {
                                        let! stations = getStations searchString
                                        store.Stations.Publish stations
                                    })
                            }
                        }
                    }

                    stationsList (store, localizer)
                }
            })

let stationsByTag (tag: string) =
    html.inject
        (fun
            (store: IShareStore,
             stationsService: IStationsService,
             localizer: IStringLocalizer<SharedResources>,
             hook: IComponentHook) ->
            hook.AddFirstAfterRenderTask(fun _ ->
                task {
                    let searchParams = SearchStationParameters(None, None, None, Some tag, Some true)
                    store.SearchMode.Publish(Search searchParams)
                    store.HeaderTitle.Publish $"""{localizer["StationsByTag"]}: {tag}"""
                    let parameters = getParameters (0, stationsService.Settings)
                    let! stations = stationsService.SearchStations(searchParams, parameters)
                    store.Stations.Publish stations
                    store.IsPlaying.Publish false
                })

            stationsList (store, localizer))

let homePage =
    html.inject (fun (options: IOptions<AppSettings>) ->
        let countryCode = options.Value.CurrentRegion.TwoLetterISORegionName
        stationsByCountry countryCode)

let countriesPage =
    html.inject
        (fun
            (store: IShareStore,
             listsService: IListsService,
             navigation: NavigationManager,
             localizer: IStringLocalizer<SharedResources>,
             hook: IComponentHook) ->
            hook.AddFirstAfterRenderTask(fun _ ->
                task {
                    store.HeaderTitle.Publish localizer["Countries"]
                    store.IsPlaying.Publish false

                    if not (store.Countries.Value.Any()) then
                        let! countries = listsService.GetCountries()
                        store.Countries.Publish countries
                })

            fragment {
                adapt {
                    let! countries, setCountries = store.Countries.WithSetter()
                    let! searchString, setSearchString = cval("").WithSetter()

                    let filteredCountries =
                        if String.IsNullOrWhiteSpace searchString then
                            countries |> Array.distinctBy (fun c -> c.Name)
                        else
                            countries
                            |> Array.filter (fun c -> c.Name.ToLower().Contains(searchString.ToLower()))
                            |> Array.distinctBy (fun c -> c.Name)


                    if countries.Length = 0 then
                        div {
                            style' "text-align:center;"
                            localizer["IsLoading"]
                        }
                    else
                        div {
                            style' "margin:10px;"

                            FluentSearch'' {
                                Placeholder(string (localizer["CountryName"]))
                                Immediate true
                                Value searchString
                                ValueChanged(fun s -> setSearchString s)
                            }
                        }

                        if filteredCountries.Length = 0 then
                            div {
                                style' "text-align:center;"
                                localizer["NoCountriesFound"]
                            }
                        else
                            div {
                                class' "countries-list"

                                for country in filteredCountries do
                                    div {
                                        class' "country"

                                        title'
                                            $"""{country.Name.ToUpper()} ({localizer["StationsCount"]}: {country.Stationcount})"""

                                        onclick (fun _ ->
                                            navigation.NavigateTo $"/stationsByCountry/{country.Iso31661}")

                                        div {
                                            class' "country-name"
                                            country.Name
                                        }

                                        img {
                                            class' "country-image"
                                            src $"./images/flags/{country.Iso31661.ToLower()}.svg"
                                            loadingExperimental true
                                        }
                                    }
                            }
                }
            })

let tagsPage =
    html.inject
        (fun
            (store: IShareStore,
             listsService: IListsService,
             localizer: IStringLocalizer<SharedResources>,
             hook: IComponentHook) ->
            hook.AddFirstAfterRenderTask(fun _ ->
                task {
                    store.HeaderTitle.Publish localizer["Tags"]
                    store.IsPlaying.Publish false

                    if not (store.Tags.Value.Any()) then
                        let! tags = listsService.GetTags()
                        store.Tags.Publish tags
                })

            fragment {
                adapt {
                    let! tags, setTags = store.Tags.WithSetter()
                    let! searchString, setSearchString = cval("").WithSetter()

                    let arrangeTags (arr: NameAndCountProvider.NameAndCount[]) =
                        let ind = arr |> Array.mapi (fun i t -> (i, t))

                        let odds =
                            ind
                            |> Array.filter (fun (i, _) -> i % 2 = 1)
                            |> Array.map (fun (_, t) -> t)
                            |> Array.rev

                        let ev =
                            ind |> Array.filter (fun (i, _) -> i % 2 = 0) |> Array.map (fun (_, t) -> t)

                        Array.append odds ev

                    let filteredTags =
                        if String.IsNullOrWhiteSpace searchString then
                            arrangeTags tags
                        else
                            tags
                            |> Array.filter (fun t -> t.Name.ToLower().Contains(searchString.ToLower()))
                            |> arrangeTags

                    let getColor () =
                        let r = Random().Next(150, 255)
                        let g = Random().Next(150, 255)
                        let b = Random().Next(150, 255)
                        r, g, b

                    let invertColor (rgb: int * int * int) =
                        rgb |> fun (r, g, b) -> 255 - r, 255 - g, 255 - b

                    let colorToRGBString (rgb: int * int * int) =
                        rgb |> fun (r, g, b) -> $"rgb({r},{g},{b})"


                    let calculateFontSize (minCount: int) (maxCount: int) (count: int) =
                        let minSize = 10.0
                        let maxSize = 64.0

                        minSize
                        + (maxSize - minSize) * float (count - minCount) / float (maxCount - minCount)

                    if tags.Length = 0 then
                        div {
                            style' "text-align:center;"
                            localizer["IsLoading"]
                        }
                    else
                        div {
                            style' "margin:10px;"

                            FluentSearch'' {
                                Placeholder(string (localizer["TagName"]))
                                Immediate true
                                Value searchString
                                ValueChanged(fun s -> setSearchString s)
                            }
                        }

                        if filteredTags.Length = 0 then
                            div {
                                style' "text-align:center;"
                                localizer["NoTagsFound"]
                            }
                        else
                            let minCount = filteredTags |> Array.map (fun x -> x.Stationcount) |> Array.min
                            let maxCount = filteredTags |> Array.map (fun x -> x.Stationcount) |> Array.max

                            div {
                                class' "tags-list"

                                for tag in filteredTags do
                                    let rColor = getColor ()
                                    let invertedColor = invertColor rColor
                                    let rSize = calculateFontSize minCount maxCount tag.Stationcount
                                    let fSize = Math.Round(rSize) |> int
                                    let paddingSize = (if rSize > 25.0 then Math.Round(rSize / 5.0) else 0.0) |> int
                                    let heightSize = rSize + 2.0 * Math.Round(rSize / 5.0) |> int

                                    let allStyle =
                                        $"font-size:{fSize}px;color:{colorToRGBString invertedColor};background-color:{colorToRGBString rColor};"

                                    let style =
                                        if rSize > 25.0 then
                                            $"height:{heightSize}px;padding-top:{paddingSize}px;"
                                        else
                                            ""

                                    a {
                                        class' "tag-item"

                                        title'
                                            $"""{tag.Name.ToUpper()} ({localizer["StationsCount"]}: {tag.Stationcount})"""

                                        style' $"{allStyle}{style}"
                                        href $"/stationsByTag/{tag.Name}"
                                        tag.Name
                                    }
                            }
                }
            })

let favoriteStations =
    html.inject
        (fun
            (store: IShareStore,
             stationsService: IStationsService,
             localizer: IStringLocalizer<SharedResources>,
             hook: IComponentHook) ->
            hook.AddFirstAfterRenderTask(fun _ ->
                task {
                    store.SearchMode.Publish Favorites
                    store.HeaderTitle.Publish localizer["Favorites"]
                    let parameters = getParameters (0, stationsService.Settings)
                    let! stations = stationsService.GetFavoriteStations parameters
                    store.Stations.Publish stations
                    store.IsPlaying.Publish false
                })

            stationsList (store, localizer))

let stationsByVotes =
    html.inject
        (fun
            (store: IShareStore,
             stationsService: IStationsService,
             localizer: IStringLocalizer<SharedResources>,
             hook: IComponentHook) ->
            hook.AddFirstAfterRenderTask(fun _ ->
                task {
                    store.SearchMode.Publish ByVotes
                    store.HeaderTitle.Publish localizer["StationsByVotes"]
                    let parameters = getParameters (0, stationsService.Settings)
                    let! stations = stationsService.GetStationsByVotes parameters
                    store.Stations.Publish stations
                    store.IsPlaying.Publish false
                })

            stationsList (store, localizer))

let stationsByClicks =
    html.inject
        (fun
            (store: IShareStore,
             stationsService: IStationsService,
             localizer: IStringLocalizer<SharedResources>,
             hook: IComponentHook) ->
            hook.AddFirstAfterRenderTask(fun _ ->
                task {
                    store.SearchMode.Publish ByClicks
                    store.HeaderTitle.Publish localizer["StationsByClicks"]
                    let parameters = getParameters (0, stationsService.Settings)
                    let! stations = stationsService.GetStationsByClicks parameters
                    store.Stations.Publish stations
                    store.IsPlaying.Publish false
                })

            stationsList (store, localizer))

let historyPage =
    html.inject (fun (store: IShareStore, localizer: IStringLocalizer<SharedResources>, los: ILinkOpeningService) ->
        adapt {
            store.HeaderTitle.Publish localizer["History"]
            store.SearchMode.Publish History
            store.IsPlaying.Publish false
            let! history, setHistory = store.History.WithSetter()

            if history.Length = 0 then
                div {
                    style' "text-align:center;"
                    localizer["NoHistory"]
                }
            else
                table {
                    class' "history-table"

                    thead {
                        tr {
                            th { localizer["Time"] }
                            th { localizer["Station"] }
                            th { localizer["Title"] }
                        }
                    }

                    tbody {
                        for record in history do
                            tr {
                                td { record.StartTime.ToString("g", CultureInfo.CurrentCulture) }
                                td { record.StationName }

                                td {
                                    span {
                                        class' "link"

                                        onclick (fun _ ->
                                            los.OpenUrl
                                                $"https://www.youtube.com/results?search_query={Uri.EscapeDataString record.Title}")

                                        record.Title
                                    }
                                }
                            }
                    }
                }
        })

let rec getTitle (store: IShareStore, metadataService: IMetadataService) =
    task {
        let SelectedStation = store.SelectedStation.Value

        match SelectedStation with
        | NotSelected -> store.Title.Publish None
        | Selected station ->
            if store.IsPlaying.Value then
                let! titleOpt = metadataService.GetTitleAsync station.UrlResolved

                match titleOpt with
                | Some title when not (String.IsNullOrWhiteSpace title) ->
                    store.Title.Publish titleOpt
                    let history = store.History.Value

                    let record =
                        { StartTime = DateTime.Now
                          Title = title
                          StationName = station.Name }

                    if
                        history
                        |> List.exists (fun r -> r.Title = title && r.StationName = station.Name)
                        |> not
                    then
                        store.History.Publish(record :: history |> List.truncate store.HistoryTruncateCount.Value)

                    do! Task.Delay store.GetTitleDelay.Value
                    return! getTitle (store, metadataService)
                | _ -> store.Title.Publish None
            else
                store.Title.Publish None
    }

let player =
    html.inject (fun (store: IShareStore, jsRuntime: IJSRuntime, services: IServices) ->
        adapt {
            let! selectedStation = store.SelectedStation
            let! isPlaying = store.IsPlaying
            let! visiblePopover, setVisiblePopover = cval(false).WithSetter()
            let! volume, setVolume = cval(store.Volume.Value).WithSetter()
            let! selectedStationIsFavorite = store.SelectedStationIsFavorite

            match selectedStation with
            | NotSelected ->
                div {
                    style' "text-align:center;"
                    services.Localizer["NoStationSelected"]
                }
            | Selected station ->
                FluentStack'' {
                    Orientation Orientation.Horizontal
                    VerticalAlignment VerticalAlignment.Center

                    stationIcon station.Favicon

                    FluentStack'' {
                        Orientation Orientation.Vertical
                        VerticalGap 4
                        style' "max-width: 500px;overflow: hidden;"

                        FluentStack'' {
                            Orientation Orientation.Horizontal
                            VerticalAlignment VerticalAlignment.Center

                            if not (String.IsNullOrEmpty station.CountryCode) then
                                img {
                                    class' "flag-icon"
                                    title' station.Country
                                    src $"./images/flags/{station.CountryCode.ToLower()}.svg"
                                    loadingExperimental true
                                }

                            if not (String.IsNullOrEmpty station.Homepage) then
                                FluentAnchor'' {
                                    class' "station-name"
                                    title' station.Name
                                    Appearance Appearance.Hypertext
                                    href "#"
                                    OnClick(fun _ -> services.LinkOpeningService.OpenUrl station.Homepage)

                                    station.Name
                                }
                            else
                                span {
                                    class' "station-name"
                                    title' station.Name
                                    station.Name
                                }
                        }

                        FluentStack'' {
                            Orientation Orientation.Horizontal
                            VerticalAlignment VerticalAlignment.Center
                            HorizontalGap 4

                            FluentIcon'' {
                                Value(
                                    if selectedStationIsFavorite then
                                        Icons.Filled.Size16.Heart() :> Icon
                                    else
                                        Icons.Regular.Size16.Heart() :> Icon
                                )
                            }

                            span { $"{station.Codec} {station.Bitrate} kbps " }
                            span { station.Language }
                        }

                        div {
                            class' "player-tags"

                            if not (String.IsNullOrWhiteSpace station.Tags) then
                                station.Tags.Split ","
                                |> Array.mapi (fun i tag ->
                                    fragment {
                                        if i > 0 then
                                            ", "

                                        a {
                                            href $"/stationsByTag/{tag}"
                                            tag
                                        }
                                    })
                        }
                    }

                    FluentSpacer''

                    FluentButton'' {
                        class' "player-button"
                        Title(string (services.Localizer["AddToFavorites"]))

                        IconStart(
                            if selectedStationIsFavorite then
                                Icons.Filled.Size48.Heart() :> Icon
                            else
                                Icons.Regular.Size48.Heart() :> Icon
                        )

                        OnClick(fun _ ->
                            if services.DataAccess.Exists station.Id then
                                services.DataAccess.Remove station.Id
                                station.IsFavorite <- false
                            else
                                services.DataAccess.Add station
                                station.IsFavorite <- true

                            store.SelectedStationIsFavorite.Publish station.IsFavorite)

                    }

                    FluentButton'' {
                        Id "player-button-volume"
                        class' "player-button"
                        Title(string (services.Localizer["Volume"]))
                        IconStart(Icons.Regular.Size48.Speaker2())
                        OnClick(fun _ -> setVisiblePopover true)
                    }

                    FluentButton'' {
                        class' "player-button"
                        Title(string (services.Localizer["PlayPause"]))

                        IconStart(
                            if isPlaying then
                                Icons.Regular.Size48.RecordStop() :> Icon
                            else
                                Icons.Regular.Size48.PlayCircle() :> Icon
                        )

                        OnClick(fun _ ->
                            task {
                                let newPlaying = not isPlaying
                                store.IsPlaying.Publish newPlaying
                                jsRuntime.InvokeVoidAsync("playAudio", newPlaying) |> ignore
                            })
                    }
                }

                FluentPopover'' {
                    AnchorId "player-button-volume"
                    style' "width: 50px;height: 260px;"
                    VerticalPosition VerticalPosition.Top
                    Open visiblePopover
                    OpenChanged(fun v -> setVisiblePopover v)

                    Body(
                        FluentSliderFloat'' {
                            Orientation Orientation.Vertical
                            Min 0.0
                            Max 1.0
                            Step 0.01
                            Value volume

                            ValueChanged(fun v ->
                                setVolume v
                                store.Volume.Publish v
                                jsRuntime.InvokeVoidAsync("setVolume", v) |> ignore)
                        }
                    )
                }

                audio {
                    id "player"
                    style' "display:none;"
                    controls
                    src station.UrlResolved

                    onstalled (fun _ ->
                        task {
                            let msg = string (services.Localizer["ErrorLoadingStation"])
                            let errorMessage = $"{msg}: {station.Name} ({station.UrlResolved})"

                            services.ToastService.ShowError errorMessage |> ignore
                            store.IsPlaying.Publish false
                        })

                    onerror (fun _ ->
                        task {
                            let msg = string (services.Localizer["ErrorLoadingStation"])
                            let errorMessage = $"{msg}: {station.Name} ({station.UrlResolved})"

                            services.ToastService.ShowError errorMessage |> ignore
                            store.IsPlaying.Publish false
                        })

                    onended (fun _ -> store.IsPlaying.Publish false)
                    onpause (fun _ -> store.IsPlaying.Publish false)
                    onplay (fun _ -> task { return! getTitle (store, services.MetadataService) })
                }
        })

let appHeader =
    html.inject
        (fun
            (store: IShareStore,
             options: IOptions<AppSettings>,
             los: ILinkOpeningService,
             localizer: IStringLocalizer<SharedResources>) ->
            FluentHeader'' {
                FluentStack'' {
                    Orientation Orientation.Horizontal
                    HorizontalGap 2

                    img {
                        src AppSettings.FavIconFileName
                        style { height "40px" }
                    }

                    FluentLabel'' {
                        Typo Typography.H2
                        Color Color.Fill
                        style' "cursor: default;"
                        title' $"v{typeof<AppSettings>.Assembly.GetName().Version}"
                        AppSettings.ApplicationName
                    }

                    FluentSpacer''

                    FluentButton'' {
                        Appearance Appearance.Accent
                        IconStart(Github())
                        title' (string (localizer["GitHub"]))

                        OnClick(fun _ -> los.OpenUrl "https://github.com/poimenov/RadioBrowser")
                    }

                    adapt {
                        let! theme = store.Theme

                        if store.GetTitleDelay.Value <> options.Value.GetTitleDelay then
                            store.GetTitleDelay.Publish options.Value.GetTitleDelay

                        if store.HistoryTruncateCount.Value <> options.Value.HistoryTruncateCount then
                            store.HistoryTruncateCount.Publish options.Value.HistoryTruncateCount

                        FluentDesignTheme'' {
                            StorageName "theme"
                            Mode store.Theme.Value
                            OfficeColor options.Value.AccentColor

                            OnLoaded(fun args ->
                                if args.IsDark then
                                    store.Theme.Publish DesignThemeModes.Dark)
                        }

                        FluentButton'' {
                            Appearance Appearance.Accent
                            IconStart(Icons.Regular.Size20.DarkTheme())
                            title' (string (localizer["SwitchTheme"]))

                            OnClick(fun _ ->
                                store.Theme.Publish(
                                    if theme = DesignThemeModes.Dark then
                                        DesignThemeModes.Light
                                    else
                                        DesignThemeModes.Dark
                                ))
                        }
                    }
                }
            })

let appFooter =
    html.inject (fun (store: IShareStore, services: IServices) ->
        FluentFooter'' {
            FluentAnchor'' {
                Appearance Appearance.Hypertext
                href "#"
                OnClick(fun _ -> services.LinkOpeningService.OpenUrl "https://slaveoftime.github.io/Fun.Blazor.Docs/")

                "Fun.Blazor"
            }

            adapt {
                let! title = store.Title
                let locTitle = services.Localizer["NowPlaying"]

                let sTitle =
                    match title with
                    | Some t when not (String.IsNullOrWhiteSpace t) -> $"{locTitle}: {t}"
                    | _ -> ""

                div {
                    id "status-bar"
                    style' "flex-grow: 1;padding-left: 10px;cursor: pointer;text-align: center;"
                    title' (string (services.Localizer["SearchOnYouTube"]))

                    onclick (fun _ ->
                        match title with
                        | Some t when not (String.IsNullOrWhiteSpace t) ->
                            services.LinkOpeningService.OpenUrl
                                $"https://www.youtube.com/results?search_query={Uri.EscapeDataString t}"
                        | _ -> ())

                    sTitle
                }
            }


            FluentAnchor'' {
                Appearance Appearance.Hypertext
                href "#"
                OnClick(fun _ -> services.LinkOpeningService.OpenUrl "https://www.tryphotino.io")

                "Photino"
            }
        })

let navmenus =
    html.injectWithNoKey (fun (store: IShareStore, localizer: IStringLocalizer<SharedResources>) ->
        adaptiview () {
            let! binding = store.IsMenuOpen.WithSetter()

            FluentNavMenu'' {
                Width 200
                Collapsible true

                Expanded' binding

                FluentNavLink'' {
                    Href "/"
                    Match NavLinkMatch.All
                    Icon(Icons.Regular.Size20.Home())
                    Tooltip(string (localizer["Home"]))
                    localizer["Home"]
                }

                FluentNavLink'' {
                    Href "/favorites"
                    Match NavLinkMatch.Prefix
                    Icon(Icons.Regular.Size20.Heart())
                    Tooltip(string (localizer["Favorites"]))
                    localizer["Favorites"]
                }

                FluentNavLink'' {
                    Href "/countries"
                    Match NavLinkMatch.Prefix
                    Icon(Icons.Regular.Size20.Flag())
                    Tooltip(string (localizer["ByCountry"]))
                    localizer["ByCountry"]
                }

                FluentNavLink'' {
                    Href "/tags"
                    Match NavLinkMatch.Prefix
                    Icon(Icons.Regular.Size20.Tag())
                    Tooltip(string (localizer["ByTags"]))
                    localizer["ByTags"]
                }

                FluentNavLink'' {
                    Href "/stationsByVotes"
                    Match NavLinkMatch.Prefix
                    Icon(Icons.Regular.Size20.Vote())
                    Tooltip(string (localizer["ByVotes"]))
                    localizer["ByVotes"]
                }

                FluentNavLink'' {
                    Href "/stationsByClicks"
                    Match NavLinkMatch.Prefix
                    Icon(Icons.Regular.Size20.CursorClick())
                    Tooltip(string (localizer["ByClicks"]))
                    localizer["ByClicks"]
                }

                FluentNavLink'' {
                    Href "/history"
                    Match NavLinkMatch.Prefix
                    Icon(Icons.Regular.Size20.History())
                    Tooltip(string (localizer["History"]))
                    localizer["History"]
                }
            }
        })

let routes =
    html.route
        [| routeCi "/stationsByVotes" stationsByVotes
           routeCi "/stationsByClicks" stationsByClicks
           routeCi "/favorites" favoriteStations
           routeCi "/countries" countriesPage
           routeCif "/stationsByCountry/%s" (fun x -> stationsByCountry (x))
           routeCi "/tags" tagsPage
           routeCif "/stationsByTag/%s" (fun x -> stationsByTag (x))
           routeCi "/history" historyPage
           routeAny homePage |]

let app =
    html.inject
        (fun
            (hook: IComponentHook,
             store: IShareStore,
             options: IOptions<AppSettings>,
             stationService: IStationsService,
             dataAccess: IFavoritesDataAccess) ->
            hook.AddInitializedTask(fun _ ->
                task {
                    let update (ids: Guid array) =
                        async {
                            let! stations = stationService.GetStations ids
                            stations |> dataAccess.Update |> ignore
                        }

                    let favCount = dataAccess.FavoritesCount()
                    let favArrs = new List<Station array>()
                    let mutable count = 0

                    while count < favCount do
                        let favs = dataAccess.GetFavorites(getParameters (count, stationService.Settings))
                        favArrs.Add favs
                        count <- count + favs.Length

                    favArrs
                    |> Seq.toList
                    |> List.map (fun arr -> update (arr |> Array.map (fun x -> x.Id)))
                    |> fun upd -> Async.Parallel(upd, 5) |> Async.Ignore |> Async.Start
                })

            ErrorBoundary'' {
                ErrorContent(fun e ->
                    FluentLabel'' {
                        Color Color.Error
                        string e
                    })

                FluentDesignTheme'' { StorageName "theme" }
                FluentToastProvider'' { MaxToastCount 3 }

                FluentLayout'' {
                    appHeader

                    FluentStack'' {
                        Width "100%"
                        class' "main"
                        Orientation Orientation.Horizontal
                        navmenus

                        FluentBodyContent'' {
                            class' "body-content"
                            style { overflowHidden }

                            FluentStack'' {
                                style' "width:100%;height:100%;"
                                Orientation Orientation.Vertical
                                VerticalGap 2

                                adapt {
                                    let! selectedStation = store.SelectedStation
                                    let! searchMode = store.SearchMode
                                    let! headerTitle = store.HeaderTitle

                                    let icon: Icon =
                                        match searchMode with
                                        | Search parameters ->
                                            if parameters.CountryCode.IsSome then
                                                if
                                                    parameters.CountryCode.Value = options.Value.CurrentRegion.TwoLetterISORegionName
                                                then
                                                    Icons.Regular.Size24.Home()
                                                else
                                                    Icons.Regular.Size24.Flag()
                                            else if parameters.Tag.IsSome then
                                                Icons.Regular.Size24.Tag()
                                            else
                                                Icons.Regular.Size24.Search()
                                        | Favorites -> Icons.Regular.Size32.Heart()
                                        | ByVotes -> Icons.Regular.Size24.Vote()
                                        | ByClicks -> Icons.Regular.Size24.CursorClick()
                                        | History -> Icons.Regular.Size24.History()


                                    FluentStack'' {
                                        Orientation Orientation.Horizontal
                                        VerticalAlignment VerticalAlignment.Center
                                        HorizontalAlignment HorizontalAlignment.Center
                                        HorizontalGap 4
                                        style' "height: 30px;"

                                        FluentIcon'' { Value icon }

                                        span {
                                            style' "font-size: 24px; font-weight: bold;"
                                            headerTitle
                                        }
                                    }

                                    let styleHeight =
                                        if selectedStation = NotSelected then
                                            "height: calc(100% - 30px);"
                                        else
                                            "height: calc(100% - 104px);"

                                    div {
                                        class' "content"
                                        style' styleHeight

                                        routes
                                    }

                                    if selectedStation <> NotSelected then
                                        div {
                                            class' "player-container"
                                            player
                                        }
                                }
                            }
                        }
                    }

                    appFooter
                }
            })

type AppComponent() =
    inherit FunBlazorComponent()
    override this.Render() = app
