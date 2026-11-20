namespace RadioBrowser.Tests.Unit

open System.Globalization
open Xunit
open Bunit
open FsUnit.Xunit
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Localization
open RadioBrowser.AppSettings
open RadioBrowser.Services
open RadioBrowser.Tests

module AppTests =

    [<Fact>]
    let ``localizer should use Russian resources for ru-RU culture`` () =
        let services = ServiceCollection()
        services.AddLogging() |> ignore
        services.AddLocalization(fun options -> options.ResourcesPath <- "Resources") |> ignore
        use serviceProvider = services.BuildServiceProvider()

        let localizer =
            serviceProvider.GetRequiredService<IStringLocalizer<RadioBrowser.Interfaces.SharedResources>>()

        let previousCulture = CultureInfo.CurrentUICulture

        try
            CultureInfo.CurrentUICulture <- CultureInfo.GetCultureInfo("en-US")
            let mutable activeCulture = CultureInfo.GetCultureInfo("en-US")

            let cultureAwareLocalizer =
                CultureAwareStringLocalizer<RadioBrowser.Interfaces.SharedResources>(
                    localizer,
                    fun () -> activeCulture
                )
                :> IStringLocalizer<RadioBrowser.Interfaces.SharedResources>

            activeCulture <- CultureInfo.GetCultureInfo("ru-RU")
            cultureAwareLocalizer["Language"].Value |> should equal "Язык"

            activeCulture <- CultureInfo.GetCultureInfo("en-US")
            cultureAwareLocalizer["Language"].Value |> should equal "Language"
        finally
            CultureInfo.CurrentUICulture <- previousCulture

    [<Fact>]
    let ``loadingState should return correct value`` () =
        // Arrange
        use context = TestFixtures.createBunitContext ()

        let mockStringLocalizer =
            MockServices.createMockStringLocalizer ([ "IsLoading", "Loading..." ] |> Map.ofList)

        // Act
        let fragment = RadioBrowser.WatchElement.loadingState mockStringLocalizer.Object

        // Assert
        use result = context.RenderNode fragment

        result.Markup
        |> should startWith "<div style=\"margin:auto;width:100px;\"><div>Loading...</div><fluent-spinner"


    [<Fact>]
    let ``getParameters should create GetStationParameters with correct values`` () =
        // Arrange
        let offset = 10
        let settings = AppSettings()
        settings.LimitCount <- 25
        settings.HideBroken <- false

        // Act
        let result = RadioBrowser.WatchElement.getParameters (offset, settings)

        // Assert
        result.Offset |> should equal offset
        result.Limit |> should equal settings.LimitCount
        result.Hidebroken |> should equal settings.HideBroken

    [<Fact>]
    let ``getParameters should use settings values`` () =
        // Arrange
        let offset = 0
        let settings = AppSettings()
        settings.LimitCount <- 50
        settings.HideBroken <- true

        // Act
        let result = RadioBrowser.WatchElement.getParameters (offset, settings)

        // Assert
        result.Offset |> should equal 0
        result.Limit |> should equal 50
        result.Hidebroken |> should equal true

    [<Fact>]
    let ``stationIcon should return non-null result`` () =
        // Arrange
        use context = TestFixtures.createBunitContext ()
        let testUrl = "http://example.com/favicon.ico"

        // Act
        let fragment = RadioBrowser.StationItem.stationIcon testUrl
        use result = context.RenderNode fragment

        // Assert
        result.MarkupMatches
            $"<img src=\"{testUrl}\" class=\"favicon\" loadingExperimental onerror=\"this.src = './images/radio.svg';\" />"

    [<Theory>]
    [<InlineData(null)>]
    [<InlineData("")>]
    [<InlineData("   ")>]
    let ``stationIcon should handle various inputs`` (iconUrl: string) =
        // Arrange
        use context = TestFixtures.createBunitContext ()

        // Act
        let fragment = RadioBrowser.StationItem.stationIcon iconUrl
        use result = context.RenderNode fragment

        // Assert
        result.MarkupMatches
            $"<img src=\"./images/radio.svg\" class=\"favicon\" loadingExperimental onerror=\"this.src = './images/radio.svg';\" />"
