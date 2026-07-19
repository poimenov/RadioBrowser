[<AutoOpen>]
module RadioBrowser.App

open System
open System.Collections.Generic
open Microsoft.AspNetCore.Components.Web
open Microsoft.FluentUI.AspNetCore.Components
open Microsoft.JSInterop
open Fun.Blazor
open Fun.Blazor.Router

let rec getTitle (store: IShareStore, metadataService: IMetadataService, historyDataAccess: IHistoryDataAccess) =
    async {
        try
            match store.SelectedStation.Value with
            | NotSelected -> store.Title.Publish None
            | Selected station ->
                if store.IsPlaying.Value then
                    let! result = station.UrlResolved |> metadataService.GetTitleAsync

                    match result with
                    | Ok titleOpt ->
                        store.Title.Publish titleOpt

                        match titleOpt with
                        | None -> ()
                        | Some title ->
                            if not (String.IsNullOrWhiteSpace title) then
                                let history = store.History.Value

                                let record =
                                    { StartTime = DateTime.Now
                                      Title = title
                                      StationName = station.Name }

                                if
                                    history
                                    |> List.tryHead
                                    |> function
                                        | Some r -> r.Title <> title || r.StationName <> station.Name
                                        | None -> true
                                then
                                    historyDataAccess.Add record

                                    store.History.Publish(
                                        record :: history |> List.truncate store.HistoryTruncateCount.Value
                                    )

                    | _ -> store.Title.Publish None
                else
                    store.Title.Publish None
        with ex ->
            store.Title.Publish None

        do! store.GetTitleDelay.Value |> Async.Sleep
        return! getTitle (store, metadataService, historyDataAccess)
    }

let routes =
    html.route
        [| routeCi "/stationsByVotes" byVotesPage
           routeCi "/stationsByClicks" byClicksPage
           routeCi "/favorites" favoriteStations
           routeCi "/countries" countriesPage
           routeCif "/stationsByCountry/%s" (fun x -> stationsByCountry (x))
           routeCi "/tags" tagsPage
           routeCif "/stationsByTag/%s" (fun x -> stationsByTag (x))
           routeCi "/history" historyPage
           routeAny homePage |]

let app =
    html.inject
        (fun (hook: IComponentHook, store: IShareStore, services: IServices, window: Photino.NET.PhotinoWindow) ->
            hook.AddInitializedTask(fun _ ->
                task {
                    store.IsMenuOpen.Publish(window.Width > 800)

                    let update (ids: Guid array) =
                        async {
                            let! stationsState = services.StationsService.GetStations ids

                            match stationsState with
                            | Ok stations -> stations |> services.FavoritesDataAccess.Update |> ignore
                            | _ -> ()
                        }

                    let historyResult = services.HistoryDataAccess.GetHistory()

                    match historyResult with
                    | Ok records -> store.History.Publish records
                    | _ -> ()

                    getTitle (store, services.MetadataService, services.HistoryDataAccess)
                    |> Async.Start

                    let favCount = services.FavoritesDataAccess.FavoritesCount()
                    let favArrs = new List<Station array>()
                    let mutable count = 0

                    while count < favCount do
                        let favoritesResult =
                            services.FavoritesDataAccess.GetFavorites(
                                None,
                                getParameters (count, services.StationsService.Settings)
                            )

                        match favoritesResult with
                        | Ok favs ->
                            favArrs.Add favs
                            count <- count + favs.Length
                        | _ -> ()

                    favArrs
                    |> Seq.toList
                    |> List.map (fun arr -> update (arr |> Array.map (fun x -> x.Id)))
                    |> fun upd -> Async.Parallel(upd, 5) |> Async.Ignore |> Async.Start
                })

            ErrorBoundary'' {
                ErrorContent(fun e ->
                    FluentLabel'' {
                        Color Color.Error
                        string e
                    })

                FluentDesignTheme'' { StorageName "theme" }

                FluentToastProvider'' {
                    MaxToastCount 3
                    Position ToastPosition.TopLeft
                }

                FluentMenuProvider''
                FluentDialogProvider''

                FluentLayout'' {
                    appHeader

                    FluentStack'' {
                        Width "100%"
                        class' "main"
                        Orientation Orientation.Horizontal
                        navmenus

                        FluentBodyContent'' {
                            class' "body-content"
                            style { overflowHidden }

                            FluentStack'' {
                                style' "width:100%;height:100%;"
                                Orientation Orientation.Vertical
                                VerticalGap 2

                                adapt {
                                    let! selectedStation = store.SelectedStation
                                    let! isPlaying = store.IsPlaying
                                    let! searchMode = store.SearchMode
                                    let! headerTitle = store.HeaderTitle
                                    let! headerIcon = store.HeaderIcon

                                    let audioUrl =
                                        match selectedStation with
                                        | NotSelected -> None
                                        | Selected station ->
                                            if String.IsNullOrWhiteSpace station.UrlResolved then
                                                None
                                            else
                                                Some station.UrlResolved

                                    task {
                                        try
                                            do! services.JsRuntime.InvokeVoidAsync("playAudio", isPlaying, audioUrl)
                                        with ex ->
                                            services.ToastService.ShowError(
                                                string (services.Localizer["ErrorInvokePlayAudio"])
                                            )
                                            |> ignore
                                    }
                                    |> ignore

                                    let getErrorMessage =
                                        match selectedStation with
                                        | NotSelected -> ""
                                        | Selected station ->
                                            let msg = string (services.Localizer["ErrorLoadingStation"])
                                            $"{msg}: {station.Name} ({station.UrlResolved})"

                                    FluentStack'' {
                                        Orientation Orientation.Horizontal
                                        VerticalAlignment VerticalAlignment.Center
                                        HorizontalAlignment HorizontalAlignment.Center
                                        HorizontalGap 4
                                        style' "height: 30px;"

                                        FluentIcon'' { Value headerIcon }

                                        span {
                                            style' "font-size: 24px; font-weight: bold;"
                                            headerTitle
                                        }
                                    }

                                    let styleHeight =
                                        match selectedStation with
                                        | NotSelected -> "height: calc(100% - 30px);"
                                        | Selected _ -> "height: calc(100% - 104px);"

                                    div {
                                        class' "content"
                                        style' styleHeight

                                        routes
                                    }

                                    if selectedStation <> NotSelected then
                                        div {
                                            class' "player-container"
                                            player

                                            audio {
                                                id "player"
                                                style' "display:none;"
                                                controls

                                                onstalled (fun _ ->
                                                    task {
                                                        services.ToastService.ShowError getErrorMessage
                                                        store.IsPlaying.Publish false
                                                    })

                                                onerror (fun _ ->
                                                    task {
                                                        services.ToastService.ShowError getErrorMessage
                                                        store.IsPlaying.Publish false
                                                    })

                                                onended (fun _ -> store.IsPlaying.Publish false)
                                                onpause (fun _ -> store.IsPlaying.Publish false)
                                            }
                                        }
                                }
                            }
                        }
                    }

                    appFooter
                }
            })

type AppComponent() =
    inherit FunBlazorComponent()
    override this.Render() = app
