[<AutoOpen>]
module RadioBrowser.App

open System
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

type SharedResources() = class end

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

let getParameters (offset: int, settings: AppSettings) =
    GetStationParameters(offset, settings.LimitCount, settings.HideBroken)

type ElementVisibilityCallback(store: IShareStore, stationsService: IStationsService) =
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

            if newStations.Count() > 0 then
                let stations = store.Stations.Value.Concat(newStations)
                store.Stations.Publish stations
        }

let watchElementVisibleComponent =
    html.inject
        (fun (store: IShareStore, jsRuntime: IJSRuntime, hook: IComponentHook, stationsService: IStationsService) ->
            let elementId = "watched-div"

            hook.AddFirstAfterRenderTask(fun _ ->
                task {
                    let callback = ElementVisibilityCallback(store, stationsService)
                    let dotNetRef = DotNetObjectReference.Create(callback)
                    jsRuntime.InvokeVoidAsync("observeVisibility", elementId, dotNetRef) |> ignore
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
                                let selected = SelectedStation.Selected(station)
                                store.SelectedStationIsFavorite.Publish(station.IsFavorite)
                                store.SelectedStation.Publish(selected))

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
                    let searchParams =
                        SearchStationParameters(None, None, Some code, None, None, None, None)

                    store.SearchMode.Publish(Search searchParams)
                    let parameters = getParameters (0, stationsService.Settings)
                    return! stationsService.SearchStations(searchParams, parameters)
                }

            hook.AddFirstAfterRenderTask(fun _ ->
                task {
                    let! stations = getStationsByCountryCode (countryCode)
                    store.Stations.Publish stations
                })

            fragment {
                adapt {
                    let! searchString, setSearchString = cval("").WithSetter()

                    let searchEnabled (str: string) =
                        str.Trim().Length > 2 && str.Trim().Length < 36

                    let filterStations (name: string) =
                        async {
                            let searchParams =
                                SearchStationParameters(
                                    Some name,
                                    Some false,
                                    Some countryCode,
                                    None,
                                    None,
                                    None,
                                    None
                                )

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
                    let searchParams =
                        SearchStationParameters(None, None, None, None, Some tag, None, None)

                    store.SearchMode.Publish(Search searchParams)
                    let parameters = getParameters (0, stationsService.Settings)
                    let! stations = stationsService.SearchStations(searchParams, parameters)
                    store.Stations.Publish stations
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

                            FluentTextField'' {
                                Label(string (localizer["FilterCountries"]))
                                Placeholder(string (localizer["CountryName"]))
                                Immediate true
                                Value searchString
                                ValueChanged(fun s -> setSearchString s)
                            }
                        }

                        div {
                            class' "countries-list"

                            for country in filteredCountries do
                                div {
                                    class' "country"

                                    title'
                                        $"""{country.Name.ToUpper()} ({localizer["StationsCount"]}: {country.Stationcount})"""

                                    onclick (fun _ -> navigation.NavigateTo $"/stationsByCountry/{country.Iso31661}")

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
                    if not (store.Tags.Value.Any()) then
                        let! tags = listsService.GetTags()
                        let ind = tags |> Array.mapi (fun i t -> (i, t))

                        let odds =
                            ind
                            |> Array.filter (fun (i, _) -> i % 2 = 1)
                            |> Array.map (fun (_, t) -> t)
                            |> Array.rev

                        let ev =
                            ind |> Array.filter (fun (i, _) -> i % 2 = 0) |> Array.map (fun (_, t) -> t)

                        let result = Array.append odds ev
                        store.Tags.Publish result
                })

            fragment {
                adapt {
                    let! tags, setTags = store.Tags.WithSetter()

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
                        let minCount = tags |> Array.map (fun x -> x.Stationcount) |> Array.min
                        let maxCount = tags |> Array.map (fun x -> x.Stationcount) |> Array.max

                        div {
                            class' "tags-list"

                            for tag in tags do
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
                    let parameters = getParameters (0, stationsService.Settings)
                    let! stations = stationsService.GetFavoriteStations parameters
                    store.Stations.Publish stations
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
                    let parameters = getParameters (0, stationsService.Settings)
                    let! stations = stationsService.GetStationsByVotes parameters
                    store.Stations.Publish stations
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
                    let parameters = getParameters (0, stationsService.Settings)
                    let! stations = stationsService.GetStationsByClicks parameters
                    store.Stations.Publish stations
                })

            stationsList (store, localizer))

let player =
    html.inject
        (fun
            (store: IShareStore,
             jsRuntime: IJSRuntime,
             dataAccess: IFavoritesDataAccess,
             localizer: IStringLocalizer<SharedResources>,
             los: ILinkOpeningService) ->
            adapt {
                let! selectedStation = store.SelectedStation
                let! isPlaying, setIsPlaying = cval(false).WithSetter()
                let! visiblePopover, setVisiblePopover = cval(false).WithSetter()
                let! volume, setVolume = cval(store.Volume.Value).WithSetter()
                let! selectedStationIsFavorite = store.SelectedStationIsFavorite

                match selectedStation with
                | NotSelected ->
                    div {
                        style' "text-align:center;"
                        localizer["NoStationSelected"]
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
                                        OnClick(fun _ -> los.OpenUrl station.Homepage)

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
                                style' "height:40px;"
                                station.Tags
                            }
                        }

                        FluentSpacer''

                        FluentButton'' {
                            class' "player-button"
                            Title(string (localizer["AddToFavorites"]))

                            IconStart(
                                if selectedStationIsFavorite then
                                    Icons.Filled.Size48.Heart() :> Icon
                                else
                                    Icons.Regular.Size48.Heart() :> Icon
                            )

                            OnClick(fun _ ->
                                if dataAccess.Exists station.Id then
                                    dataAccess.Remove station.Id
                                    station.IsFavorite <- false
                                else
                                    dataAccess.Add station
                                    station.IsFavorite <- true

                                store.SelectedStationIsFavorite.Publish station.IsFavorite)

                        }

                        FluentButton'' {
                            Id "player-button-volume"
                            class' "player-button"
                            Title(string (localizer["Volume"]))
                            IconStart(Icons.Regular.Size48.Speaker2())
                            OnClick(fun _ -> setVisiblePopover true)
                        }

                        FluentButton'' {
                            class' "player-button"
                            Title(string (localizer["PlayPause"]))

                            IconStart(
                                if isPlaying then
                                    Icons.Regular.Size48.RecordStop() :> Icon
                                else
                                    Icons.Regular.Size48.PlayCircle() :> Icon
                            )

                            OnClick(fun _ ->
                                task {
                                    let newPlaying = not isPlaying
                                    setIsPlaying newPlaying
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

                        FluentDesignTheme'' {
                            StorageName "theme"
                            Mode store.Theme.Value
                            OfficeColor options.Value.AccentColor

                            OnLoaded(fun args ->
                                if args.IsDark then
                                    store.Theme.Publish(DesignThemeModes.Dark))
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
    html.inject (fun (los: ILinkOpeningService) ->
        FluentFooter'' {
            FluentAnchor'' {
                Appearance Appearance.Hypertext
                href "#"
                OnClick(fun _ -> los.OpenUrl "https://slaveoftime.github.io/Fun.Blazor.Docs/")

                "Fun.Blazor"
            }

            FluentSpacer''

            FluentAnchor'' {
                Appearance Appearance.Hypertext
                href "#"
                OnClick(fun _ -> los.OpenUrl "https://www.tryphotino.io")

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
           routeAny homePage |]

let app =
    ErrorBoundary'' {
        ErrorContent(fun e ->
            FluentLabel'' {
                Color Color.Error
                string e
            })

        FluentDesignTheme'' { StorageName "theme" }

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

                        div {
                            class' "content"

                            routes
                        }

                        div {
                            class' "player-container"
                            player
                        }
                    }
                }
            }

            appFooter
        }
    }
