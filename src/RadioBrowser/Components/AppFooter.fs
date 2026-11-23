[<AutoOpen>]
module RadioBrowser.AppFooter

open System
open Microsoft.Extensions.Options
open Microsoft.FluentUI.AspNetCore.Components
open Fun.Blazor

let appFooter =
    html.inject (fun (store: IShareStore, services: IServices, options: IOptions<AppSettings>, pluginService: IPluginService) ->
        footer {
            FluentLink'' {
                Appearance LinkAppearance.Default
                OnClick(fun _ -> services.LinkOpeningService.OpenUrl "https://slaveoftime.github.io/Fun.Blazor.Docs/")

                "Fun.Blazor"
            }

            adapt {
                let! title = store.Title
                let! _cultureName = store.CultureName
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
                        HorizontalGap "4"

                        FluentIcon'' {
                            slot' "start"
                            title' (string (services.Localizer["SearchForDownloading"]))
                            Value(Icons.Regular.Size20.Search())
                            style' $"margin-right: 15px;cursor: pointer;display: {displayStyle};"

                            OnClick(fun _ ->
                                task {
                                    match title with
                                    | Some t when not (String.IsNullOrWhiteSpace t) ->
                                        let plugins = pluginService.GetPlugins()

                                        if plugins.Count > 0 then
                                            let downloader = 
                                                if String.IsNullOrWhiteSpace(options.Value.SelectedDownloadPlugin) |> not then
                                                    match pluginService.GetPluginByName(options.Value.SelectedDownloadPlugin) with
                                                    | Some p -> p
                                                    | None -> plugins.[0]
                                                else
                                                    plugins.[0]

                                            let content =
                                                DialogData<DownloadPanelData>(DownloadPanelData(downloader, t))

                                            let dialogOptions = DialogOptions()

                                            dialogOptions.Header.Title <-
                                                String.Concat(
                                                    string (services.Localizer["SearchOn"]),
                                                    " ",
                                                    downloader.PluginName
                                                )

                                            dialogOptions.Header.CloseAction.Visible <- true
                                            dialogOptions.Width <- "450px"
                                            dialogOptions.Alignment <- DialogAlignment.End
                                            dialogOptions.Data <- content

                                            let! _ =
                                                services.DialogService.ShowDrawerAsync<DownloadPanel>(dialogOptions)
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

            FluentLink'' {
                Appearance LinkAppearance.Default
                OnClick(fun _ -> services.LinkOpeningService.OpenUrl "https://www.tryphotino.io")

                "Photino"
            }
        })
