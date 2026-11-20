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

                    if countryCode = stationsService.Settings.CurrentRegion.TwoLetterISORegionName then
                        store.HeaderIcon.Publish(Icons.Regular.Size24.Home())
                    else
                        store.HeaderIcon.Publish(Icons.Regular.Size24.Flag())

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

                    input {
                        type' "search"
                        class' "search-input"
                        title' (string (localizer["StationNamePlaceholder"]))
                        placeholder (string (localizer["StationNamePlaceholder"]))
                        value searchString

                        oninput (fun args ->
                            task {
                                let s = string args.Value
                                setSearchString s

                                let! stationsResult =
                                    if searchEnabled s then
                                        getStationsByCountryCode (Some s, countryCode, Option.ofObj selectedTag)
                                    else
                                        getStationsByCountryCode (None, countryCode, Option.ofObj selectedTag)

                                publishStationsResult (store, stationsResult, emptyStations)
                            })
                    }

                    input {
                        type' "search"
                        class' "search-input"
                        list "tag-options"
                        placeholder (string (localizer["Tags"]))
                        value (if isNull selectedTag then "" else selectedTag)

                        oninput (fun args ->
                            task {
                                let item = string args.Value
                                setSelectedTag (if String.IsNullOrWhiteSpace item then null else item)

                                let! stationsResult =
                                    if searchEnabled searchString then
                                        getStationsByCountryCode (Some searchString, countryCode, Option.ofObj item)
                                    else
                                        getStationsByCountryCode (None, countryCode, Option.ofObj item)

                                publishStationsResult (store, stationsResult, emptyStations)
                            })
                    }

                    datalist {
                        id "tag-options"

                        match store.Tags.Value with
                        | Loaded tags ->
                            for tag in tags do
                                option {
                                    value tag.Name
                                }
                        | _ -> ()
                    }
                }

                stationsList (store, localizer)
            })
