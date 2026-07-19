[<AutoOpen>]
module RadioBrowser.AppFooter

open System
open Microsoft.FluentUI.AspNetCore.Components
open Fun.Blazor

let appFooter =
    html.inject (fun (store: IShareStore, services: IServices, pluginService: IPluginService) ->
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

                let displayStyle =
                    match title with
                    | Some t when not (String.IsNullOrWhiteSpace t) ->
                        if pluginService.GetPlugins().Count > 0 then
                            "inline-block"
                        else
                            "none"
                    | _ -> "none"

                div {
                    style' "flex-grow: 1;text-align: center;vertical-align: middle;height:16px;"

                    FluentStack'' {
                        Orientation Orientation.Horizontal
                        VerticalAlignment VerticalAlignment.Center
                        HorizontalAlignment HorizontalAlignment.Center
                        HorizontalGap 4

                        FluentIcon'' {
                            slot' "start"
                            title' "Search for downloading"
                            Value(Icons.Regular.Size20.Search())
                            style' $"margin-right: 15px;cursor: pointer;display: {displayStyle};"

                            OnClick(fun _ ->
                                task {
                                    match title with
                                    | Some t when not (String.IsNullOrWhiteSpace t) ->
                                        let plugins = pluginService.GetPlugins()

                                        if plugins.Count > 0 then
                                            let downloader = plugins.[0]

                                            let content =
                                                DialogData<DownloadPanelData>(DownloadPanelData(downloader, t))

                                            let dialogParams = DialogParameters<DialogData<DownloadPanelData>>()
                                            dialogParams.Title <- $"Search on {downloader.PluginName}"
                                            dialogParams.Width <- "450px"
                                            dialogParams.Alignment <- HorizontalAlignment.Right

                                            dialogParams.Content <- content

                                            let! dialog =
                                                services.DialogService.ShowPanelAsync<DownloadPanel>(
                                                    content,
                                                    dialogParams
                                                )
                                                |> Async.AwaitTask

                                            ()
                                    | _ -> ()
                                })
                        }

                        span {
                            id "status-bar"

                            style' "cursor: pointer;"

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
                }
            }

            FluentAnchor'' {
                Appearance Appearance.Hypertext
                href "#"
                OnClick(fun _ -> services.LinkOpeningService.OpenUrl "https://www.tryphotino.io")

                "Photino"
            }
        })
