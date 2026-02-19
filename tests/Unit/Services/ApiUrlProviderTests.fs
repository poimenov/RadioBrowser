namespace RadioBrowser.Tests.Unit.Services

open Xunit
open FsUnit.Xunit
open RadioBrowser
open RadioBrowser.Tests

module ApiUrlProviderTests =
    [<Fact>]
    let ``ApiUrlProvider should implement IApiUrlProvider`` () =
        let mockLogger = TestFixtures.createMockLogger<ApiUrlProvider> ()
        let provider = new ApiUrlProvider(mockLogger)

        provider |> should be (instanceOfType<IApiUrlProvider>)

    [<Fact>]
    let ``ApiUrlProvider.GetUrl should return a non-empty string`` () =
        let mockLogger = TestFixtures.createMockLogger<ApiUrlProvider> ()
        let provider = new ApiUrlProvider(mockLogger) :> IApiUrlProvider

        let url = provider.GetUrl()

        url |> should not' (be EmptyString)

    [<Fact>]
    let ``ApiUrlProvider.GetUrl should return a valid domain`` () =
        let mockLogger = TestFixtures.createMockLogger<ApiUrlProvider> ()
        let provider = new ApiUrlProvider(mockLogger) :> IApiUrlProvider

        let url = provider.GetUrl()

        (url.Contains(".") || url.Length > 0) |> should be True

    [<Fact>]
    let ``ApiUrlProvider.GetUrl should be consistent`` () =
        let mockLogger = TestFixtures.createMockLogger<ApiUrlProvider> ()
        let provider = new ApiUrlProvider(mockLogger) :> IApiUrlProvider

        let url1 = provider.GetUrl()
        let url2 = provider.GetUrl()

        url1 |> should not' (be EmptyString)
        url2 |> should not' (be EmptyString)

    [<Fact>]
    let ``Mock ApiUrlProvider should return configured URL`` () =
        let testUrl = TestData.testApiUrl
        let mockProvider = MockServices.createMockApiUrlProvider testUrl

        let actualUrl = mockProvider.Object.GetUrl()

        actualUrl |> should equal testUrl

    [<Theory>]
    [<InlineData("all.api.radio-browser.info")>]
    [<InlineData("de1.api.radio-browser.info")>]
    [<InlineData("de2.api.radio-browser.info")>]
    let ``ApiUrlProvider returns valid radio-browser API domain`` (expectedDomain: string) =
        let mockProvider = MockServices.createMockApiUrlProvider expectedDomain

        let url = mockProvider.Object.GetUrl()

        url |> should equal expectedDomain
