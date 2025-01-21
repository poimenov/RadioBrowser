#if INTERACTIVE

#r "nuget: Avalonia"
#r "nuget: Avalonia.Desktop"
#r "nuget: Avalonia.Themes.Fluent"
#r "nuget: Avalonia.FuncUI"
#r "nuget: FSharp.Control.TaskSeq"
#r "nuget: FluentIcons.Avalonia"
//#r "nuget: YoutubeExplode"
#r "nuget: YoutubeExplode, 6.4.3"
#r "nuget: FFMpegCore"
#r "nuget: AsyncImageLoader.Avalonia"

#endif

open System
open System.IO
open Avalonia
open Avalonia.Controls
open Avalonia.FuncUI
open Avalonia.FuncUI.DSL
open Avalonia.FuncUI.Hosts
open Avalonia.Layout
open FluentIcons.Avalonia
open System.Collections.ObjectModel
open YoutubeExplode.Search
open Avalonia.Data
open Avalonia.Input
open YoutubeExplode
open FFMpegCore
open FFMpegCore.Pipes
open FSharp.Control
open AsyncImageLoader

[<AutoOpen>]
module SymbolIcon =
    open Avalonia.FuncUI.Types
    open Avalonia.FuncUI.Builder
    open FluentIcons.Common

    let create (attrs: IAttr<SymbolIcon> list) : IView<SymbolIcon> = ViewBuilder.Create<SymbolIcon>(attrs)

    type SymbolIcon with
        static member symbol<'t when 't :> SymbolIcon>(value: Symbol) : IAttr<'t> =
            AttrBuilder<'t>
                .CreateProperty<Symbol>(SymbolIcon.SymbolProperty, value, ValueNone)


[<AbstractClass; Sealed>]
type Views =

    static member main() =
        Component(fun ctx ->
            let data = ctx.useState (ObservableCollection<VideoSearchResult>([]))
            let selectedItem = ctx.useState<Option<VideoSearchResult>> None
            let searchButtonEnabled = ctx.useState true
            let downloadButtonEnabled = ctx.useState true
            let searchText = ctx.useState ""

            let doSearch =
                async {
                    searchButtonEnabled.Set(false)

                    try
                        data.Current.Clear()
                        let youtube = YoutubeClient()
                        let results = youtube.Search.GetVideosAsync(searchText.Current)

                        do!
                            results
                            |> TaskSeq.iterAsync (fun x -> task { data.Current.Add(x) })
                            |> Async.AwaitTask
                    with ex ->
                        printfn "%A" ex

                    searchButtonEnabled.Set(true)
                }

            let doDownload =
                async {
                    match selectedItem.Current with
                    | None -> ()
                    | Some searchResult ->
                        downloadButtonEnabled.Set(false)

                        try
                            let youtube = YoutubeClient()
                            let! video = youtube.Videos.GetAsync(searchResult.Url).AsTask() |> Async.AwaitTask

                            let! streamManifest =
                                youtube.Videos.Streams.GetManifestAsync(video.Id).AsTask() |> Async.AwaitTask

                            let streamInfo =
                                streamManifest.GetAudioOnlyStreams()
                                |> Seq.maxBy (fun x -> x.Bitrate.BitsPerSecond)

                            let! stream = youtube.Videos.Streams.GetAsync(streamInfo).AsTask() |> Async.AwaitTask

                            let videoPath =
                                Path.Combine(
                                    Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
                                    $"{video.Title}.{streamInfo.Container.Name}"
                                )

                            do!
                                FFMpegArguments
                                    .FromPipeInput(new StreamPipeSource(stream))
                                    .OutputToFile(Path.ChangeExtension(videoPath, "mp3"), true)
                                    .ProcessAsynchronously()
                                |> Async.AwaitTask
                                |> Async.Ignore

                        with ex ->
                            printfn "%A" ex

                        downloadButtonEnabled.Set(true)
                }

            let getImage (thumbs: Common.Thumbnail seq) =
                let thumb = thumbs |> Seq.filter (fun x -> x.Resolution.Width = 120) |> Seq.last

                let btm =
                    ImageLoader.AsyncImageLoader.ProvideImageAsync(thumb.Url)
                    |> Async.AwaitTask
                    |> Async.RunSynchronously

                Image.create [ Image.source btm ]


            DockPanel.create
                [ DockPanel.children
                      [ StackPanel.create
                            [ StackPanel.orientation Orientation.Horizontal
                              StackPanel.dock Dock.Top
                              StackPanel.margin 4
                              StackPanel.children
                                  [ TextBox.create
                                        [ TextBox.margin 4
                                          TextBox.watermark "Search music"
                                          TextBox.width 700
                                          TextBox.onKeyDown (fun e ->
                                              if e.Key = Key.Enter then
                                                  Async.StartImmediate doSearch)
                                          TextBox.onTextChanged (fun e -> searchText.Set(e)) ]
                                    Button.create
                                        [ Button.content (
                                              SymbolIcon.create
                                                  [ SymbolIcon.width 24
                                                    SymbolIcon.height 24
                                                    SymbolIcon.symbol FluentIcons.Common.Symbol.Search ]
                                          )
                                          ToolTip.tip "Search"
                                          Button.isEnabled (
                                              searchButtonEnabled.Current
                                              && not (String.IsNullOrWhiteSpace(searchText.Current))
                                          )
                                          Button.onClick (fun _ -> Async.StartImmediate doSearch) ]
                                    Button.create
                                        [ Button.content (
                                              SymbolIcon.create
                                                  [ SymbolIcon.width 24
                                                    SymbolIcon.height 24
                                                    SymbolIcon.symbol FluentIcons.Common.Symbol.ArrowDownload ]
                                          )
                                          ToolTip.tip "Download"
                                          Button.isEnabled (
                                              selectedItem.Current.IsSome && downloadButtonEnabled.Current
                                          )
                                          Button.onClick (fun _ -> Async.StartImmediate doDownload) ] ] ]
                        DataGrid.create
                            [ DataGrid.dock Dock.Top
                              DataGrid.isReadOnly true
                              DataGrid.margin 4
                              DataGrid.items data.Current
                              DataGrid.onSelectedItemChanged (fun item ->
                                  (match box item with
                                   | null -> None
                                   | :? VideoSearchResult as i -> Some i
                                   | _ -> failwith "Something went horribly wrong!")
                                  |> selectedItem.Set)

                              DataGrid.columns
                                  [ DataGridTemplateColumn.create
                                        [ DataGridTemplateColumn.header ""
                                          DataGridTemplateColumn.width (DataGridLength(120.0))
                                          DataGridTemplateColumn.cellTemplate (
                                              DataTemplateView<_>.create (fun (data: VideoSearchResult) ->
                                                  getImage data.Thumbnails)
                                          ) ]
                                    DataGridTextColumn.create
                                        [ DataGridTextColumn.header "Author"
                                          DataGridTextColumn.width (DataGridLength(170.0))
                                          DataGridTextColumn.binding (Binding("Author")) ]
                                    DataGridTextColumn.create
                                        [ DataGridTextColumn.header "Title"
                                          DataGridTextColumn.width (DataGridLength(400.0))
                                          DataGridTextColumn.binding (Binding("Title")) ]
                                    DataGridTextColumn.create
                                        [ DataGridTextColumn.header "Duration"
                                          DataGridTextColumn.width (DataGridLength(100.0))
                                          DataGridTextColumn.binding (Binding "Duration") ] ] ] ] ])

type MainWindow() as this =
    inherit HostWindow()

    do
        base.Title <- "Youtube - Music Search/Download"
        base.Width <- 800.0
        base.Height <- 500.0
        this.Content <- Views.main ()


type App() =
    inherit Application()

    override this.Initialize() =
        this.Styles.Add(Themes.Fluent.FluentTheme())
        this.RequestedThemeVariant <- Styling.ThemeVariant.Dark
        this.Styles.Load "avares://Avalonia.Controls.DataGrid/Themes/Fluent.xaml"

    override this.OnFrameworkInitializationCompleted() =

        match this.ApplicationLifetime with
        | :? ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime as desktop ->
            desktop.MainWindow <- MainWindow()
            printfn "App running..."
        | _ -> ()

let app =
    AppBuilder
        .Configure<App>()
        .UsePlatformDetect()
        .StartWithClassicDesktopLifetime([||])
