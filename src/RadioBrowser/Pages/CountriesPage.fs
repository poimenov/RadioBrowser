[<AutoOpen>]
module RadioBrowser.CountriesPage

open System
open Microsoft.AspNetCore.Components
open Microsoft.Extensions.Localization
open Microsoft.FluentUI.AspNetCore.Components
open FSharp.Data.Adaptive
open Fun.Blazor

let countriesPage =
    html.inject
        (fun
            (store: IShareStore,
             listsService: IListsService,
             navigation: NavigationManager,
             localizer: IStringLocalizer<SharedResources>,
             hook: IComponentHook) ->
            hook.AddFirstAfterRenderTask(fun _ ->
                task {
                    store.HeaderTitle.Publish localizer["Countries"]
                    store.HeaderIcon.Publish(Icons.Regular.Size24.Flag())

                    do!
                        Async.Parallel [ loadCountries (store, listsService); loadTasks (store, listsService) ]
                        |> Async.Ignore
                })

            fragment {
                adapt {
                    let! countriesState, setCountries = store.Countries.WithSetter()
                    let! searchString, setSearchString = cval("").WithSetter()

                    match countriesState with
                    | Loading _ -> loadingState localizer
                    | Failed(countries, m) ->
                        div {
                            style' "text-align:center;color:red;"
                            string (localizer["Fail"]) + ": " + m
                        }
                    | EndOfList countries
                    | Loaded countries ->
                        let candidateCountries =
                            if String.IsNullOrWhiteSpace searchString then
                                countries
                            else
                                countries
                                |> Array.filter (fun c -> c.Name.ToLower().Contains(searchString.ToLower()))

                        let filteredCountries =
                            candidateCountries
                            |> Array.distinctBy (fun country -> country.Name)
                            |> Array.sortBy (fun country -> -country.Stationcount, country.Name)

                        div {
                            style' "margin-bottom:10px;"

                            input {
                                type' "search"
                                class' "search-input"
                                style' "width:330px;"
                                placeholder (string (localizer["CountryName"]))
                                value searchString
                                oninput (fun args -> setSearchString (string args.Value))
                            }
                        }

                        if filteredCountries.Length = 0 then
                            div {
                                style' "text-align:center;"
                                localizer["NoCountriesFound"]
                            }
                        else
                            div {
                                class' "countries-list"

                                for country in filteredCountries do
                                    div {
                                        class' "country"

                                        title'
                                            $"""{country.Name.ToUpper()} ({localizer["StationsCount"]}: {country.Stationcount})"""

                                        onclick (fun _ ->
                                            navigation.NavigateTo $"/stationsByCountry/{country.Iso31661}")

                                        div {
                                            class' "country-name"
                                            country.Name
                                        }

                                        img {
                                            class' "country-image"
                                            src $"./images/flags/{country.Iso31661.ToLower()}.svg"
                                            loadingExperimental true
                                        }
                                    }
                            }
                }
            })
