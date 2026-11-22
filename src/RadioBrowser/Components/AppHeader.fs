[<AutoOpen>]
module RadioBrowser.AppHeader

open Microsoft.Extensions.Options
open Microsoft.FluentUI.AspNetCore.Components
open Microsoft.JSInterop
open FSharp.Data.Adaptive
open Fun.Blazor

let appHeader =
    html.inject
        (fun (store: IShareStore, services: IServices, themeService: IThemeService, options: IOptions<AppSettings>) ->
            let los = services.LinkOpeningService
            let jsRuntime = services.JsRuntime
            let localizer = services.Localizer

            header {
                class' "app-header"

                style'
                    "display:flex;align-items:center;height:56px;flex:0 0 56px;box-sizing:border-box;position:relative;z-index:2;overflow:visible;padding:8px 12px;background:var(--colorBrandBackground);color:var(--colorNeutralForegroundOnBrand,#ffffff);"

                FluentStack'' {
                    Orientation Orientation.Horizontal
                    VerticalAlignment VerticalAlignment.Center
                    HorizontalGap "8"
                    style' "width:100%;"

                    img {
                        src AppSettings.FavIconFileName
                        style { height "40px" }
                    }

                    h2 {
                        class' "app-title"
                        style' "font-size:24px;line-height:32px;cursor:default;margin:0;"
                        title' $"v{typeof<AppSettings>.Assembly.GetName().Version}"
                        AppSettings.ApplicationName
                    }

                    FluentSpacer''

                    adapt {
                        let! theme = store.Theme
                        let! accentColor = store.AccentColor
                        let! cultureName = store.CultureName

                        let colors =
                            [ "Blue", "#036ac4"
                              "Teal", "#008272"
                              "Purple", "#8764b8"
                              "Green", "#107c10"
                              "Orange", "#ca5010"
                              "Red", "#d13438" ]

                        let colorItems =
                            FluentMenuItem'' {
                                MenuItems(
                                    fragment {
                                        for (label, accentHex) in colors do
                                            FluentMenuItem'' {
                                                role MenuItemRole.Radio
                                                checked' (accentColor = accentHex)

                                                CheckedChanged(fun isChecked ->
                                                    task {
                                                        if isChecked.GetValueOrDefault() then
                                                            do! themeService.SetThemeAsync(accentHex, true)

                                                            do!
                                                                themeService.SetThemeAsync(
                                                                    if theme = Dark then
                                                                        ThemeMode.Dark
                                                                    else
                                                                        ThemeMode.Light
                                                                )

                                                            do!
                                                                jsRuntime.InvokeVoidAsync(
                                                                    "applyRadioBrowserTheme",
                                                                    (if theme = Dark then "dark" else "light"),
                                                                    accentHex
                                                                )

                                                            store.AccentColor.Publish accentHex
                                                    })

                                                FluentIcon'' {
                                                    slot' "start"
                                                    Value(Icons.Filled.Size20.RectangleLandscape())
                                                    style' $"color:{accentHex};"
                                                }

                                                label
                                            }
                                    }
                                )

                                FluentIcon'' {
                                    slot' "start"
                                    Value(Icons.Filled.Size20.RectangleLandscape())
                                    style' $"color:{accentColor};"
                                }

                                localizer["AccentColor"]
                            }

                        let languageItems =
                            FluentMenuItem'' {
                                MenuItems(
                                    fragment {
                                        for (languageName, culture) in [ "English", "en-US"; "Русский", "ru-RU" ] do
                                            FluentMenuItem'' {
                                                role MenuItemRole.Radio
                                                checked' (cultureName = culture)

                                                CheckedChanged(fun isChecked ->
                                                    task {
                                                        if isChecked.GetValueOrDefault() then
                                                            options.Value.CultureName <- culture
                                                            options.Value.ApplyCulture()
                                                            lock options.Value (fun () -> options.Value.Save())
                                                            store.CultureName.Publish culture
                                                    })

                                                languageName
                                            }
                                    }
                                )

                                FluentIcon'' {
                                    slot' "start"
                                    Value(Icons.Regular.Size20.LocalLanguage())
                                }

                                localizer["Language"]
                            }

                        let githubItem =
                            FluentMenuItem'' {
                                OnClick(fun _ -> los.OpenUrl "https://github.com/poimenov/RadioBrowser")

                                FluentIcon'' {
                                    slot' "start"
                                    Value(Github())
                                }

                                localizer["GitHub"]
                            }

                        let themeItem =
                            FluentMenuItem'' {
                                OnClick(fun _ ->
                                    task {
                                        let updatedTheme = if theme = Dark then Light else Dark
                                        store.Theme.Publish updatedTheme

                                        do!
                                            themeService.SetThemeAsync(
                                                if updatedTheme = Dark then
                                                    ThemeMode.Dark
                                                else
                                                    ThemeMode.Light
                                            )

                                        do!
                                            jsRuntime.InvokeVoidAsync(
                                                "applyRadioBrowserTheme",
                                                (if updatedTheme = Dark then "dark" else "light"),
                                                accentColor
                                            )
                                    })

                                FluentIcon'' {
                                    slot' "start"

                                    Value(
                                        if theme = Dark then
                                            Icons.Regular.Size20.WeatherSunny() :> Icon
                                        else
                                            Icons.Regular.Size20.WeatherMoon() :> Icon
                                    )
                                }

                                localizer["SwitchTheme"]
                            }

                        FluentButton'' {
                            id "settingsButton"
                            Appearance ButtonAppearance.Primary
                            class' "header-settings-button"
                            IconStart(Icons.Regular.Size20.Settings())
                        }

                        FluentMenu'' {
                            Trigger "settingsButton"

                            FluentMenuList'' {
                                class' "settings-menu-list"
                                themeItem
                                colorItems
                                languageItems
                                githubItem
                            }
                        }
                    }
                }
            })
