namespace RadioBrowser.Tests.Unit.Services

open Xunit
open Moq
open Microsoft.Extensions.Logging
open RadioBrowser
open RadioBrowser.LinkOpeningService
open RadioBrowser.Tests

module LinkOpeningServiceTests =

    let private assertWarningLogged (logger: Mock<ILogger<LinkOpeningService>>) =
        let warningLogged =
            logger.Invocations
            |> Seq.exists (fun invocation ->
                invocation.Method.Name = "Log"
                && unbox<LogLevel> invocation.Arguments.[0] = LogLevel.Warning)

        Assert.True warningLogged

    [<Fact>]
    let ``LinkOpeningService should implement ILinkOpeningService`` () =
        let mockLogger = TestFixtures.createMockLogger<LinkOpeningService> ()
        let mockProcessLauncher = MockServices.createMockProcessLauncher ()
        let service = new LinkOpeningService(mockLogger, mockProcessLauncher.Object)

        service
        |> Assert.IsAssignableFrom<RadioBrowser.Interfaces.ILinkOpeningService>
        |> ignore

    [<Theory>]
    [<InlineData("javascript:alert(1)")>]
    [<InlineData("file:///etc/passwd")>]
    [<InlineData("ftp://example.com/file")>]
    let ``LinkOpeningService should reject unsupported URL schemes`` (url: string) =
        let mockLogger = TestFixtures.createMockLoggerWithVerify<LinkOpeningService> ()
        let mockProcessLauncher = MockServices.createMockProcessLauncher ()

        let service: RadioBrowser.Interfaces.ILinkOpeningService =
            new LinkOpeningService(mockLogger.Object, mockProcessLauncher.Object)

        service.OpenUrl url
        assertWarningLogged mockLogger

    [<Theory>]
    [<InlineData("not a url")>]
    [<InlineData(" https://example.com")>]
    [<InlineData("https://user:password@example.com")>]
    let ``LinkOpeningService should reject malformed or deceptive URLs`` (url: string) =
        let mockLogger = TestFixtures.createMockLoggerWithVerify<LinkOpeningService> ()
        let mockProcessLauncher = MockServices.createMockProcessLauncher ()

        let service: RadioBrowser.Interfaces.ILinkOpeningService =
            new LinkOpeningService(mockLogger.Object, mockProcessLauncher.Object)

        service.OpenUrl url
        assertWarningLogged mockLogger

    [<Theory>]
    [<InlineData("https://www.youtube.com/results?search_query=%D0%92%D0%BB%D0%B0%D0%B4%D0%B8%D0%BC%D0%B8%D1%80")>]
    [<InlineData("https://www.youtube.com/results?search_query=test%20%26%20space")>]
    let ``LinkOpeningService should accept valid encoded URLs`` (url: string) =
        let mockLogger = TestFixtures.createMockLoggerWithVerify<LinkOpeningService> ()
        let mockProcessLauncher = MockServices.createMockProcessLauncher ()

        let service: RadioBrowser.Interfaces.ILinkOpeningService =
            new LinkOpeningService(mockLogger.Object, mockProcessLauncher.Object)

        service.OpenUrl url

        let warningLogged =
            mockLogger.Invocations
            |> Seq.exists (fun invocation ->
                invocation.Method.Name = "Log"
                && unbox<LogLevel> invocation.Arguments.[0] = LogLevel.Warning)

        Assert.False warningLogged
