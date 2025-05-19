[<AutoOpen>]
module RadioBrowser.App

open System
open System.Linq
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
    | Selected of StationsProvider.Station

type CurrentSearchMode =
    | Search of SearchStationParameters
    | ByVotes
    | ByClicks

type IShareStore with
    member store.IsMenuOpen = store.CreateCVal(nameof store.IsMenuOpen, true)
    member store.Theme = store.CreateCVal(nameof store.Theme, DesignThemeModes.Light)

    member store.Stations =
        store.CreateCVal(nameof store.Stations, Enumerable.Empty<StationsProvider.Station>())

    member store.SelectedStation =
        store.CreateCVal(nameof store.SelectedStation, NotSelected)

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
                | Search ssp -> stationsService.SearchStations(ssp, parameters)

            if newStations.Count() > 0 then
                let stations = store.Stations.Value.Concat(newStations)
                store.Stations.Publish stations
        }

let homePage =
    fragment {
        SectionContent'' {
            SectionName "Title"
            "Home"
        }

        FluentLabel'' {
            Typo Typography.H1
            Color Color.Accent
            "Hi from FunBlazor"
        }
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

let stationItem (station: StationsProvider.Station) =
    div {
        class' "station-item"
        stationIcon station.Favicon

        div {
            div {
                if not (String.IsNullOrEmpty station.Countrycode) then
                    img {
                        class' "flag-icon"
                        src $"./images/flags/{station.Countrycode.ToLower()}.svg"
                        loadingExperimental true
                    }

                span {
                    class' "station-name"
                    title' station.Name
                    station.Name
                }
            }

            div {
                span { $"{station.Bitrate} kbps " }
                span { station.Language }
            }

            div { station.Tags }
        }
    }

let stationsList (store: IShareStore) =
    div {
        style' "padding:10px;"

        adapt {

            let! stations = store.Stations
            let! selectedStation = store.SelectedStation

            let isCurrent (curr: StationsProvider.Station) =
                match selectedStation with
                | NotSelected -> false
                | Selected item -> curr.Stationuuid = item.Stationuuid

            let getSelectedClass (curr: StationsProvider.Station) =
                if isCurrent curr then
                    "station selected-station"
                else
                    "station"

            if stations.Any() then
                div {
                    class' "stations-list"

                    for station in stations.ToArray() do
                        div {
                            class' (getSelectedClass station)

                            onclick (fun _ ->
                                let selected = SelectedStation.Selected(station)
                                store.SelectedStation.Publish(selected))

                            stationItem station
                        }

                    watchElementVisibleComponent
                }
            else
                div {
                    style' "text-align:center;"

                    "No stations found."
                }
        }
    }

let stationsByVotes =
    html.inject (fun (store: IShareStore, stationsService: IStationsService, hook: IComponentHook) ->
        hook.AddFirstAfterRenderTask(fun _ ->
            task {
                store.SearchMode.Publish ByVotes
                let parameters = getParameters (0, stationsService.Settings)
                let! stations = stationsService.GetStationsByVotes parameters
                store.Stations.Publish stations
            })

        fragment {
            SectionContent'' {
                SectionName "Title"
                "Stations by votes"
            }

            stationsList store
        })

let stationsByClicks =
    html.inject (fun (store: IShareStore, stationsService: IStationsService, hook: IComponentHook) ->
        hook.AddFirstAfterRenderTask(fun _ ->
            task {
                store.SearchMode.Publish ByClicks
                let parameters = getParameters (0, stationsService.Settings)
                let! stations = stationsService.GetStationsByClicks parameters
                store.Stations.Publish stations
            })

        fragment {
            SectionContent'' {
                SectionName "Title"
                "Stations by clicks"
            }

            stationsList store
        })

let player =
    html.inject (fun (store: IShareStore, jsRuntime: IJSRuntime) ->
        adapt {
            let! selectedStation = store.SelectedStation
            let! isPlaying, setIsPlaying = cval(false).WithSetter()
            let! visiblePopover, setVisiblePopover = cval(false).WithSetter()
            let! volume, setVolume = cval(store.Volume.Value).WithSetter()

            match selectedStation with
            | NotSelected -> div { "No station selected" }
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

                            if not (String.IsNullOrEmpty station.Countrycode) then
                                img {
                                    class' "flag-icon"
                                    src $"./images/flags/{station.Countrycode.ToLower()}.svg"
                                    loadingExperimental true
                                }

                            span {
                                class' "station-name"
                                title' station.Name
                                station.Name
                            }
                        }

                        div {
                            span { $"{station.Bitrate} kbps " }
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
                        IconStart(Icons.Regular.Size48.Heart())
                    }

                    FluentButton'' {
                        Id "player-button-volume"
                        class' "player-button"
                        IconStart(Icons.Regular.Size48.Speaker2())
                        OnClick(fun _ -> setVisiblePopover true)
                    }

                    FluentButton'' {
                        class' "player-button"

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
    html.injectWithNoKey (fun (store: IShareStore) ->
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
                    "Home"
                }

                FluentNavLink'' {
                    Href "/stationsByVotes"
                    Match NavLinkMatch.Prefix
                    Icon(Icons.Regular.Size20.MusicNote1())
                    "By votes"
                }

                FluentNavLink'' {
                    Href "/stationsByClicks"
                    Match NavLinkMatch.Prefix
                    Icon(Icons.Regular.Size20.MusicNote1())
                    "By clicks"
                }
            }
        })

let routes =
    html.route
        [| routeCi "/stationsByVotes" stationsByVotes
           routeCi "/stationsByClicks" stationsByClicks
           routeAny homePage |]

let app =
    ErrorBoundary'' {
        ErrorContent(fun e ->
            FluentLabel'' {
                Color Color.Error
                string e
            })

        //FluentToastProvider''
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
