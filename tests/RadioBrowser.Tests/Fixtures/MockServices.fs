namespace RadioBrowser.Tests

open Moq
open RadioBrowser
open Microsoft.Extensions.Localization

module MockServices =

    /// Creates a mock IStringLocalizer with specified resource values
    let createMockStringLocalizer
        (resources: Map<string, string>)
        : Mock<IStringLocalizer<RadioBrowser.Interfaces.SharedResources>> =
        let mock = Mock<IStringLocalizer<RadioBrowser.Interfaces.SharedResources>>()

        mock
            .Setup(fun x -> x.Item(It.IsAny<string>()))
            .Returns(fun (name: string) ->
                match resources.TryFind name with
                | Some value -> LocalizedString(name, value)
                | None -> LocalizedString(name, "", false))
        |> ignore

        mock

    /// Creates a mock IProcessLauncher for testing
    let createMockProcessLauncher () : Mock<RadioBrowser.Interfaces.IProcessLauncher> =
        let mock = Mock<RadioBrowser.Interfaces.IProcessLauncher>()
        mock.Setup(fun m -> m.Start(It.IsAny<System.Diagnostics.ProcessStartInfo>())).Callback(fun _ -> ())
        |> ignore
        mock

    /// Creates a mock ILinkOpeningService for testing
    let createMockLinkOpeningService () : Mock<RadioBrowser.Interfaces.ILinkOpeningService> =
        let mock = Mock<RadioBrowser.Interfaces.ILinkOpeningService>()

        mock.Setup(fun m -> m.OpenUrl(It.IsAny<string>())).Callback<string>(fun url -> ())
        |> ignore

        mock

    /// Creates a mock IApiUrlProvider for testing
    let createMockApiUrlProvider (returnUrl: string) : Mock<RadioBrowser.Interfaces.IApiUrlProvider> =
        let mock = Mock<RadioBrowser.Interfaces.IApiUrlProvider>()
        mock.Setup(fun m -> m.GetUrl()).Returns(returnUrl) |> ignore
        mock
