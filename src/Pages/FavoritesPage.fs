[<AutoOpen>]
module RadioBrowser.FavoritesPage

open System
open Microsoft.Extensions.Localization
open Microsoft.FluentUI.AspNetCore.Components
open FSharp.Data.Adaptive
open Fun.Blazor

let favoriteStations =
    html.inject
        (fun
            (store: IShareStore,
             stationsService: IStationsService,
             localizer: IStringLocalizer<SharedResources>,
             hook: IComponentHook) ->

            let getFavorites (name: string option) =
                async {
                    store.Stations.Publish(Loading emptyStations)
                    store.SearchMode.Publish(Favorites name)
                    let parameters = getParameters (0, stationsService.Settings)
                    return! stationsService.GetFavoriteStations(name, parameters)
                }

            hook.AddFirstAfterRenderTask(fun _ ->
                task {
                    store.HeaderTitle.Publish localizer["Favorites"]
                    let! stationsResult = getFavorites None
                    publishStationsResult (store, stationsResult, emptyStations)
                })

            adapt {
                let! searchString, setSearchString = cval("").WithSetter()

                div {
                    class' "stations-list"
                    style' "padding:10px;"

                    FluentSearch'' {
                        title' (string (localizer["FilterStations"]))
                        Placeholder(string (localizer["StationName"]))
                        Value searchString

                        ValueChanged(fun s ->
                            task {
                                setSearchString s

                                let! stationsResult =
                                    if String.IsNullOrWhiteSpace s then
                                        getFavorites None
                                    else
                                        getFavorites (Some s)

                                publishStationsResult (store, stationsResult, emptyStations)
                            })
                    }
                }

                stationsList (store, localizer)
            })