[<AutoOpen>]
module RadioBrowser.Navigation

open Microsoft.Extensions.Localization
open Microsoft.FluentUI.AspNetCore.Components
open Fun.Blazor
open Microsoft.AspNetCore.Components.Routing

let navmenus =
    html.injectWithNoKey (fun (store: IShareStore, localizer: IStringLocalizer<SharedResources>) ->
        adaptiview () {
            let! isMenuOpen = store.IsMenuOpen
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

            div {
                class' (if isMenuOpen then "navigation-pane expanded" else "navigation-pane collapsed")

                button {
                    type' "button"
                    class' "nav-collapse-toggle"
                    title' (if isMenuOpen then "Collapse navigation" else "Expand navigation")
                    onclick (fun _ -> store.IsMenuOpen.Publish(not isMenuOpen))

                    FluentIcon'' {
                        Value(Icons.Regular.Size24.Navigation())
                        style' "width:20px;height:20px;"
                    }
                }

                FluentNav'' {
                    Width "100%"
                    BackgroundColor "var(--navigation-item-background)"
                    BackgroundColorHover "var(--navigation-item-hover-background)"

                    FluentNavItem'' {
                        Href "/"
                        Disabled isDisabled
                        Match NavLinkMatch.All
                        IconRest(Icons.Regular.Size20.Home())
                        Tooltip(string (localizer["Home"]))
                        if isMenuOpen then localizer["Home"] else ()
                    }

                    FluentNavItem'' {
                        Href "/favorites"
                        Disabled isDisabled
                        Match NavLinkMatch.Prefix
                        IconRest(Icons.Regular.Size20.Heart())
                        Tooltip(string (localizer["Favorites"]))
                        if isMenuOpen then localizer["Favorites"] else ()
                    }

                    FluentNavItem'' {
                        Href "/countries"
                        Disabled isDisabled
                        Match NavLinkMatch.Prefix
                        IconRest(Icons.Regular.Size20.Flag())
                        Tooltip(string (localizer["ByCountry"]))
                        if isMenuOpen then localizer["ByCountry"] else ()
                    }

                    FluentNavItem'' {
                        Href "/tags"
                        Disabled isDisabled
                        Match NavLinkMatch.Prefix
                        IconRest(Icons.Regular.Size20.Tag())
                        Tooltip(string (localizer["ByTags"]))
                        if isMenuOpen then localizer["ByTags"] else ()
                    }

                    FluentNavItem'' {
                        Href "/stationsByVotes"
                        Disabled isDisabled
                        Match NavLinkMatch.Prefix
                        IconRest(Icons.Regular.Size20.Vote())
                        Tooltip(string (localizer["ByVotes"]))
                        if isMenuOpen then localizer["ByVotes"] else ()
                    }

                    FluentNavItem'' {
                        Href "/stationsByClicks"
                        Disabled isDisabled
                        Match NavLinkMatch.Prefix
                        IconRest(Icons.Regular.Size20.CursorClick())
                        Tooltip(string (localizer["ByClicks"]))
                        if isMenuOpen then localizer["ByClicks"] else ()
                    }

                    FluentNavItem'' {
                        Href "/history"
                        Disabled isDisabled
                        Match NavLinkMatch.Prefix
                        IconRest(Icons.Regular.Size20.History())
                        Tooltip(string (localizer["History"]))
                        if isMenuOpen then localizer["History"] else ()
                    }
                }
            }
        })
