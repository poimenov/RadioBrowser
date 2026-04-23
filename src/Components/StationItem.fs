[<AutoOpen>]
module RadioBrowser.StationItem

open System
open Microsoft.FluentUI.AspNetCore.Components
open Fun.Blazor

let stationIcon (iconUrl: string) =
    let defaultImageSrc = "./images/radio.svg"

    let imgSrc =
        if String.IsNullOrWhiteSpace iconUrl then
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