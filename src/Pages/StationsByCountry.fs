[<AutoOpen>]
module RadioBrowser.StationsByCountry

open System
open System.Globalization
open Microsoft.Extensions.Localization
open Microsoft.FluentUI.AspNetCore.Components
open FSharp.Data.Adaptive
open Fun.Blazor

let stationsByCountry (countryCode: string) =
    html.inject
        (fun
            (store: IShareStore,
             stationsService: IStationsService,
             localizer: IStringLocalizer<SharedResources>,
             hook: IComponentHook) ->
            let getStationsByCountryCode (name: string option, countryCode: string, tag: string option) =
                async {
                    store.Stations.Publish(Loading emptyStations)
                    let searchParams = SearchStationParameters(name, None, Some countryCode, tag, None)
                    store.SearchMode.Publish(Search searchParams)
                    let parameters = getParameters (0, stationsService.Settings)

                    return!
                        stationsService.SearchStations(searchParams, parameters)
                        |> Async.StartAsTask
                        |> Async.AwaitTask
                }

            hook.AddFirstAfterRenderTask(fun _ ->
                task {
                    let regionInfo = RegionInfo countryCode
                    store.HeaderTitle.Publish $"""{localizer["StationsByCountry"]}: {regionInfo.EnglishName}"""
                    let! stationsResult = getStationsByCountryCode (None, countryCode, None)
                    publishStationsResult (store, stationsResult, emptyStations)
                })

            let sString = cval ""
            let sTag = cval null

            adapt {
                let! searchString, setSearchString = sString.WithSetter()
                let! selectedTag, setSelectedTag = sTag.WithSetter()

                let searchEnabled (str: string) =
                    let s = str.Trim()
                    s.Length > 2 && s.Length < 36

                div {
                    class' "stations-list"
                    style' "padding:10px;"

                    FluentSearch'' {
                        title' (string (localizer["StationNamePlaceholder"]))
                        Placeholder(string (localizer["StationNamePlaceholder"]))
                        Value searchString

                        ValueChanged(fun s ->
                            task {
                                setSearchString s

                                let! stationsResult =
                                    if searchEnabled s then
                                        getStationsByCountryCode (Some s, countryCode, Option.ofObj selectedTag)
                                    else
                                        getStationsByCountryCode (None, countryCode, Option.ofObj selectedTag)

                                publishStationsResult (store, stationsResult, emptyStations)
                            })
                    }

                    FluentAutocomplete'' {
                        type' string
                        SelectedOption'(selectedTag, setSelectedTag)
                        autocomplete "off"
                        Placeholder(string (localizer["Tags"]))
                        Multiple false

                        OnOptionsSearch(fun (e: OptionsSearchEventArgs<string>) ->
                            task {
                                match store.Tags.Value with
                                | Loaded tags ->
                                    e.Items <-
                                        tags
                                        |> Array.filter (fun (x: NameAndCountProvider.NameAndCount) ->
                                            x.Name.StartsWith(e.Text, StringComparison.OrdinalIgnoreCase))
                                        |> Array.sortBy (fun x -> x.Name)
                                        |> Array.map (fun x -> x.Name)
                                        |> Array.toSeq
                                | _ -> ()
                            })

                        SelectedOptionChanged(fun (item: string) ->
                            task {
                                setSelectedTag item

                                let! stationsResult =
                                    if searchEnabled searchString then
                                        getStationsByCountryCode (Some searchString, countryCode, Option.ofObj item)
                                    else
                                        getStationsByCountryCode (None, countryCode, Option.ofObj item)

                                publishStationsResult (store, stationsResult, emptyStations)
                            })
                    }
                }

                stationsList (store, localizer)
            })
