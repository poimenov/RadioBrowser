[<AutoOpen>]
module RadioBrowser.AppHeader

open Microsoft.Extensions.Localization
open Microsoft.Extensions.Options
open Microsoft.FluentUI.AspNetCore.Components
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

                    FluentButton'' {
                        Appearance Appearance.Accent
                        IconStart(Github())
                        title' (string (localizer["GitHub"]))

                        OnClick(fun _ -> los.OpenUrl "https://github.com/poimenov/RadioBrowser")
                    }

                    adapt {
                        let! theme = store.Theme

                        if store.GetTitleDelay.Value <> options.Value.GetTitleDelay then
                            store.GetTitleDelay.Publish options.Value.GetTitleDelay

                        if store.HistoryTruncateCount.Value <> options.Value.HistoryTruncateCount then
                            store.HistoryTruncateCount.Publish options.Value.HistoryTruncateCount

                        FluentDesignTheme'' {
                            StorageName "theme"
                            Mode store.Theme.Value
                            OfficeColor options.Value.AccentColor

                            OnLoaded(fun args ->
                                if args.IsDark then
                                    store.Theme.Publish DesignThemeModes.Dark)
                        }

                        FluentButton'' {
                            Appearance Appearance.Accent
                            IconStart(Icons.Regular.Size20.DarkTheme())
                            title' (string (localizer["SwitchTheme"]))

                            OnClick(fun _ ->
                                store.Theme.Publish(
                                    if theme = DesignThemeModes.Dark then
                                        DesignThemeModes.Light
                                    else
                                        DesignThemeModes.Dark
                                ))
                        }
                    }
                }
            })
