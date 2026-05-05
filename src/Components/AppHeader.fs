[<AutoOpen>]
module RadioBrowser.AppHeader

open Microsoft.Extensions.Localization
open Microsoft.Extensions.Options
open Microsoft.FluentUI.AspNetCore.Components
open Microsoft.FluentUI.AspNetCore.Components.Extensions
open FSharp.Data.Adaptive
open Fun.Blazor

let appHeader =
    html.inject
        (fun
            (store: IShareStore,
             options: IOptions<AppSettings>,
             los: ILinkOpeningService,
             localizer: IStringLocalizer<SharedResources>) ->
            FluentHeader'' {
                FluentStack'' {
                    Orientation Orientation.Horizontal
                    HorizontalGap 2

                    img {
                        src AppSettings.FavIconFileName
                        style { height "40px" }
                    }

                    FluentLabel'' {
                        Typo Typography.H2
                        Color Color.Fill
                        style' "cursor: default;"
                        title' $"v{typeof<AppSettings>.Assembly.GetName().Version}"
                        AppSettings.ApplicationName
                    }

                    FluentSpacer''

                    let isMenuOpen = cval false

                    adapt {
                        let! theme = store.Theme
                        let! accentColor = store.AccentColor
                        let! menuOpen, setMenuOpen = isMenuOpen.WithSetter()

                        if store.GetTitleDelay.Value <> options.Value.GetTitleDelay then
                            store.GetTitleDelay.Publish options.Value.GetTitleDelay

                        if store.HistoryTruncateCount.Value <> options.Value.HistoryTruncateCount then
                            store.HistoryTruncateCount.Publish options.Value.HistoryTruncateCount

                        FluentDesignTheme'' {
                            StorageName "theme"
                            Mode theme
                            OfficeColor accentColor

                            OnLoaded(fun args ->
                                if args.IsDark then
                                    store.Theme.Publish DesignThemeModes.Dark)
                        }

                        FluentButton'' {
                            id "settingsButton"
                            Appearance Appearance.Accent
                            IconStart(Icons.Regular.Size20.Settings())
                            OnClick(fun _ -> setMenuOpen (not menuOpen))
                        }

                        FluentMenu'' {
                            Anchor "settingsButton"
                            Open'(menuOpen, setMenuOpen)

                            FluentMenuItem'' {
                                OnClick(fun _ ->
                                    store.Theme.Publish(
                                        if theme = DesignThemeModes.Dark then
                                            DesignThemeModes.Light
                                        else
                                            DesignThemeModes.Dark
                                    ))

                                span {
                                    slot' "start"

                                    FluentIcon'' {
                                        Value(
                                            if theme = DesignThemeModes.Dark then
                                                Icons.Regular.Size20.WeatherSunny() :> Icon
                                            else
                                                Icons.Regular.Size20.WeatherMoon() :> Icon
                                        )
                                    }
                                }

                                localizer["SwitchTheme"]
                            }

                            let colors = System.Enum.GetValues<OfficeColor>()

                            let getCustomColor (color: OfficeColor) : string =
                                match color with
                                | OfficeColor.Default -> "#036ac4"
                                | c -> c.ToAttributeValue()

                            FluentMenuItem'' {
                                MenuItems(
                                    fragment {
                                        for c in colors do
                                            FluentMenuItem'' {
                                                OnClick(fun _ -> store.AccentColor.Publish c)
                                                Checked(c = accentColor)

                                                span {
                                                    slot' "start"

                                                    FluentIcon'' {
                                                        Value(Icons.Filled.Size20.RectangleLandscape())
                                                        Color Color.Custom
                                                        CustomColor(getCustomColor c)
                                                    }
                                                }

                                                c.GetDisplayName()
                                            }
                                    }
                                )

                                localizer["Accent Color"]
                            }

                            FluentMenuItem'' {
                                OnClick(fun _ -> los.OpenUrl "https://github.com/poimenov/RadioBrowser")

                                span {
                                    slot' "start"
                                    FluentIcon'' { Value(Github()) }
                                }

                                localizer["GitHub"]
                            }
                        }

                    }
                }
            })
