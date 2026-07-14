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
                        let filteredCountries =
                            if String.IsNullOrWhiteSpace searchString then
                                countries |> Array.distinctBy (fun c -> c.Name)
                            else
                                countries
                                |> Array.filter (fun c -> c.Name.ToLower().Contains(searchString.ToLower()))
                                |> Array.distinctBy (fun c -> c.Name)

                        div {
                            style' "margin-bottom:10px;"

                            FluentSearch'' {
                                style' "width:330px;"
                                Placeholder(string (localizer["CountryName"]))
                                Immediate true
                                Value searchString
                                ValueChanged(fun s -> setSearchString s)
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
