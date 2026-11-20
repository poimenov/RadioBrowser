[<AutoOpen>]
module RadioBrowser.AppHeader

open Microsoft.Extensions.Localization
open Microsoft.FluentUI.AspNetCore.Components
open Microsoft.JSInterop
open FSharp.Data.Adaptive
open Fun.Blazor

let appHeader =
    html.inject
        (fun
            (store: IShareStore,
             los: ILinkOpeningService,
             jsRuntime: IJSRuntime,
             themeService: IThemeService,
             localizer: IStringLocalizer<SharedResources>) ->
            let menuOpenState = cval false
            let accentMenuOpenState = cval false

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
                        let! menuOpen, setMenuOpen = menuOpenState.WithSetter()
                        let! accentMenuOpen, setAccentMenuOpen = accentMenuOpenState.WithSetter()

                        let colors =
                            [ "Blue", "#036ac4"
                              "Teal", "#008272"
                              "Purple", "#8764b8"
                              "Green", "#107c10"
                              "Orange", "#ca5010"
                              "Red", "#d13438" ]

                        let colorItems =
                            fragment {
                                for (label, accentHex) in colors do
                                    button {
                                        type' "button"
                                        class' "settings-menu-item"

                                        onclick (fun _ ->
                                            task {
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
                                                setAccentMenuOpen false
                                            })

                                        FluentIcon'' {
                                            Value(Icons.Filled.Size20.RectangleLandscape())
                                            style' $"color:{accentHex};"
                                        }

                                        span {
                                            style' "flex:1;"
                                            label
                                        }

                                        if accentHex = accentColor then
                                            FluentIcon'' { Value(Icons.Regular.Size20.Checkmark()) }
                                    }
                            }

                        FluentButton'' {
                            id "settingsButton"
                            Appearance ButtonAppearance.Primary
                            class' "header-settings-button"
                            IconStart(Icons.Regular.Size20.Settings())
                            OnClick(fun _ -> setMenuOpen (not menuOpen))
                        }

                        FluentPopover'' {
                            AnchorId "settingsButton"
                            Opened menuOpen
                            OpenedChanged(fun opened ->
                                setMenuOpen opened

                                if not opened then
                                    setAccentMenuOpen false)
                            style' "width:210px;"

                            if accentMenuOpen then
                                button {
                                    type' "button"
                                    class' "settings-menu-item"
                                    onclick (fun _ -> setAccentMenuOpen false)

                                    FluentIcon'' { Value(Icons.Regular.Size20.ChevronLeft()) }
                                    span { localizer["Accent Color"] }
                                }

                                colorItems
                            else
                                button {
                                    type' "button"
                                    class' "settings-menu-item"

                                    onclick (fun _ ->
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
                                        Value(
                                            if theme = Dark then
                                                Icons.Regular.Size20.WeatherSunny() :> Icon
                                            else
                                                Icons.Regular.Size20.WeatherMoon() :> Icon
                                        )
                                    }

                                    span { localizer["SwitchTheme"] }
                                }

                                button {
                                    type' "button"
                                    id "accentMenuButton"
                                    class' "settings-menu-item"
                                    onclick (fun _ -> setAccentMenuOpen true)

                                    FluentIcon'' {
                                        Value(Icons.Filled.Size20.RectangleLandscape())
                                        style' $"color:{accentColor};"
                                    }

                                    span {
                                        style' "flex:1;"
                                        localizer["Accent Color"]
                                    }

                                    FluentIcon'' { Value(Icons.Regular.Size20.ChevronRight()) }
                                }

                                button {
                                    type' "button"
                                    class' "settings-menu-item"
                                    onclick (fun _ -> los.OpenUrl "https://github.com/poimenov/RadioBrowser")

                                    FluentIcon'' {
                                        Value(Github())
                                    }

                                    span { localizer["GitHub"] }
                                }
                        }
                    }
                }
            })
