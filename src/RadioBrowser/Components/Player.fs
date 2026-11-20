[<AutoOpen>]
module RadioBrowser.Player

open System
open System.Globalization
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
            let volumePercent = (volume * 100.0).ToString("0.##", CultureInfo.InvariantCulture)

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
                    HorizontalGap "12"

                    stationIcon station.Favicon

                    FluentStack'' {
                        Orientation Orientation.Vertical
                        VerticalGap "4"
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
                                FluentLink'' {
                                    class' "station-name"
                                    title' station.Name
                                    Appearance LinkAppearance.Default
                                    Href "#"
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
                            HorizontalGap "4"

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

                        FluentIcon'' {
                            Value(
                                if selectedStationIsFavorite then
                                    Icons.Filled.Size48.Heart() :> Icon
                                else
                                    Icons.Regular.Size48.Heart() :> Icon
                            )
                            style' "width:48px;height:48px;flex-shrink:0;color:var(--colorBrandForeground1);"
                        }
                    }

                    FluentButton'' {
                        Id "player-button-volume"
                        class' "player-button"
                        Title(string (services.Localizer["Volume"]))
                        OnClick(fun _ -> setVisiblePopover true)

                        FluentIcon'' {
                            Value(Icons.Regular.Size48.Speaker2())
                            style' "width:48px;height:48px;flex-shrink:0;color:var(--colorBrandForeground1);"
                        }
                    }

                    FluentButton'' {
                        class' "player-button"
                        Title(string (services.Localizer["PlayPause"]))

                        OnClick(fun _ ->
                            task {
                                if not isPlaying then
                                    services.StationsService.ClickStation station.Id

                                store.IsPlaying.Publish(not isPlaying)
                            })

                        FluentIcon'' {
                            Value(
                                if isPlaying then
                                    Icons.Regular.Size48.RecordStop() :> Icon
                                else
                                    Icons.Regular.Size48.PlayCircle() :> Icon
                            )
                            style' "width:48px;height:48px;flex-shrink:0;color:var(--colorBrandForeground1);"
                        }
                    }
                }

                FluentPopover'' {
                    AnchorId "player-button-volume"
                    class' "volume-popover"
                    style' "width:96px;height:260px;padding:0;"
                    Opened visiblePopover
                    OpenedChanged(fun v -> setVisiblePopover v)

                    div {
                        class' "volume-slider-container"

                        input {
                            type' "range"
                            min "0"
                            max "100"
                            step "1"
                            value volumePercent
                            class' "volume-slider"
                            style' ("--volume-percent:" + volumePercent + "%;")

                            oninput (fun args ->
                                task {
                                    let rawValue = Convert.ToString(args.Value, CultureInfo.InvariantCulture)
                                    let percent = Double.Parse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture)

                                    if percent < 0.0 || percent > 100.0 then
                                        invalidArg "args" "Volume percentage must be between 0 and 100."

                                    let normalizedVolume = percent / 100.0
                                    do! services.JsRuntime.InvokeVoidAsync("setVolume", normalizedVolume)
                                    setVolume normalizedVolume
                                    store.Volume.Publish normalizedVolume
                                })
                        }

                        div {
                            class' "volume-scale"

                            for percent in [ 100; 75; 50; 25; 0 ] do
                                div {
                                    class' "volume-scale-mark"
                                    span { class' "volume-scale-tick" }
                                    span { string percent + "%" }
                                }
                        }
                    }
                }
        })
