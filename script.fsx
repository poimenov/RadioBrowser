#if INTERACTIVE

#r "nuget: Avalonia"
#r "nuget: Avalonia.Desktop"
#r "nuget: Avalonia.Themes.Fluent"
#r "nuget: Avalonia.FuncUI"
#r "nuget: FSharp.Data"

#endif

//https://funcui.avaloniaui.net/

open Avalonia
open Avalonia.Controls
open Avalonia.FuncUI
open Avalonia.FuncUI.DSL
open Avalonia.FuncUI.Hosts
open System.Collections.ObjectModel
open Avalonia.Data
open Avalonia.Layout
open System.IO
open FSharp.Data
open System

type Track(id, title, artistId, artist, duration, downloadUrl) =
    member val Id = id with get, set
    member val Title = title with get, set
    member val Artist = artist with get, set
    member val ArtistId = artistId with get, set
    member val Duration = duration with get, set
    member val DownloadUrl = downloadUrl with get, set

[<AbstractClass; Sealed>]
type Views =

    static member main() =
        Component(fun ctx ->
            let data = ctx.useState (ObservableCollection<Track>([]))
            let selectedItem = ctx.useState<Option<Track>> None
            let searchButtonEnabled = ctx.useState true
            let downloadButtonEnabled = ctx.useState true
            let searchText = ctx.useState ""
            let baseUri = new Uri("https://m.z3.fm")

            let getAsyncHtmlPage (url: string) =
                async { return! HtmlDocument.AsyncLoad(url) }

            let getHtmlItems (page: HtmlDocument) =
                page.Descendants [ "li" ]
                |> Seq.filter (fun x -> x.AttributeValue("class") = "tracks-item")

            let getTrack (htmlItem: HtmlNode) =
                let id = htmlItem.AttributeValue("data-id")
                let title = htmlItem.AttributeValue("data-title")
                let artistId = htmlItem.AttributeValue("data-artist-id")
                let artist = htmlItem.AttributeValue("data-artist")

                let duration =
                    htmlItem.Descendants [ "div" ]
                    |> Seq.filter (fun x -> x.AttributeValue("class") = "tracks-time")
                    |> Seq.head
                    |> fun x -> x.InnerText()

                let downloadUrl = "/download/" + id
                Track(id, title, artistId, artist, duration, downloadUrl)

            let doSearch =
                async {
                    let uri = new Uri(baseUri, "mp3/search?keywords=" + searchText.Current)
                    searchButtonEnabled.Set(false)

                    try
                        let! page = getAsyncHtmlPage (uri.AbsoluteUri)

                        let tracks =
                            getHtmlItems page |> Seq.map getTrack |> Seq.toList |> ObservableCollection

                        data.Set(tracks)
                    with ex ->
                        printfn "%A" ex

                    searchButtonEnabled.Set(true)
                }

            let doDownload =
                async {
                    match selectedItem.Current with
                    | None -> ()
                    | Some track ->
                        let uri = new Uri(baseUri, track.DownloadUrl)
                        downloadButtonEnabled.Set(false)

                        try
                            let! response = Http.AsyncRequest(uri.AbsoluteUri)
                            //response.Headers |> Seq.iter (fun x -> printfn "%A" x)

                            match response.Body with
                            | Binary bytes ->
                                File.WriteAllBytes(
                                    Path.Combine(
                                        Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
                                        response.Headers.["Content-Disposition"].Split("filename=").[1].Split(",").[0]
                                    ),
                                    bytes
                                )
                            | Text(_) -> ignore ()
                        with ex ->
                            printfn "%A" ex

                        downloadButtonEnabled.Set(true)
                }

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
                                          TextBox.width 600
                                          TextBox.onTextChanged (fun e -> searchText.Set(e)) ]
                                    Button.create
                                        [ Button.content "Search"
                                          Button.width 90
                                          Button.isEnabled searchButtonEnabled.Current
                                          Button.horizontalContentAlignment HorizontalAlignment.Center
                                          Button.onClick (fun _ -> Async.StartImmediate doSearch) ]
                                    Button.create
                                        [ Button.content "Download"
                                          Button.width 90
                                          Button.isEnabled (
                                              selectedItem.Current.IsSome && downloadButtonEnabled.Current
                                          )
                                          Button.horizontalContentAlignment HorizontalAlignment.Center
                                          Button.onClick (fun _ -> Async.StartImmediate doDownload) ] ] ]
                        DataGrid.create
                            [ DataGrid.dock Dock.Top
                              DataGrid.isReadOnly true
                              DataGrid.items data.Current
                              DataGrid.onSelectedItemChanged (fun item ->
                                  (match box item with
                                   | null -> None
                                   | :? Track as i -> Some i
                                   | _ -> failwith "Something went horribly wrong!")
                                  |> selectedItem.Set)

                              DataGrid.columns
                                  [ DataGridTextColumn.create
                                        [ DataGridTextColumn.header "Artist"
                                          DataGridTextColumn.width (DataGridLength(200.0))
                                          DataGridTextColumn.binding (Binding("Artist")) ]
                                    DataGridTextColumn.create
                                        [ DataGridTextColumn.header "Title"
                                          DataGridTextColumn.width (DataGridLength(490.0))
                                          DataGridTextColumn.binding (Binding("Title")) ]
                                    DataGridTextColumn.create
                                        [ DataGridTextColumn.header "Duration"
                                          DataGridTextColumn.width (DataGridLength(100.0))
                                          DataGridTextColumn.binding (Binding "Duration") ] ] ] ] ])

type MainWindow() as this =
    inherit HostWindow()

    do
        base.Title <- "z3.fm - Music Search/Download"
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
