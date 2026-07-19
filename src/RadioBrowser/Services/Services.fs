[<AutoOpen>]
module RadioBrowser.Services

open Microsoft.Extensions.Localization
open Microsoft.FluentUI.AspNetCore.Components
open Microsoft.JSInterop

type Services
    (
        toastService: IToastService,
        stationsService: IStationsService,
        linkOpeningService: ILinkOpeningService,
        localizer: IStringLocalizer<SharedResources>,
        metadataService: IMetadataService,
        jsRuntime: IJSRuntime,
        historyDataAccess: IHistoryDataAccess,
        dialogService: IDialogService
    ) =
    interface IServices with
        member _.ToastService = toastService

        member _.FavoritesDataAccess: IFavoritesDataAccess =
            stationsService.FavoritesDataAccess

        member _.StationsService: IStationsService = stationsService
        member _.LinkOpeningService: ILinkOpeningService = linkOpeningService
        member _.Localizer: IStringLocalizer<SharedResources> = localizer
        member _.MetadataService: IMetadataService = metadataService
        member _.JsRuntime: IJSRuntime = jsRuntime
        member _.HistoryDataAccess = historyDataAccess
        member _.DialogService = dialogService
