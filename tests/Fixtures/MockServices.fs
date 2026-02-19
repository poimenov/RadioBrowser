namespace RadioBrowser.Tests

open Moq
open RadioBrowser

module MockServices =

    /// Creates a mock IPlatformService for testing
    let createMockPlatformService (returnPlatform: Platform) : Mock<IPlatformService> =
        let mock = Mock<IPlatformService>()
        mock.Setup(fun m -> m.GetPlatform()).Returns(returnPlatform) |> ignore
        mock

    /// Creates a mock IProcessService for testing
    let createMockProcessService () : Mock<IProcessService> =
        let mock = Mock<IProcessService>()

        mock.Setup(fun m -> m.Run(It.IsAny<string>(), It.IsAny<string>())).Callback<string, string>(fun cmd args -> ())
        |> ignore

        mock

    /// Creates a mock ILinkOpeningService for testing
    let createMockLinkOpeningService () : Mock<ILinkOpeningService> =
        let mock = Mock<ILinkOpeningService>()

        mock.Setup(fun m -> m.OpenUrl(It.IsAny<string>())).Callback<string>(fun url -> ())
        |> ignore

        mock

    /// Creates a mock IApiUrlProvider for testing
    let createMockApiUrlProvider (returnUrl: string) : Mock<IApiUrlProvider> =
        let mock = Mock<IApiUrlProvider>()
        mock.Setup(fun m -> m.GetUrl()).Returns(returnUrl) |> ignore
        mock
