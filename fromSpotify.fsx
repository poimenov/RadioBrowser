#if INTERACTIVE

#r "nuget: Avalonia"
#r "nuget: Avalonia.Desktop"
#r "nuget: Avalonia.Themes.Fluent"
#r "nuget: Avalonia.FuncUI"
#r "nuget: FSharp.Data"
#r "nuget: FluentIcons.Avalonia"
#r "nuget: SpotifyExplode"
#r "nuget: LibVLCSharp"
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
open Avalonia.Input
open FSharp.Control
open FSharp.Data
open AsyncImageLoader
open SpotifyExplode
open SpotifyExplode.Search
open System.Text.RegularExpressions
open LibVLCSharp.Shared
open Avalonia.Media
open Avalonia.Media.Imaging

// [<AutoOpen>]
// module WindowIcon =
//     open Avalonia.FuncUI.Types
//     open Avalonia.FuncUI.Builder

//     let create (attrs: IAttr<WindowIcon> list) : IView<WindowIcon> = ViewBuilder.Create<WindowIcon>(attrs)

//     // type WindowIcon with
//     //     static member icon<'t when 't :> WindowIcon>(value: string) : IAttr<'t> =
//     //         AttrBuilder<'t>
//     //             .CreateProperty<string>(WindowIcon.icon, value, ValueNone)

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
            let data = ctx.useState (ObservableCollection<TrackSearchResult>([]))
            let selectedItem = ctx.useState<Option<TrackSearchResult>> None
            let searchButtonEnabled = ctx.useState true
            let downloadButtonEnabled = ctx.useState true
            let playEnabled = ctx.useState true
            let isPlaying = ctx.useState false
            let searchText = ctx.useState ""
            let libVlc = ctx.useState (new LibVLC())

            let getPlayer =
                let _player = new MediaPlayer(libVlc.Current)

                _player.EndReached.Add(fun _ ->
                    isPlaying.Set(false)
                    _player.Media.Dispose()
                    _player.Media <- null)

                _player

            let player = ctx.useState (getPlayer)

            let doSearch =
                async {
                    searchButtonEnabled.Set(false)

                    try
                        data.Current.Clear()
                        let client = SpotifyClient()

                        let! results = client.Search.GetTracksAsync(searchText.Current).AsTask() |> Async.AwaitTask

                        results |> Seq.iter (fun x -> data.Current.Add(x))
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
                            let client = SpotifyClient()
                            let! url = client.Tracks.GetDownloadUrlAsync(searchResult.Id).AsTask() |> Async.AwaitTask
                            printfn "%A" url

                            let path =
                                Path.Combine(
                                    Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
                                    Regex.Replace(
                                        $"{searchResult.Artists[0].Name} {searchResult.Title}",
                                        "[\\/:*?\"<>|]*",
                                        ""
                                    )
                                    + ".mp3"
                                )

                            let! response = Http.AsyncRequest(url)

                            match response.Body with
                            | Binary bytes -> File.WriteAllBytesAsync(path, bytes) |> Async.AwaitTask |> ignore
                            | Text(_) -> ignore ()

                        with ex ->
                            printfn "%A" ex

                        downloadButtonEnabled.Set(true)
                }

            let play =
                async {
                    match selectedItem.Current with
                    | None -> ()
                    | Some track ->
                        let client = SpotifyClient()
                        playEnabled.Set(false)

                        try
                            let! url = client.Tracks.GetDownloadUrlAsync(track.Id).AsTask() |> Async.AwaitTask
                            isPlaying.Set(player.Current.Play(new Media(libVlc.Current, Uri(url))))
                        with ex ->
                            printfn "%A" ex
                            isPlaying.Set(false)

                        playEnabled.Set(true)
                }

            let playStop =
                async {
                    if isPlaying.Current then
                        player.Current.Stop()
                        isPlaying.Set(false)
                    else
                        play |> Async.Start
                }

            let getItem (item: TrackSearchResult) =
                let t = TimeSpan.FromMilliseconds(float item.DurationMs)
                let duration = String.Format("{0:D2}:{1:D2}", t.Minutes, t.Seconds)
                let artist = item.Artists |> Seq.map (fun x -> x.Name) |> String.concat ", "
                let thumb = item.Album.Images |> Seq.minBy (fun x -> x.Width.Value)

                let btm =
                    ImageLoader.AsyncImageLoader.ProvideImageAsync(thumb.Url)
                    |> Async.AwaitTask
                    |> Async.RunSynchronously

                let btm =
                    ImageLoader.AsyncImageLoader.ProvideImageAsync(thumb.Url)
                    |> Async.AwaitTask
                    |> Async.RunSynchronously

                StackPanel.create
                    [ StackPanel.orientation Orientation.Horizontal
                      StackPanel.dock Dock.Top
                      //StackPanel.margin 4
                      StackPanel.children
                          [ Image.create [ Image.source btm ]
                            StackPanel.create
                                [ StackPanel.orientation Orientation.Vertical
                                  StackPanel.width 620
                                  StackPanel.margin (15, 4, 15, 4)
                                  StackPanel.children
                                      [ TextBlock.create
                                            [ TextBlock.text artist
                                              TextBlock.fontWeight FontWeight.Bold
                                              TextBlock.fontSize 16.0 ]
                                        TextBlock.create
                                            [ TextBlock.text item.Album.Name
                                              TextBlock.fontSize 14.0
                                              TextBlock.fontStyle FontStyle.Italic ]
                                        TextBlock.create [ TextBlock.text item.Title; TextBlock.fontSize 14.0 ] ] ]
                            TextBlock.create [ TextBlock.text duration ] ] ]


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
                                          TextBox.width 655
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
                                                    SymbolIcon.symbol (
                                                        if isPlaying.Current then
                                                            FluentIcons.Common.Symbol.Stop
                                                        else
                                                            FluentIcons.Common.Symbol.Play
                                                    ) ]
                                          )
                                          ToolTip.tip (if isPlaying.Current then "Stop" else "Play")
                                          Button.isEnabled (selectedItem.Current.IsSome && playEnabled.Current)
                                          Button.onClick (fun _ -> Async.StartImmediate playStop) ]
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
                        ListBox.create
                            [ ListBox.dock Dock.Top
                              ListBox.dataItems data.Current
                              ListBox.onSelectedItemChanged (fun item ->
                                  (match box item with
                                   | null -> None
                                   | :? TrackSearchResult as i -> Some i
                                   | _ -> failwith "Something went horribly wrong!")
                                  |> selectedItem.Set)
                              ListBox.itemTemplate (
                                  DataTemplateView<_>.create (fun (data: TrackSearchResult) -> getItem data)
                              )
                              ListBox.margin 4 ] ] ])

type MainWindow() as this =
    inherit HostWindow()

    do
        base.Title <- "Spotify - Music Search/Download"
        base.Width <- 800.0
        base.Height <- 500.0
        this.Content <- Views.main ()

type App() =
    inherit Application()

    override this.Initialize() =
        this.Styles.Add(Themes.Fluent.FluentTheme())
        this.RequestedThemeVariant <- Styling.ThemeVariant.Dark

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
