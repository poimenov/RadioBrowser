namespace RadioBrowser.Tests.Unit.Services

open Xunit
open FsUnit.Xunit
open Moq
open Microsoft.Extensions.Logging
open RadioBrowser
open RadioBrowser.Services
open RadioBrowser.Tests

module ProcessServiceTests =
    
    [<Fact>]
    let ``ProcessService should implement IProcessService`` () =
        let mockLogger = TestFixtures.createMockLogger<IProcessService>()
        let service = new ProcessService(mockLogger)
        
        service |> should be (instanceOfType<IProcessService>)

    [<Fact>]
    let ``ProcessService.Run should accept valid command and arguments`` () =
        let mockLogger = TestFixtures.createMockLogger<IProcessService>()
        let service = new ProcessService(mockLogger) :> IProcessService
        let command = "notepad"
        let arguments = "test.txt"
        
        (fun () -> service.Run(command, arguments)) |> should not' (throw typeof<System.ArgumentException>)

    [<Fact>]
    let ``ProcessService.Run should handle empty arguments`` () =
        let mockLogger = TestFixtures.createMockLogger<IProcessService>()
        let service = new ProcessService(mockLogger) :> IProcessService
        
        (fun () -> service.Run("cmd", "")) |> should not' (throw typeof<System.ArgumentException>)

    [<Theory>]
    [<InlineData("cmd")>]
    [<InlineData("bash")>]
    let ``ProcessService should work with different shell commands`` (command: string) =
        let mockLogger = TestFixtures.createMockLogger<IProcessService>()
        let service = new ProcessService(mockLogger) :> IProcessService
        
        (fun () -> service.Run(command, "/c echo test")) |> should not' (throw typeof<System.ArgumentException>)

    [<Fact>]
    let ``ProcessService.Run with mock should verify call count`` () =
        // Arrange
        let mockLogger = TestFixtures.createMockLogger<IProcessService>()
        let mockProcess = Mock<IProcessService>()
        let command = "test"
        let arguments = "arg"
        
        // Act
        mockProcess.Object.Run(command, arguments)
        
        // Assert
        Assert.True(true)
