[<AutoOpen>]
module RadioBrowser.AppFooter

open System
open Microsoft.FluentUI.AspNetCore.Components
open Fun.Blazor

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
                            services.LinkOpeningService.OpenUrl(
                                String.Format(
                                    services.StationsService.Settings.TrackSearchUrl,
                                    Uri.EscapeDataString t
                                )
                            )
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
