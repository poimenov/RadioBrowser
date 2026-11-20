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

let private gridHeader (icon: Icon) (title: string) =
    div {
        class' "data-grid-header-content"

        FluentIcon'' {
            Value icon
            class' "data-grid-header-icon"
        }

        span { title }
    }

type DownloadPanel() =
    inherit FunComponent()

    let gridRef: ElementReference ref = ref Unchecked.defaultof<ElementReference>
    let defaultTimeout = 5.0

    [<Inject>]
    member val Logger: ILogger<DownloadPanel> = Unchecked.defaultof<_> with get, set

    [<Inject>]
    member val Localizer: IStringLocalizer<SharedResources> = Unchecked.defaultof<_> with get, set

    [<Inject>]
    member val NotificationService: INotificationService = Unchecked.defaultof<_> with get, set

    [<Inject>]
    member val Options: IOptions<AppSettings> = Unchecked.defaultof<_> with get, set

    [<CascadingParameter>]
    member val DialogInstance = Unchecked.defaultof<IDialogInstance> with get, set

    member this.Content =
        this.DialogInstance.Options.Data :?> DialogData<DownloadPanelData>

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
                this.NotificationService.ShowErrorToastAsync result.Message |> ignore

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
                let successMessage =
                    String.Format(
                        string (this.Localizer["DownloadSuccessMessage"]),
                        track.FileName,
                        this.Options.Value.DownloadsFolderPath
                    )

                this.NotificationService.ShowSuccessToastAsync successMessage |> ignore
            else
                this.Logger.LogError(result.Exception, result.Message)
                this.NotificationService.ShowErrorToastAsync result.Message |> ignore

            this.IsDownloading.Publish false |> ignore
            this.StateHasChanged()
        }

    override this.OnInitializedAsync() : Tasks.Task =
        this.PerfomSearch this.Content.Data.Search |> ignore
        base.OnInitializedAsync()

    override this.Render() : NodeRenderFragment =
        let body = adapt {
            let tracks = this.SearchResults.Value.AsQueryable<Track>()

            div {
                class' "download-panel-content"
                FluentStack'' {
                    Orientation Orientation.Horizontal
                    VerticalAlignment VerticalAlignment.Center
                    HorizontalGap "4"
                    style' "width:100%;margin-bottom:8px;flex-shrink:0;"

                    input {
                        type' "search"
                        class' "search-input"
                        placeholder (string (this.Localizer["SearchPlaceholder"]))
                        value this.Content.Data.Search
                        oninput (fun args -> this.Content.Data.Search <- string args.Value)

                        onkeyup (fun args ->
                            task {
                                if args.Key = "Enter" then
                                    this.PerfomSearch this.Content.Data.Search |> ignore
                            })

                        style' "flex-grow: 1;"
                    }

                    FluentButton'' {
                        Appearance ButtonAppearance.Primary
                        OnClick(fun _ -> this.PerfomSearch this.Content.Data.Search)

                        this.Localizer["Search"]
                    }
                }

                div {
                    class' "download-grid-scroll"

                    FluentDataGrid'' {
                        type' typeof<Track>
                        ShowHover true
                        Items tracks
                        EmptyContent(string (this.Localizer["NoResultsFound"]))
                        GenerateHeader DataGridGeneratedHeaderType.Sticky

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
                            title' (string (this.Localizer["Artist"]))
                            HeaderCellTitleTemplate(fun _ ->
                                gridHeader
                                    (Icons.Regular.Size20.PersonNote())
                                    (string (this.Localizer["Artist"])))

                            Tooltip true
                            TooltipText(fun (t: Track) -> t.Artist)
                            ChildContent(fun (t: Track) -> fragment { t.Artist })
                        }

                        TemplateColumn'' {
                            title' (string (this.Localizer["Title"]))
                            HeaderCellTitleTemplate(fun _ ->
                                gridHeader
                                    (Icons.Regular.Size20.MusicNote1())
                                    (string (this.Localizer["Title"])))

                            Tooltip true
                            TooltipText(fun (t: Track) -> t.Title)
                            ChildContent(fun (t: Track) -> fragment { t.Title })
                        }

                        TemplateColumn'' {
                            title' (string (this.Localizer["Duration"]))
                            HeaderCellTitleTemplate(fun _ ->
                                gridHeader
                                    (Icons.Regular.Size20.Timer())
                                    (string (this.Localizer["Duration"])))

                            ChildContent(fun (t: Track) -> fragment { t.DisplayDuration })
                        }
                    }
                }

            }

            div {
                class' "download-panel-actions"
                FluentButton'' {
                    Appearance ButtonAppearance.Primary

                    disabled (
                        this.SelectedTrack.Value = Unchecked.defaultof<Track>
                        || this.IsDownloading.Value
                    )

                    OnClick(fun _ ->
                        task { this.PerformDownload this.SelectedTrack.Value |> Async.AwaitTask |> ignore })

                    this.Localizer["Download"]
                }

                FluentButton'' {
                    Appearance ButtonAppearance.Default
                    disabled this.IsDownloading.Value
                    OnClick(fun _ -> task { this.DialogInstance.CancelAsync() |> Async.AwaitTask |> ignore })
                    this.Localizer["Cancel"]
                }
            }
        }

        FluentDialogBody'' {
            class' "download-panel-dialog-body"
            body
        }
