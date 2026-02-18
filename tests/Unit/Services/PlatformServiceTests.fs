namespace RadioBrowser.Tests.Unit.Services

open Xunit
open FsUnit.Xunit
open Moq
open RadioBrowser
open RadioBrowser.Services
open RadioBrowser.Tests

module PlatformServiceTests =
    
    [<Fact>]
    let ``PlatformService.GetPlatform should return a valid platform`` () =
        let service = new PlatformService() :> IPlatformService
        let platform = service.GetPlatform()
        
        let validPlatforms = [Windows; Linux; MacOS; Unknown]
        (validPlatforms |> List.contains platform) |> should be True

    [<Fact>]
    let ``PlatformService should implement IPlatformService`` () =
        let service = new PlatformService()
        service |> should be (instanceOfType<IPlatformService>)

    [<Fact>]
    let ``PlatformService.GetPlatform should be consistent`` () =
        let service = new PlatformService() :> IPlatformService
        let platform1 = service.GetPlatform()
        let platform2 = service.GetPlatform()
        let platform3 = service.GetPlatform()
        
        platform1 |> should equal platform2
        platform2 |> should equal platform3

    [<Theory>]
    [<InlineData(0)>]  // Windows = 0
    [<InlineData(1)>]  // Linux = 1
    [<InlineData(2)>]  // MacOS = 2
    [<InlineData(3)>]  // Unknown = 3
    let ``Mock PlatformService should return configured platform`` (platformId: int) =
        let expectedPlatform = match platformId with
                               | 0 -> Windows
                               | 1 -> Linux
                               | 2 -> MacOS
                               | _ -> Unknown
        let mockPlatformService = MockServices.createMockPlatformService expectedPlatform
        let service = mockPlatformService.Object
        
        let actualPlatform = service.GetPlatform()
        
        actualPlatform |> should equal expectedPlatform
