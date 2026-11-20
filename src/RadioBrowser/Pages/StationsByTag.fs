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
            let countryPickerOpenState = cval false
            let countrySearchTextState = cval ""

            adapt {
                let! searchString, setSearchString = sString.WithSetter()
                let! selectedCountryCode, setSelectedCountryCode = sCountryCode.WithSetter()
                let! countryPickerOpen, setCountryPickerOpen = countryPickerOpenState.WithSetter()
                let! countrySearchText, setCountrySearchText = countrySearchTextState.WithSetter()
                let! countriesState = store.Countries

                let getCode (code: Nullable<Country>) =
                    if code.HasValue then Some code.Value.Code else None

                let getCountry (item: CountriesProvider.Country) =
                    let mutable retVal = new Country()
                    retVal.Name <- item.Name
                    retVal.Code <- item.Iso31661
                    Nullable retVal

                let filteredCountries =
                    match countriesState with
                    | Loaded countries ->
                        countries
                        |> Array.filter (fun country ->
                            String.IsNullOrWhiteSpace countrySearchText
                            || country.Name.Contains(countrySearchText, StringComparison.OrdinalIgnoreCase))
                        |> Array.sortBy (fun country -> -country.Stationcount, country.Name)
                    | _ -> [||]

                let searchEnabled (str: string) =
                    let s = str.Trim()
                    s.Length > 2 && s.Length < 36

                let selectCountry (country: Nullable<Country>) =
                    task {
                        setSelectedCountryCode country
                        setCountryPickerOpen false
                        setCountrySearchText ""

                        let! stationsResult =
                            if searchEnabled searchString then
                                getStationsByTag (Some searchString, tag, getCode country)
                            else
                                getStationsByTag (None, tag, getCode country)

                        publishStationsResult (store, stationsResult, emptyStations)
                    }

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
                                        getStationsByTag (Some s, tag, getCode selectedCountryCode)
                                    else
                                        getStationsByTag (None, tag, getCode selectedCountryCode)

                                publishStationsResult (store, stationsResult, emptyStations)
                            })
                    }

                    button {
                        type' "button"
                        id "countryPickerButton"
                        class' "search-input country-picker-button"
                        onclick (fun _ ->
                            setCountrySearchText ""
                            setCountryPickerOpen (not countryPickerOpen))

                        if selectedCountryCode.HasValue then
                            img {
                                class' "flag-icon"
                                src $"./images/flags/{selectedCountryCode.Value.Code.ToLowerInvariant()}.svg"
                                alt selectedCountryCode.Value.Code
                            }

                            span { selectedCountryCode.Value.Name }
                        else
                            span { localizer["Countries"] }

                        FluentIcon'' { Value(Icons.Regular.Size20.ChevronDown()) }
                    }

                    FluentPopover'' {
                        AnchorId "countryPickerButton"
                        Opened countryPickerOpen
                        OpenedChanged(fun opened ->
                            setCountryPickerOpen opened

                            if not opened then
                                setCountrySearchText "")
                        style' "width:330px;"

                        input {
                            type' "search"
                            class' "search-input"
                            style' "width:100%;"
                            placeholder (string (localizer["Countries"]))
                            value countrySearchText
                            oninput (fun args ->
                                setCountrySearchText (string args.Value)
                                setCountryPickerOpen true)
                        }

                        div {
                            class' "country-picker-options"

                            button {
                                type' "button"
                                class' "country-picker-option"
                                onclick (fun _ -> selectCountry (Nullable<Country>()))
                                localizer["Countries"]
                            }

                            for country in filteredCountries do
                                button {
                                    type' "button"
                                    class' "country-picker-option"
                                    onclick (fun _ -> selectCountry (getCountry country))

                                    img {
                                        class' "flag-icon"
                                        src $"images/flags/{country.Iso31661.ToLowerInvariant()}.svg"
                                        alt country.Iso31661
                                    }

                                    span { country.Name }
                                }
                        }
                    }
                }

                stationsList (store, localizer)
            })
