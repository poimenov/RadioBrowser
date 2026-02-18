namespace RadioBrowser.Tests.Unit

open Xunit
open FsUnit.Xunit
open RadioBrowser
open Microsoft.Extensions.DependencyInjection
open Bunit
open RadioBrowser.Tests.Extensions

module AppTests =
    open Microsoft.AspNetCore.Components.Routing
    open Moq

    let private createBunitContext () =
        let textContext = new BunitContext()

        textContext.Services.AddScoped<INavigationInterception>(fun _ -> Mock.Of<INavigationInterception>())
        |> ignore

        textContext

    [<Fact>]
    let ``getParameters should create GetStationParameters with correct values`` () =
        // Arrange
        let offset = 10
        let settings = AppSettings()
        settings.LimitCount <- 25
        settings.HideBroken <- false

        // Act
        let result = getParameters (offset, settings)

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
        let result = getParameters (offset, settings)

        // Assert
        result.Offset |> should equal 0
        result.Limit |> should equal 50
        result.Hidebroken |> should equal true

    [<Fact>]
    let ``stationIcon should return non-null result`` () =
        // Arrange
        use context = createBunitContext ()
        let testUrl = "http://example.com/favicon.ico"

        // Act
        let fragment = stationIcon testUrl
        use result = context.RenderNode fragment

        // Assert
        result.MarkupMatches
            $"<img src=\"{testUrl}\" class=\"favicon\" loadingExperimental onerror=\"this.src = './images/radio.svg';\" />"

    [<Fact>]
    let ``stationIcon should handle empty string`` () =
        // Arrange
        use context = createBunitContext ()

        // Act
        let fragment = stationIcon ""
        use result = context.RenderNode fragment

        // Assert
        result.MarkupMatches
            $"<img src=\"./images/radio.svg\" class=\"favicon\" loadingExperimental onerror=\"this.src = './images/radio.svg';\" />"

    [<Theory>]
    [<InlineData(null)>]
    [<InlineData("")>]
    [<InlineData("   ")>]
    let ``stationIcon should handle various inputs`` (iconUrl: string) =
        // Act
        let result = stationIcon iconUrl

        // Assert
        result |> should not' (be null)
