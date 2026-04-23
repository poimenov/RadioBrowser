[<AutoOpen>]
module RadioBrowser.ByCliksPage

open Microsoft.Extensions.Localization
open Fun.Blazor

let byClicksPage =
    html.inject
        (fun
            (store: IShareStore,
             stationsService: IStationsService,
             localizer: IStringLocalizer<SharedResources>,
             hook: IComponentHook) ->
            hook.AddFirstAfterRenderTask(fun _ ->
                task {
                    store.HeaderTitle.Publish localizer["StationsByClicks"]
                    store.SearchMode.Publish ByClicks
                    store.Stations.Publish(Loading emptyStations)
                    let parameters = getParameters (0, stationsService.Settings)
                    let! stationsResult = stationsService.GetStationsByClicks parameters
                    publishStationsResult (store, stationsResult, emptyStations)
                })

            stationsList (store, localizer))
