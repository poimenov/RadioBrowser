[<AutoOpen>]
module RadioBrowser.DownloadPanel

open System
open System.Linq
open System.Threading
open Microsoft.AspNetCore.Components
open Microsoft.Extensions.Localization
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Options
open Microsoft.FluentUI.AspNetCore.Components
open FSharp.Data.Adaptive
open Fun.Blazor
open RadioBrowser.PluginContract

type DownloadPanel() =
    inherit FunComponent()

    let gridRef: ElementReference ref = ref Unchecked.defaultof<ElementReference>
    let defaultTimeout = 5.0

    [<Inject>]
    member val Logger: ILogger<DownloadPanel> = Unchecked.defaultof<_> with get, set

    [<Inject>]
    member val Localizer: IStringLocalizer<SharedResources> = Unchecked.defaultof<_> with get, set

    [<Inject>]
    member val ToastService: IToastService = Unchecked.defaultof<_> with get, set

    [<Inject>]
    member val Options: IOptions<AppSettings> = Unchecked.defaultof<_> with get, set

    [<Parameter>]
    member val Content = Unchecked.defaultof<DialogData<DownloadPanelData>> with get, set

    interface IDialogContentComponent<DialogData<DownloadPanelData>> with
        member this.Content = this.Content

        member this.Content
            with set (value) = this.Content <- value

    [<CascadingParameter>]
    member val Dialog = Unchecked.defaultof<FluentDialog> with get, set

    member val SearchResults = cval (Enumerable.Empty<Track>()) with get, set

    member val IsLoading = cval false with get, set
    member val IsDownloading = cval false with get, set
    member val SelectedTrack = cval Unchecked.defaultof<Track> with get, set

    member this.PerfomSearch(search: string) =
        task {
            this.IsLoading.Publish true |> ignore
            this.SelectedTrack.Publish Unchecked.defaultof<Track> |> ignore
            this.StateHasChanged()

            let timeout =
                if this.Options.Value.SearchDownloadTimeout > 0 then
                    this.Options.Value.SearchDownloadTimeout
                else
                    defaultTimeout

            use cts = new CancellationTokenSource(TimeSpan.FromSeconds timeout)
            let! result = this.Content.Data.Downloader.Search(search, cts.Token, 50)

            if result.Success then
                this.SearchResults.Publish result.Result |> ignore
                this.StateHasChanged()
            else
                this.Logger.LogError(result.Exception, result.Message)
                this.ToastService.ShowError result.Message |> ignore

            this.IsLoading.Publish false |> ignore
            this.StateHasChanged()
        }

    member this.PerformDownload(track: Track) =
        task {
            this.IsDownloading.Publish true |> ignore
            this.StateHasChanged()

            let timeout =
                if this.Options.Value.SearchDownloadTimeout > 0 then
                    this.Options.Value.SearchDownloadTimeout
                else
                    defaultTimeout

            use cts = new CancellationTokenSource(TimeSpan.FromSeconds timeout)

            let! result =
                this.Content.Data.Downloader.Download(track, this.Options.Value.DownloadsFolderPath, cts.Token)

            if result.Success then
                this.ToastService.ShowSuccess
                    $"The file {track.FileName} was successfully downloaded to the folder: {this.Options.Value.DownloadsFolderPath}"
                |> ignore
            else
                this.Logger.LogError(result.Exception, result.Message)
                this.ToastService.ShowError result.Message |> ignore

            this.IsDownloading.Publish false |> ignore
            this.StateHasChanged()
        }

    override this.OnInitializedAsync() : Tasks.Task =
        this.PerfomSearch this.Content.Data.Search |> ignore
        base.OnInitializedAsync()

    override this.Render() : NodeRenderFragment =
        adapt {
            let tracks = this.SearchResults.Value.AsQueryable<Track>()

            FluentDialogHeader'' {
                title' this.Dialog.Instance.Parameters.Title
                ShowDismiss true
            }

            FluentDialogBody'' {
                FluentStack'' {
                    Orientation Orientation.Horizontal
                    VerticalAlignment VerticalAlignment.Center
                    HorizontalGap 4
                    style' "width: 100%; margin-bottom: 15px; flex-shrink: 0;"

                    FluentTextField'' {
                        Placeholder(string (this.Localizer["SearchPlaceholder"]))
                        Immediate true
                        value this.Content.Data.Search
                        onchange (fun args -> this.Content.Data.Search <- string args.Value)

                        onkeyup (fun args ->
                            task {
                                if args.Key = "Enter" then
                                    this.PerfomSearch this.Content.Data.Search |> ignore
                            })

                        style' "flex-grow: 1;"
                    }

                    FluentButton'' {
                        Appearance Appearance.Accent
                        OnClick(fun _ -> this.PerfomSearch this.Content.Data.Search)

                        "Search"
                    }
                }

                div {
                    style'
                        "max-height: calc(100dvh - 200px); overflow-y: auto; overflow-x: hidden; margin-top: 15px; border: 1px solid var(--neutral-stroke-rest);"

                    FluentDataGrid'' {
                        type' typeof<Track>
                        ShowHover true
                        Items tracks
                        EmptyContent "No results found"

                        OnRowClick(fun row ->
                            this.SelectedTrack.Publish row.Item |> ignore
                            this.StateHasChanged())

                        RowClass(fun track ->
                            if
                                this.SelectedTrack.Value <> Unchecked.defaultof<Track>
                                && track.Id = this.SelectedTrack.Value.Id
                            then
                                "selected-row"
                            else
                                "")

                        TemplateColumn'' {
                            title' "Artist"
                            ChildContent(fun (t: Track) -> fragment { t.Artist })
                        }

                        TemplateColumn'' {
                            title' "Title"
                            ChildContent(fun (t: Track) -> fragment { t.Title })
                        }

                        TemplateColumn'' {
                            title' "Duration"
                            ChildContent(fun (t: Track) -> fragment { t.DisplayDuration })
                        }
                    }
                }

            }

            FluentDialogFooter'' {
                FluentButton'' {
                    Appearance Appearance.Accent

                    disabled (
                        this.SelectedTrack.Value = Unchecked.defaultof<Track>
                        || this.IsDownloading.Value
                    )

                    OnClick(fun _ ->
                        task { this.PerformDownload this.SelectedTrack.Value |> Async.AwaitTask |> ignore })

                    "Download"
                }

                FluentButton'' {
                    Appearance Appearance.Neutral
                    disabled this.IsDownloading.Value
                    OnClick(fun _ -> task { this.Dialog.CancelAsync() |> Async.AwaitTask |> ignore })
                    "Cancel"
                }
            }
        }
