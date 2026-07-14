namespace RadioBrowser.Tests.Unit.Services

open Xunit
open FsUnit.Xunit
open Moq
open RadioBrowser
open RadioBrowser.PlatformService
open RadioBrowser.Tests

module PlatformServiceTests =

    [<Fact>]
    let ``PlatformService.GetPlatform should return a valid platform`` () =
        let service = new PlatformService() :> RadioBrowser.Interfaces.IPlatformService
        let platform = service.GetPlatform()

        let validPlatforms =
            [ RadioBrowser.Interfaces.Platform.Windows
              RadioBrowser.Interfaces.Platform.Linux
              RadioBrowser.Interfaces.Platform.MacOS
              RadioBrowser.Interfaces.Platform.Unknown ]

        (validPlatforms |> List.contains platform) |> should be True

    [<Fact>]
    let ``PlatformService should implement IPlatformService`` () =
        let service = new PlatformService()
        service |> should be (instanceOfType<RadioBrowser.Interfaces.IPlatformService>)

    [<Fact>]
    let ``PlatformService.GetPlatform should be consistent`` () =
        let service = new PlatformService() :> RadioBrowser.Interfaces.IPlatformService
        let platform1 = service.GetPlatform()
        let platform2 = service.GetPlatform()
        let platform3 = service.GetPlatform()

        platform1 |> should equal platform2
        platform2 |> should equal platform3

    [<Theory>]
    [<InlineData(0)>] // Windows = 0
    [<InlineData(1)>] // Linux = 1
    [<InlineData(2)>] // MacOS = 2
    [<InlineData(3)>] // Unknown = 3
    let ``Mock PlatformService should return configured platform`` (platformId: int) =
        let expectedPlatform =
            match platformId with
            | 0 -> RadioBrowser.Interfaces.Platform.Windows
            | 1 -> RadioBrowser.Interfaces.Platform.Linux
            | 2 -> RadioBrowser.Interfaces.Platform.MacOS
            | _ -> RadioBrowser.Interfaces.Platform.Unknown

        let mockPlatformService = MockServices.createMockPlatformService expectedPlatform
        let service = mockPlatformService.Object

        let actualPlatform = service.GetPlatform()

        actualPlatform |> should equal expectedPlatform
