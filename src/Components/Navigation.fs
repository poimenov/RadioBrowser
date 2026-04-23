[<AutoOpen>]
module RadioBrowser.Navigation

open Microsoft.Extensions.Localization
open Microsoft.FluentUI.AspNetCore.Components
open Fun.Blazor
open Microsoft.AspNetCore.Components.Routing

let navmenus =
    html.injectWithNoKey (fun (store: IShareStore, localizer: IStringLocalizer<SharedResources>) ->
        adaptiview () {
            let! binding = store.IsMenuOpen.WithSetter()
            let! stationsState = store.Stations
            let! tagsState = store.Tags
            let! countriesState = store.Countries

            let isDisabled =
                let ss =
                    match stationsState with
                    | Loading _ -> true
                    | _ -> false

                let ts =
                    match tagsState with
                    | Loading _ -> true
                    | _ -> false

                let cs =
                    match countriesState with
                    | Loading _ -> true
                    | _ -> false

                ss || ts || cs

            FluentNavMenu'' {
                Width 200
                Collapsible true

                Expanded' binding

                FluentNavLink'' {
                    Href "/"
                    Disabled isDisabled
                    Match NavLinkMatch.All
                    Icon(Icons.Regular.Size20.Home())
                    Tooltip(string (localizer["Home"]))
                    localizer["Home"]
                }

                FluentNavLink'' {
                    Href "/favorites"
                    Disabled isDisabled
                    Match NavLinkMatch.Prefix
                    Icon(Icons.Regular.Size20.Heart())
                    Tooltip(string (localizer["Favorites"]))
                    localizer["Favorites"]
                }

                FluentNavLink'' {
                    Href "/countries"
                    Disabled isDisabled
                    Match NavLinkMatch.Prefix
                    Icon(Icons.Regular.Size20.Flag())
                    Tooltip(string (localizer["ByCountry"]))
                    localizer["ByCountry"]
                }

                FluentNavLink'' {
                    Href "/tags"
                    Disabled isDisabled
                    Match NavLinkMatch.Prefix
                    Icon(Icons.Regular.Size20.Tag())
                    Tooltip(string (localizer["ByTags"]))
                    localizer["ByTags"]
                }

                FluentNavLink'' {
                    Href "/stationsByVotes"
                    Disabled isDisabled
                    Match NavLinkMatch.Prefix
                    Icon(Icons.Regular.Size20.Vote())
                    Tooltip(string (localizer["ByVotes"]))
                    localizer["ByVotes"]
                }

                FluentNavLink'' {
                    Href "/stationsByClicks"
                    Disabled isDisabled
                    Match NavLinkMatch.Prefix
                    Icon(Icons.Regular.Size20.CursorClick())
                    Tooltip(string (localizer["ByClicks"]))
                    localizer["ByClicks"]
                }

                FluentNavLink'' {
                    Href "/history"
                    Disabled isDisabled
                    Match NavLinkMatch.Prefix
                    Icon(Icons.Regular.Size20.History())
                    Tooltip(string (localizer["History"]))
                    localizer["History"]
                }
            }
        })
