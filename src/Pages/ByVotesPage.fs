[<AutoOpen>]
module RadioBrowser.ByVotesPage

open Microsoft.Extensions.Localization
open Fun.Blazor

let byVotesPage =
    html.inject
        (fun
            (store: IShareStore,
             stationsService: IStationsService,
             localizer: IStringLocalizer<SharedResources>,
             hook: IComponentHook) ->
            hook.AddFirstAfterRenderTask(fun _ ->
                task {
                    store.HeaderTitle.Publish localizer["StationsByVotes"]
                    store.Stations.Publish(Loading emptyStations)
                    store.SearchMode.Publish ByVotes
                    let parameters = getParameters (0, stationsService.Settings)
                    let! stationsResult = stationsService.GetStationsByVotes parameters
                    publishStationsResult (store, stationsResult, emptyStations)
                })

            stationsList (store, localizer))
