#if INTERACTIVE

#r "nuget: Avalonia"
#r "nuget: Avalonia.Desktop"
#r "nuget: Avalonia.Themes.Fluent"
#r "nuget: Avalonia.FuncUI"
#r "nuget: FluentIcons.Avalonia"
#r "nuget: LibVLCSharp"
#r "nuget: RadioBrowser"
#r "nuget: AsyncImageLoader.Avalonia"
#r "nuget: PSC.CSharp.Library.CountryData"
#r "nuget: Avalonia.Svg"

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
open AsyncImageLoader
open LibVLCSharp.Shared
open RadioBrowser
open RadioBrowser.Models
open Avalonia.Media.Imaging
open Avalonia.Media
open Avalonia.Controls.Templates
open Avalonia.Styling
open FluentIcons.Common
open PSC.CSharp.Library.CountryData
open Avalonia.Svg
open System.Diagnostics
open System.Runtime.InteropServices
open System.Text.Json

[<Serializable>]
type public Station
    (
        id: Guid,
        name: string,
        url: Uri,
        imageUrl: string,
        countryCode: string,
        languages: string,
        tags: string,
        codec: string,
        bitrate: string,
        isFavorite: bool
    ) =
    member val Id = id with get, set
    member val Name = name with get, set
    member val Url = url with get, set
    member val ImageUrl = imageUrl with get, set
    member val CountryCode = countryCode with get, set
    member val Languages = languages with get, set
    member val Tags = tags with get, set
    member val Codec = codec with get, set
    member val Bitrate = bitrate with get, set
    member val IsFavorite = isFavorite with get, set

type Platform =
    | Windows
    | Linux
    | MacOS
    | Unknown

[<AutoOpen>]
module SymbolIcon =
    open Avalonia.FuncUI.Types
    open Avalonia.FuncUI.Builder

    let create (attrs: IAttr<SymbolIcon> list) : IView<SymbolIcon> = ViewBuilder.Create<SymbolIcon>(attrs)

    type SymbolIcon with
        static member symbol<'t when 't :> SymbolIcon>(value: Symbol) : IAttr<'t> =
            AttrBuilder<'t>
                .CreateProperty<Symbol>(SymbolIcon.SymbolProperty, value, ValueNone)

        static member iconVariant<'t when 't :> SymbolIcon>(value: IconVariant) : IAttr<'t> =
            AttrBuilder<'t>
                .CreateProperty<IconVariant>(SymbolIcon.IconVariantProperty, value, ValueNone)

[<AbstractClass; Sealed>]
type Views =

    static member main() =
        Component(fun ctx ->
            let items = ctx.useState<ObservableCollection<Station>> (ObservableCollection())
            let favItems = ctx.useState<ObservableCollection<Station>> (ObservableCollection())

            let countries =
                ctx.useState<ObservableCollection<NameAndCount>> (ObservableCollection())

            let selectedItem = ctx.useState<Option<Station>> None
            let selectedCountry = ctx.useState<Option<NameAndCount>> None
            let searchButtonEnabled = ctx.useState false
            let playEnabled = ctx.useState false
            let isPlaying = ctx.useState false
            let searchText = ctx.useState ""
            let nowPlaying = ctx.useState<string option> None
            //https://wiki.videolan.org/VLC_command-line_help/
            let libVlc =
                ctx.useState (new LibVLC([| "--network-caching=3000"; "--sout-livehttp-caching" |]))

            let countryHelper = ctx.useState (new CountryHelper())

            let chunk = 100u
            let maxCount = 500

            let getPlatform =
                if RuntimeInformation.IsOSPlatform(OSPlatform.Windows) then
                    Windows
                elif RuntimeInformation.IsOSPlatform(OSPlatform.Linux) then
                    Linux
                elif RuntimeInformation.IsOSPlatform(OSPlatform.OSX) then
                    MacOS
                else
                    Unknown

            let runCommand (command: string, arguments: string) =
                let psi = new ProcessStartInfo(command)
                psi.RedirectStandardOutput <- false
                psi.UseShellExecute <- false
                psi.CreateNoWindow <- false
                psi.Arguments <- arguments

                let p = new Process()
                p.StartInfo <- psi
                p.Start() |> ignore

            let openUrl (url: string) =
                match getPlatform with
                | Windows -> runCommand ("cmd", $"/c start {url}")
                | Linux -> runCommand ("xdg-open", url)
                | MacOS -> runCommand ("open", url)
                | _ -> ()

            let searchInYoutube (searchText: string option) =
                match searchText with
                | None -> ()
                | Some s ->
                    openUrl (
                        "https://www.youtube.com/results?search_query="
                        + System.Web.HttpUtility.UrlEncode(s)
                    )


            let convert (item: StationInfo, isFavorite: bool) =
                let imageUrl =
                    if item.Favicon <> null then
                        item.Favicon.AbsoluteUri
                    else
                        null

                let languages = item.Language |> String.concat ", "
                let tags = item.Tags |> String.concat ", "

                new Station(
                    item.StationUuid,
                    item.Name,
                    item.Url,
                    imageUrl,
                    item.CountryCode,
                    languages,
                    tags,
                    item.Codec.ToString(),
                    item.Bitrate.ToString(),
                    isFavorite
                )

            let getDefaultStations =
                async {
                    let client = RadioBrowserClient()
                    return! client.Stations.GetByVotesAsync(chunk) |> Async.AwaitTask
                }

            let favoritesPath = Path.Combine(__SOURCE_DIRECTORY__, "favorites.json")

            let getFavStations =
                async {
                    if File.Exists(favoritesPath) then
                        let! jsonFavorites = File.ReadAllTextAsync(favoritesPath) |> Async.AwaitTask

                        let favorites = JsonSerializer.Deserialize<Station list> jsonFavorites

                        return favorites
                    else
                        return Seq.empty |> Seq.toList
                }

            let getCountries =
                async {
                    let client = RadioBrowserClient()
                    let! countryCodes = client.Lists.GetCountriesCodesAsync() |> Async.AwaitTask

                    let codes =
                        countryHelper.Current.GetCountryData()
                        |> Seq.map (fun x -> x.CountryShortCode)
                        |> Set.ofSeq

                    let result = countryCodes |> Seq.filter (fun x -> codes.Contains(x.Name))
                    let empty = new NameAndCount()
                    empty.Name <- ""
                    empty.Stationcount <- 0u
                    return result |> Seq.insertAt 0 empty
                }

            let getPlayer =
                let _player = new MediaPlayer(libVlc.Current)

                _player.EncounteredError.Add(fun _ ->
                    printfn "EncounteredError"
                    _player.Stop()
                    _player.Media.Dispose()
                    _player.Media <- null)

                _player.Playing.Add(fun _ ->
                    isPlaying.Set(true)
                    playEnabled.Set(true))

                _player.Stopped.Add(fun _ ->
                    isPlaying.Set(false)
                    playEnabled.Set(true))

                _player

            let player = ctx.useState (getPlayer)

            let isStationFavorite (item: Station) =
                (favItems.Current |> Seq.exists (fun x -> x.Id = item.Id))

            let isOptionStationFavorite (item: Option<Station>) =
                match item with
                | None -> false
                | Some item -> isStationFavorite item

            ctx.useEffect (
                handler =
                    (fun _ ->
                        ctx.control.Unloaded.Add(fun _ ->
                            let favText =
                                favItems.Current |> Seq.toList |> JsonSerializer.Serialize<Station list>

                            File.WriteAllText(favoritesPath, favText)
                            player.Current.Stop()
                            player.Current.Dispose()
                            libVlc.Current.Dispose())

                        Core.Initialize()

                        Async.StartWithContinuations(
                            getFavStations,
                            (fun (stations) ->
                                (stations |> Seq.iter (fun x -> favItems.Current.Add(x))

                                 Async.StartWithContinuations(
                                     getDefaultStations,
                                     (fun (stations) ->
                                         (stations
                                          |> Seq.iter (fun x ->
                                              let item = convert (x, false)
                                              item.IsFavorite <- isStationFavorite item
                                              items.Current.Add(item)))),
                                     (fun ex -> (printfn "%A" ex)),
                                     (fun _ -> ())
                                 ))),
                            (fun ex -> (printfn "%A" ex)),
                            (fun _ -> ())
                        )

                        Async.StartWithContinuations(
                            getCountries,
                            (fun (_countries) -> (_countries |> Seq.iter (fun x -> countries.Current.Add(x)))),
                            (fun ex -> (printfn "%A" ex)),
                            (fun _ -> ())
                        )),
                triggers = [ EffectTrigger.AfterInit ]
            )

            let getSearchOptions (offset: Nullable<uint32>) =
                let opt = new AdvancedSearchOptions()
                opt.Limit <- chunk
                opt.Offset <- offset

                if not (String.IsNullOrEmpty(searchText.Current)) then
                    opt.Name <- searchText.Current

                if
                    selectedCountry.Current.IsSome
                    && selectedCountry.Current.Value.Stationcount > 0u
                then
                    let country =
                        countryHelper.Current.GetCountryByCode(selectedCountry.Current.Value.Name)

                    opt.Country <- country.CountryName

                opt

            let rec doSearchWithOptions (options: AdvancedSearchOptions) =
                async {
                    try
                        let client = RadioBrowserClient()
                        let! results = client.Search.AdvancedAsync(options) |> Async.AwaitTask

                        results
                        |> Seq.iter (fun x ->
                            let item = convert (x, false)
                            item.IsFavorite <- isStationFavorite item
                            items.Current.Add(item))

                        if results.Count = Convert.ToInt32(chunk) && items.Current.Count < maxCount then
                            options.Offset <- options.Offset.Value + chunk
                            doSearchWithOptions (options) |> Async.Start
                        else
                            searchButtonEnabled.Set(true)
                            printfn $"Finished. Count = {items.Current.Count}"
                    with ex ->
                        printfn "%A" ex
                }

            let doSearch =
                async {
                    searchButtonEnabled.Set(false)

                    if not isPlaying.Current then
                        playEnabled.Set(false)

                    try
                        items.Current.Clear()
                        let options = getSearchOptions (Nullable.op_Implicit (0u))

                        if (String.IsNullOrEmpty(options.Name) && String.IsNullOrEmpty(options.Country)) then
                            let client = RadioBrowserClient()
                            let! results = client.Stations.GetByVotesAsync(chunk) |> Async.AwaitTask

                            results
                            |> Seq.iter (fun x ->
                                let item = convert (x, false)
                                item.IsFavorite <- isStationFavorite item
                                items.Current.Add(item))

                            searchButtonEnabled.Set(true)
                        else
                            doSearchWithOptions (options) |> Async.Start
                    with ex ->
                        printfn "%A" ex
                }

            let play =
                async {
                    match selectedItem.Current with
                    | None -> ()
                    | Some track ->
                        playEnabled.Set(false)

                        try
                            nowPlaying.Set None
                            let media = new Media(libVlc.Current, selectedItem.Current.Value.Url)
                            let! result = media.Parse(MediaParseOptions.ParseNetwork) |> Async.AwaitTask

                            if result = MediaParsedStatus.Done then
                                if media.SubItems.Count = 0 then
                                    player.Current.Play(media) |> ignore
                                else
                                    player.Current.Play(media.SubItems.Item(0)) |> ignore

                                printfn "Playing Url: %A" player.Current.Media.Mrl

                                media.MetaChanged.Add(fun e ->
                                    if e.MetadataType = MetadataType.NowPlaying then
                                        nowPlaying.Set(Some(media.Meta(e.MetadataType)))

                                    printfn $"{e.MetadataType}: {media.Meta(e.MetadataType)}")
                            else
                                playEnabled.Set(true)
                                printfn "Url: %A MediaParseStatus: %A" selectedItem.Current.Value.Url result
                        with ex ->
                            printfn "%A" ex
                }

            let playStop =
                async {
                    if player.Current.IsPlaying then
                        playEnabled.Set(false)
                        player.Current.Stop()
                        player.Current.Media.Dispose()
                        player.Current.Media <- null
                    else
                        play |> Async.Start
                }

            let getSvgImageBycountryCode (countryCode: string) =
                let svgStart =
                    "<svg xmlns=\"http://www.w3.org/2000/svg\" xmlns:xlink=\"http://www.w3.org/1999/xlink\" id=\"flag-icons-ad\" viewBox=\"0 0 640 480\">"

                let svgEnd = "</svg>"

                let xml =
                    if countryCode <> String.Empty then
                        svgStart
                        + countryHelper.Current.GetFlagByCountryCode(countryCode, SVGImages.FlagType.Wide)
                        + svgEnd
                    else
                        svgStart + svgEnd

                let svgImage = new SvgImage()

                try
                    svgImage.Source <- SvgSource.LoadFromSvg(xml)
                with ex ->
                    printfn "%A" countryCode
                    printfn "%A" ex

                svgImage

            let getItem (item: Station, textWidth: double) =
                let defaultImg = new Bitmap(Path.Combine(__SOURCE_DIRECTORY__, "img/radio.png"))

                let img =
                    async {
                        if item.ImageUrl = null then
                            return defaultImg
                        else
                            return! ImageLoader.AsyncImageLoader.ProvideImageAsync(item.ImageUrl) |> Async.AwaitTask
                    }

                let svgImage = getSvgImageBycountryCode item.CountryCode

                let countryName =
                    if item.CountryCode <> String.Empty then
                        countryHelper.Current.GetCountryByCode(item.CountryCode).CountryName
                    else
                        String.Empty

                Border.create
                    [ Border.padding 5.
                      Border.margin 0.
                      Border.child (
                          StackPanel.create
                              [ StackPanel.orientation Orientation.Horizontal
                                StackPanel.horizontalAlignment HorizontalAlignment.Stretch
                                StackPanel.useLayoutRounding true
                                StackPanel.children
                                    [ Image.create
                                          [ Image.width 60
                                            Image.height 60
                                            Image.init (fun x ->
                                                Async.StartWithContinuations(
                                                    img,
                                                    (fun b -> x.Source <- b),
                                                    (fun _ -> x.Source <- defaultImg),
                                                    (fun _ -> x.Source <- defaultImg)
                                                )) ]
                                      StackPanel.create
                                          [ StackPanel.orientation Orientation.Vertical
                                            StackPanel.margin 5
                                            StackPanel.children
                                                [ TextBlock.create
                                                      [ TextBlock.text item.Name
                                                        TextBlock.fontSize 16.0
                                                        TextBlock.textTrimming TextTrimming.CharacterEllipsis
                                                        TextBlock.textWrapping TextWrapping.NoWrap
                                                        TextBlock.width textWidth
                                                        TextBlock.fontWeight FontWeight.Bold ]
                                                  StackPanel.create
                                                      [ StackPanel.orientation Orientation.Horizontal
                                                        StackPanel.children
                                                            [ Image.create
                                                                  [ Image.source svgImage
                                                                    Image.tip countryName
                                                                    Image.margin (2, 0, 6, 0)
                                                                    Image.width 22
                                                                    Image.height 16 ]
                                                              SymbolIcon.create
                                                                  [ SymbolIcon.symbol Symbol.Star
                                                                    SymbolIcon.width 16
                                                                    SymbolIcon.height 16
                                                                    SymbolIcon.margin (2, 0, 2, 0)
                                                                    SymbolIcon.iconVariant (
                                                                        if isStationFavorite (item) then
                                                                            IconVariant.Filled
                                                                        else
                                                                            IconVariant.Regular
                                                                    ) ]
                                                              TextBlock.create
                                                                  [ TextBlock.text
                                                                        $"{item.Codec} : {item.Bitrate} kbps {item.Languages}"
                                                                    TextBlock.textTrimming
                                                                        TextTrimming.CharacterEllipsis
                                                                    TextBlock.textWrapping TextWrapping.NoWrap
                                                                    TextBlock.width (textWidth - 50.0)
                                                                    TextBlock.fontSize 14.0 ] ] ]
                                                  TextBlock.create
                                                      [ TextBlock.text item.Tags
                                                        TextBlock.textTrimming TextTrimming.CharacterEllipsis
                                                        TextBlock.textWrapping TextWrapping.NoWrap
                                                        TextBlock.width textWidth
                                                        TextBlock.fontSize 12.0 ] ] ] ] ]
                      ) ]

            let getSelectedItem (item: Option<Station>) =
                match item with
                | None ->
                    Border.create
                        [ Border.child (TextBlock.create [ TextBlock.margin 20; TextBlock.text "No station selected" ]) ]
                | Some track -> getItem (track, 592.0)

            let getCountryItem (item: NameAndCount) =
                let count =
                    if item.Stationcount = 0u then
                        ""
                    else
                        item.Stationcount.ToString()

                let emptyCountry = new Country()
                emptyCountry.CountryName <- String.Empty

                emptyCountry.CountryFlag <- countryHelper.Current.GetFlagByCountryCode("ru", SVGImages.FlagType.Square)

                emptyCountry.CountryShortCode <- String.Empty

                let country =
                    if item.Stationcount <> 0u then
                        countryHelper.Current.GetCountryByCode(item.Name)
                    else
                        emptyCountry

                let svgImage = getSvgImageBycountryCode item.Name

                StackPanel.create
                    [ StackPanel.orientation Orientation.Horizontal
                      StackPanel.children
                          [ Image.create
                                [ Image.source svgImage
                                  Image.margin (2, 0, 6, 0)
                                  Image.width 22
                                  Image.height 16 ]
                            TextBlock.create
                                [ TextBlock.text country.CountryName
                                  TextBlock.textTrimming TextTrimming.CharacterEllipsis
                                  TextBlock.textWrapping TextWrapping.NoWrap
                                  TextBlock.width 240 ]
                            TextBlock.create
                                [ TextBlock.text (count)
                                  TextBlock.width 50
                                  TextBlock.textAlignment TextAlignment.Right ] ] ]

            let getSearchPanel =
                Grid.create
                    [ Grid.row 0
                      Grid.columnDefinitions "371, *, Auto"
                      Grid.children
                          [ ComboBox.create
                                [ Grid.column 0
                                  ComboBox.horizontalAlignment HorizontalAlignment.Stretch
                                  ComboBox.verticalAlignment VerticalAlignment.Stretch
                                  ComboBox.margin 4
                                  ComboBox.dataItems countries.Current
                                  ComboBox.itemTemplate (
                                      DataTemplateView<_>.create (fun (data: NameAndCount) -> getCountryItem data)
                                  )
                                  ComboBox.onSelectedItemChanged (fun item ->
                                      (match box item with
                                       | null -> None
                                       | :? NameAndCount as i -> Some i
                                       | _ -> failwith "Something went horribly wrong!")
                                      |> selectedCountry.Set

                                      let isCountrySelected =
                                          selectedCountry.Current.IsSome
                                          && selectedCountry.Current.Value.Stationcount > 0u

                                      if isCountrySelected && not searchButtonEnabled.Current then
                                          searchButtonEnabled.Set(true)
                                      elif
                                          not isCountrySelected
                                          && searchButtonEnabled.Current
                                          && String.IsNullOrWhiteSpace(searchText.Current)
                                      then
                                          searchButtonEnabled.Set(false)) ]
                            TextBox.create
                                [ Grid.column 1
                                  TextBox.margin (1, 4, 1, 4)
                                  TextBox.watermark "Search radio stations"
                                  TextBox.horizontalAlignment HorizontalAlignment.Stretch
                                  TextBox.onKeyDown (fun e ->
                                      if e.Key = Key.Enter then
                                          let textBox = e.Source :?> TextBox
                                          searchText.Set(textBox.Text)
                                          searchButtonEnabled.Set(false)
                                          Async.StartImmediate doSearch)
                                  TextBox.onTextChanged (fun e ->
                                      (if not (String.IsNullOrWhiteSpace(e)) && not searchButtonEnabled.Current then
                                           searchButtonEnabled.Set(true)
                                       elif
                                           String.IsNullOrWhiteSpace(e)
                                           && searchButtonEnabled.Current
                                           && selectedCountry.Current.IsNone
                                       then
                                           searchButtonEnabled.Set(false))) ]
                            Button.create
                                [ Grid.column 2
                                  Button.margin 4
                                  Button.content (
                                      SymbolIcon.create
                                          [ SymbolIcon.width 24
                                            SymbolIcon.height 24
                                            SymbolIcon.symbol Symbol.Search ]
                                  )
                                  ToolTip.tip "Search"
                                  Button.isEnabled searchButtonEnabled.Current
                                  Button.onClick (fun e ->
                                      let button = e.Source :?> Button
                                      let grid = button.Parent :?> Grid

                                      let textBox =
                                          grid.Children |> Seq.filter (fun c -> c :? TextBox) |> Seq.head :?> TextBox

                                      searchText.Set(textBox.Text)
                                      searchButtonEnabled.Set(false)
                                      Async.StartImmediate doSearch) ] ] ]

            let getStationsListBox (source: ObservableCollection<Station>) =
                ListBox.create
                    [ Grid.row 1
                      ListBox.background (SolidColorBrush(Colors.Transparent))
                      ListBox.dataItems source
                      ListBox.itemsPanel (FuncTemplate<Panel>(fun () -> WrapPanel()))
                      ListBox.onSelectedItemChanged (fun item ->
                          match box item with
                          | null -> None
                          | :? Station as i -> Some i
                          | _ -> failwith "Something went horribly wrong!"
                          |> selectedItem.Set

                          if isPlaying.Current then
                              Async.StartImmediate playStop
                          else
                              playEnabled.Set(true))
                      ListBox.itemTemplate (DataTemplateView<_>.create (fun (data: Station) -> getItem (data, 270.0)))
                      ListBox.margin 4 ]

            let addRemoveFavorite (item: Station) =
                if isStationFavorite item then
                    let itemToRemove = favItems.Current |> Seq.find (fun x -> x.Id = item.Id)
                    itemToRemove.IsFavorite <- false
                    favItems.Current.Remove(itemToRemove) |> ignore
                    item.IsFavorite <- false
                else
                    item.IsFavorite <- true
                    favItems.Current.Add(item)

                selectedItem.Set(Some item)

            let getAddToFavoriteBtnEnabled (item: Option<Station>) =
                match item with
                | Some i -> true
                | None -> false

            let getPlayerPanel =
                Grid.create
                    [ Grid.row 2
                      Grid.columnDefinitions "*, Auto, Auto, Auto"
                      Grid.children
                          [ Panel.create
                                [ Grid.column 0
                                  Panel.height 70
                                  Panel.horizontalAlignment HorizontalAlignment.Left
                                  Panel.dataContext selectedItem.Current
                                  Panel.children
                                      [ ContentControl.create
                                            [ ContentControl.content selectedItem.Current
                                              ContentControl.contentTemplate (
                                                  DataTemplateView<_>.create (fun (data: Option<Station>) ->
                                                      getSelectedItem data)
                                              ) ] ] ]

                            Button.create
                                [ Grid.column 1
                                  Button.margin (4, 20, 4, 4)
                                  ToolTip.tip (
                                      match nowPlaying.Current with
                                      | None -> "Search in youtube"
                                      | Some s -> $"Search in youtube: {s}"
                                  )
                                  Button.onClick (fun _ -> searchInYoutube (nowPlaying.Current))
                                  Button.content (
                                      SymbolIcon.create
                                          [ SymbolIcon.width 24
                                            SymbolIcon.height 24
                                            SymbolIcon.symbol Symbol.VideoClip ]
                                  )
                                  Button.isEnabled (
                                      match nowPlaying.Current with
                                      | None -> false
                                      | Some s -> not (String.IsNullOrWhiteSpace(s))
                                  ) ]

                            Button.create
                                [ Grid.column 2
                                  Button.margin (4, 20, 4, 4)
                                  Button.isEnabled (getAddToFavoriteBtnEnabled (selectedItem.Current))
                                  Button.onClick (fun _ -> addRemoveFavorite (selectedItem.Current.Value))
                                  Button.content (
                                      SymbolIcon.create
                                          [ SymbolIcon.width 24
                                            SymbolIcon.height 24
                                            SymbolIcon.symbol Symbol.Star
                                            ToolTip.tip (
                                                if isOptionStationFavorite selectedItem.Current then
                                                    "Remove from favorites"
                                                else
                                                    "Add to favorites"
                                            )
                                            SymbolIcon.iconVariant (
                                                if isOptionStationFavorite selectedItem.Current then
                                                    IconVariant.Filled
                                                else
                                                    IconVariant.Regular
                                            ) ]

                                  ) ]

                            Button.create
                                [ Grid.column 3
                                  Button.margin (4, 20, 4, 4)
                                  Button.content (
                                      SymbolIcon.create
                                          [ SymbolIcon.width 24
                                            SymbolIcon.height 24
                                            SymbolIcon.symbol (if isPlaying.Current then Symbol.Stop else Symbol.Play) ]
                                  )
                                  ToolTip.tip (if isPlaying.Current then "Stop" else "Play")
                                  Button.isEnabled playEnabled.Current
                                  Button.onClick (fun _ -> Async.StartImmediate playStop) ]

                            ] ]

            let searchPageContent =
                Grid.create
                    [ Grid.rowDefinitions "Auto,*"
                      Grid.children [ getSearchPanel; getStationsListBox (items.Current) ] ]

            let getTabPanel =
                TabControl.create
                    [ Grid.row 0
                      TabControl.tabStripPlacement Dock.Top
                      TabControl.viewItems
                          [ TabItem.create [ TabItem.header "Search"; TabItem.content searchPageContent ]
                            TabItem.create
                                [ TabItem.header "Favorites"
                                  TabItem.content (getStationsListBox (favItems.Current)) ] ] ]

            Grid.create [ Grid.rowDefinitions "*, Auto"; Grid.children [ getTabPanel; getPlayerPanel ] ])

type MainWindow() as this =
    inherit HostWindow()

    do
        let getListBoxItemStyle =
            let style = new Style(fun x -> x.OfType(typeof<ListBoxItem>))
            style.Setters.Add(Setter(ListBoxItem.PaddingProperty, Thickness(2.0)))
            style.Setters.Add(Setter(ListBoxItem.CornerRadiusProperty, CornerRadius(5.0)))
            style.Setters.Add(Setter(ListBoxItem.WidthProperty, 360.0))
            style.Setters.Add(Setter(ListBoxItem.BorderBrushProperty, Brushes.Gray))
            style.Setters.Add(Setter(ListBoxItem.BorderThicknessProperty, Thickness(1.0)))
            style.Setters.Add(Setter(ListBoxItem.MarginProperty, Thickness(2.0)))
            style :> IStyle

        base.Title <- "Radio Browser"
        base.Width <- 780.0
        base.Height <- 575.0
        base.Icon <- new WindowIcon(new Bitmap(Path.Combine(__SOURCE_DIRECTORY__, "img/Fsharp_logo.png")))
        base.Styles.Add(getListBoxItemStyle)
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
