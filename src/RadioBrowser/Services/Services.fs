[<AutoOpen>]
module RadioBrowser.Services

open System.Globalization
open Microsoft.Extensions.Localization
open Microsoft.FluentUI.AspNetCore.Components
open Microsoft.JSInterop

type CultureAwareStringLocalizer<'T>(inner: IStringLocalizer, cultureProvider: unit -> CultureInfo) =
    let withCulture action =
        let currentCulture = CultureInfo.CurrentCulture
        let currentUICulture = CultureInfo.CurrentUICulture
        let culture = cultureProvider ()

        try
            CultureInfo.CurrentCulture <- culture
            CultureInfo.CurrentUICulture <- culture
            action ()
        finally
            CultureInfo.CurrentCulture <- currentCulture
            CultureInfo.CurrentUICulture <- currentUICulture

    interface IStringLocalizer with
        member _.Item
            with get (name: string) = withCulture (fun () -> inner.[name])

        member _.Item
            with get (name: string, arguments: obj array) =
                withCulture (fun () -> inner.[name, arguments])

        member _.GetAllStrings(includeParentCultures: bool) =
            withCulture (fun () ->
                inner.GetAllStrings(includeParentCultures)
                |> Seq.toArray
                :> seq<_>)

    interface IStringLocalizer<'T>

type Services
    (
        notificationService: INotificationService,
        stationsService: IStationsService,
        linkOpeningService: ILinkOpeningService,
        localizer: IStringLocalizer<SharedResources>,
        metadataService: IMetadataService,
        jsRuntime: IJSRuntime,
        historyDataAccess: IHistoryDataAccess,
        dialogService: IDialogService
    ) =
    interface IServices with
        member _.NotificationService = notificationService

        member _.FavoritesDataAccess: IFavoritesDataAccess =
            stationsService.FavoritesDataAccess

        member _.StationsService: IStationsService = stationsService
        member _.LinkOpeningService: ILinkOpeningService = linkOpeningService
        member _.Localizer: IStringLocalizer<SharedResources> = localizer
        member _.MetadataService: IMetadataService = metadataService
        member _.JsRuntime: IJSRuntime = jsRuntime
        member _.HistoryDataAccess = historyDataAccess
        member _.DialogService = dialogService
