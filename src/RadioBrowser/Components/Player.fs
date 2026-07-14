[<AutoOpen>]
module RadioBrowser.Player

open System
open Microsoft.FluentUI.AspNetCore.Components
open Microsoft.JSInterop
open FSharp.Data.Adaptive
open Fun.Blazor

let player =
    html.inject (fun (store: IShareStore, services: IServices) ->
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
                                a {
                                    href $"/stationsByCountry/{station.CountryCode}"
                                    title' station.Country
                                    class' "flag-icon-link"

                                    img {
                                        class' "flag-icon"
                                        src $"./images/flags/{station.CountryCode.ToLower()}.svg"
                                        loadingExperimental true
                                    }
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
                            task {
                                if services.FavoritesDataAccess.Exists station.Id then
                                    services.FavoritesDataAccess.Remove station.Id
                                    station.IsFavorite <- false
                                else
                                    services.FavoritesDataAccess.Add station
                                    services.StationsService.VoteStation station.Id

                                store.SelectedStationIsFavorite.Publish station.IsFavorite
                            })
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
                                if not isPlaying then
                                    services.StationsService.ClickStation station.Id

                                store.IsPlaying.Publish(not isPlaying)
                            })
                    }
                }

                FluentPopover'' {
                    AnchorId "player-button-volume"
                    style' "width: 50px;height: 260px;"
                    VerticalPosition VerticalPosition.Top
                    HorizontalPosition HorizontalPosition.Center
                    Open visiblePopover
                    OpenChanged(fun v -> setVisiblePopover v)

                    Body(
                        FluentSliderFloat'' {
                            Orientation Orientation.Vertical
                            Min 0.0
                            Max 1.0
                            Step 0.01
                            Value(1.0 - volume)

                            ValueChanged(fun v ->
                                let rv = 1.0 - v
                                setVolume rv
                                store.Volume.Publish rv
                                services.JsRuntime.InvokeVoidAsync("setVolume", rv) |> ignore)
                        }
                    )
                }
        })
