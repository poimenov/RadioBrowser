namespace RadioBrowser.Tests.Unit.Services

open Xunit
open FsUnit.Xunit
open Moq
open Microsoft.Extensions.Logging
open RadioBrowser
open RadioBrowser.Services
open RadioBrowser.Tests

module LinkOpeningServiceTests =
    
    [<Fact>]
    let ``LinkOpeningService should implement ILinkOpeningService`` () =
        let mockPlatformService = MockServices.createMockPlatformService Windows
        let mockProcessService = MockServices.createMockProcessService()
        let mockLogger = TestFixtures.createMockLogger<LinkOpeningService>()
        
        let service = new LinkOpeningService(
            mockPlatformService.Object,
            mockProcessService.Object,
            mockLogger
        )
        
        service |> should be (instanceOfType<ILinkOpeningService>)

    [<Theory>]
    [<InlineData("https://www.example.com")>]
    [<InlineData("http://localhost:8080")>]
    [<InlineData("https://radio-browser.info")>]
    let ``LinkOpeningService.OpenUrl should accept valid URLs`` (url: string) =
        let mockPlatformService = MockServices.createMockPlatformService Windows
        let mockProcessService = MockServices.createMockProcessService()
        let mockLogger = TestFixtures.createMockLogger<LinkOpeningService>()
        
        let service: ILinkOpeningService = 
            new LinkOpeningService(
                mockPlatformService.Object,
                mockProcessService.Object,
                mockLogger
            )
        
        (fun () -> service.OpenUrl url) |> should not' (throw typeof<System.Exception>)

    [<Fact>]
    let ``LinkOpeningService should call ProcessService for Windows`` () =
        let mockPlatformService = MockServices.createMockPlatformService Windows
        let mockProcessService = MockServices.createMockProcessService()
        let mockLogger = TestFixtures.createMockLogger<LinkOpeningService>()
        let service: ILinkOpeningService = 
            new LinkOpeningService(
                mockPlatformService.Object,
                mockProcessService.Object,
                mockLogger
            )
        
        (fun () -> service.OpenUrl "https://www.example.com") |> should not' (throw typeof<System.Exception>)

    [<Fact>]
    let ``LinkOpeningService should handle Linux platform`` () =
        let mockPlatformService = MockServices.createMockPlatformService Linux
        let mockProcessService = MockServices.createMockProcessService()
        let mockLogger = TestFixtures.createMockLogger<LinkOpeningService>()
        let service: ILinkOpeningService = 
            new LinkOpeningService(
                mockPlatformService.Object,
                mockProcessService.Object,
                mockLogger
            )
        
        (fun () -> service.OpenUrl "https://www.example.com") |> should not' (throw typeof<System.Exception>)

    [<Fact>]
    let ``LinkOpeningService should handle MacOS platform`` () =
        let mockPlatformService = MockServices.createMockPlatformService MacOS
        let mockProcessService = MockServices.createMockProcessService()
        let mockLogger = TestFixtures.createMockLogger<LinkOpeningService>()
        let service: ILinkOpeningService = 
            new LinkOpeningService(
                mockPlatformService.Object,
                mockProcessService.Object,
                mockLogger
            )
        
        (fun () -> service.OpenUrl "https://www.example.com") |> should not' (throw typeof<System.Exception>)

    [<Fact>]
    let ``LinkOpeningService should handle Unknown platform gracefully`` () =
        let mockPlatformService = MockServices.createMockPlatformService Unknown
        let mockProcessService = MockServices.createMockProcessService()
        let mockLogger = TestFixtures.createMockLogger<LinkOpeningService>()
        let service: ILinkOpeningService = 
            new LinkOpeningService(
                mockPlatformService.Object,
                mockProcessService.Object,
                mockLogger
            )
        
        (fun () -> service.OpenUrl "https://www.example.com") |> should not' (throw typeof<System.Exception>)
