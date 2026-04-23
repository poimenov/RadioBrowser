[<AutoOpen>]
module RadioBrowser.WatchElement

open System.Collections.Generic
open System.Linq
open Microsoft.Extensions.Localization
open Microsoft.FluentUI.AspNetCore.Components
open Microsoft.JSInterop
open Fun.Blazor

let publishStationsResult
    (store: IShareStore, stationsResult: Result<Station array, string>, stations: IEnumerable<Station>)
    =
    match stationsResult with
    | Error m -> store.Stations.Publish(Failed(stations, m))
    | Ok newStations ->
        if newStations.Length > 0 then
            store.Stations.Publish(Loaded(stations.Concat newStations))
        else
            store.Stations.Publish(EndOfList stations)

let loadingState (localizer: IStringLocalizer<SharedResources>) =
    div {
        style' "margin:auto;width:100px;"

        div { localizer["IsLoading"] }

        FluentProgressRing''
    }

let getParameters (offset: int, settings: AppSettings) =
    GetStationParameters(offset, settings.LimitCount, settings.HideBroken)

type AppCallbacks(store: IShareStore, stationsService: IStationsService, window: Photino.NET.PhotinoWindow) =
    let nextLoad (stations: IEnumerable<Station>) =
        async {
            let parameters = getParameters (stations.Count(), stationsService.Settings)

            store.Stations.Publish(Loading stations)

            let! stationsResult =
                match store.SearchMode.Value with
                | ByVotes -> stationsService.GetStationsByVotes parameters
                | ByClicks -> stationsService.GetStationsByClicks parameters
                | Favorites name -> stationsService.GetFavoriteStations(name, parameters)
                | Search ssp -> stationsService.SearchStations(ssp, parameters)
                | _ -> async { return Ok(Enumerable.Empty<Station>().ToArray()) }

            publishStationsResult (store, stationsResult, stations)
        }

    [<JSInvokable>]
    member _.OnWindowResize(width: int, height: int) =
        task {
            store.IsMenuOpen.Publish(width > 800)

            let w = if width < 600 then 600 else width
            let h = if height < 400 then 400 else height

            if width < 700 || height < 500 then
                window.SetSize(w, h) |> ignore
        }

    [<JSInvokable>]
    member _.OnElementVisible() =
        task {
            match store.Stations.Value with
            | Loading _ -> ()
            | EndOfList _ -> ()
            | Loaded stations -> nextLoad stations |> Async.Start
            | Failed(stations, m) -> nextLoad stations |> Async.Start
        }

let watchElementVisibleComponent =
    html.inject
        (fun
            (store: IShareStore,
             jsRuntime: IJSRuntime,
             hook: IComponentHook,
             stationsService: IStationsService,
             window: Photino.NET.PhotinoWindow) ->
            let elementId = "watched-div"

            hook.AddFirstAfterRenderTask(fun _ ->
                task {
                    let callback = AppCallbacks(store, stationsService, window)
                    let dotNetRef = DotNetObjectReference.Create callback
                    jsRuntime.InvokeVoidAsync("setCallbacks", elementId, dotNetRef) |> ignore
                })

            div {
                style' "margin:auto;width:50px;"

                adapt {
                    let! stationsState = store.Stations

                    match stationsState with
                    | Loading _ -> FluentProgressRing'' { style' "margin-top:5px;margin-bottom:5px;" }
                    | _ -> ()
                }

                div { id elementId }
            })