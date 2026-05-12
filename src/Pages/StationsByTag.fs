[<AutoOpen>]
module RadioBrowser.StationsByTag

open System
open Microsoft.Extensions.Localization
open Microsoft.FluentUI.AspNetCore.Components
open FSharp.Data.Adaptive
open Fun.Blazor

let stationsByTag (tag: string) =
    html.inject
        (fun
            (store: IShareStore,
             stationsService: IStationsService,
             localizer: IStringLocalizer<SharedResources>,
             hook: IComponentHook) ->
            let getStationsByTag (name: string option, tag: string, countryCode: string option) =
                async {
                    store.Stations.Publish(Loading emptyStations)
                    let searchParams = SearchStationParameters(name, None, countryCode, Some tag, None)
                    store.SearchMode.Publish(Search searchParams)
                    let parameters = getParameters (0, stationsService.Settings)

                    return!
                        stationsService.SearchStations(searchParams, parameters)
                        |> Async.StartAsTask
                        |> Async.AwaitTask
                }

            hook.AddFirstAfterRenderTask(fun _ ->
                task {
                    store.Stations.Publish(Loading emptyStations)
                    store.HeaderIcon.Publish(Icons.Regular.Size24.Tag())
                    store.HeaderTitle.Publish $"""{localizer["StationsByTag"]}: {tag}"""
                    let! stationsResult = getStationsByTag (None, tag, None)
                    publishStationsResult (store, stationsResult, emptyStations)
                })

            let sString = cval ""
            let sCountryCode: cval<Nullable<Country>> = cval (Nullable<Country>())

            adapt {
                let! searchString, setSearchString = sString.WithSetter()
                let! selectedCountryCode, setSelectedCountryCode = sCountryCode.WithSetter()

                let getCode (code: Nullable<Country>) =
                    if code.HasValue then Some code.Value.Code else None

                let getCountry (item: CountriesProvider.Country) =
                    let mutable retVal = new Country()
                    retVal.Name <- item.Name
                    retVal.Code <- item.Iso31661
                    Nullable retVal

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
                                        getStationsByTag (Some s, tag, getCode selectedCountryCode)
                                    else
                                        getStationsByTag (None, tag, getCode selectedCountryCode)

                                publishStationsResult (store, stationsResult, emptyStations)
                            })
                    }

                    FluentAutocomplete'' {
                        type' Nullable<Country>
                        SelectedOption'(selectedCountryCode, setSelectedCountryCode)
                        autocomplete "off"
                        Placeholder(string (localizer["Countries"]))
                        Multiple false

                        SelectedOptionTemplate(fun item ->
                            FluentStack'' {
                                Orientation Orientation.Horizontal
                                VerticalAlignment VerticalAlignment.Center

                                img {
                                    class' "flag-icon"
                                    src $"./images/flags/{item.Value.Code.ToLower()}.svg"
                                    loadingExperimental true
                                    alt item.Value.Code
                                }

                                span { item.Value.Name }
                            })

                        OptionTemplate(fun item ->
                            FluentStack'' {
                                Orientation Orientation.Horizontal
                                VerticalAlignment VerticalAlignment.Center

                                img {
                                    class' "flag-icon"
                                    src $"images/flags/{item.Value.Code.ToLower()}.svg"
                                    loadingExperimental true
                                    alt item.Value.Code
                                }

                                span { item.Value.Name }
                            })

                        OnOptionsSearch(fun (e: OptionsSearchEventArgs<Nullable<Country>>) ->
                            task {
                                match store.Countries.Value with
                                | Loaded countries ->
                                    e.Items <-
                                        countries
                                        |> Array.filter (fun (x: CountriesProvider.Country) ->
                                            x.Name.Contains(e.Text, StringComparison.OrdinalIgnoreCase))
                                        |> Array.sortBy (fun x -> x.Name)
                                        |> Array.map (fun x -> getCountry x)
                                        |> Array.toSeq
                                | _ -> ()
                            })

                        SelectedOptionChanged(fun (item: Nullable<Country>) ->
                            task {
                                setSelectedCountryCode item

                                let! stationsResult =
                                    if searchEnabled searchString then
                                        getStationsByTag (Some searchString, tag, getCode item)
                                    else
                                        getStationsByTag (None, tag, getCode item)

                                publishStationsResult (store, stationsResult, emptyStations)
                            })
                    }
                }

                stationsList (store, localizer)
            })
