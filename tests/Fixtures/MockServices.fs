namespace RadioBrowser.Tests

open System
open System.Diagnostics
open Microsoft.Extensions.Logging
open Moq
open RadioBrowser

module MockServices =
    open RadioBrowser.Services

    /// Создает mock IPlatformService для тестирования
    let createMockPlatformService (returnPlatform: Platform) : Mock<IPlatformService> =
        let mock = Mock<IPlatformService>()
        mock
            .Setup(fun m -> m.GetPlatform())
            .Returns(returnPlatform)
        |> ignore
        mock

    /// Создает mock IProcessService для тестирования
    let createMockProcessService() : Mock<IProcessService> =
        let mock = Mock<IProcessService>()
        mock
            .Setup(fun m -> m.Run(It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string>(fun cmd args -> ())
        |> ignore
        mock

    /// Создает mock ILinkOpeningService для тестирования
    let createMockLinkOpeningService() : Mock<ILinkOpeningService> =
        let mock = Mock<ILinkOpeningService>()
        mock
            .Setup(fun m -> m.OpenUrl(It.IsAny<string>()))
            .Callback<string>(fun url -> ())
        |> ignore
        mock

    /// Создает mock IApiUrlProvider для тестирования
    let createMockApiUrlProvider (returnUrl: string) : Mock<IApiUrlProvider> =
        let mock = Mock<IApiUrlProvider>()
        mock
            .Setup(fun m -> m.GetUrl())
            .Returns(returnUrl)
        |> ignore
        mock
