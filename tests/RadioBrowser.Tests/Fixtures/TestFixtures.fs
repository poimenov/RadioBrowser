namespace RadioBrowser.Tests

open System
open Microsoft.Extensions.Logging
open Microsoft.Extensions.DependencyInjection
open Microsoft.AspNetCore.Components.Routing
open Moq
open Bunit

module TestFixtures =
    /// Creates a BunitContext with necessary services for testing Blazor components
    let createBunitContext () =
        let textContext = new BunitContext()

        textContext.Services.AddScoped<INavigationInterception>(fun _ -> Mock.Of<INavigationInterception>())
        |> ignore

        textContext

    /// Creates a mock logger for use in tests
    let createMockLogger<'T> () : ILogger<'T> = Mock<ILogger<'T>>().Object

    /// Creates a more detailed mock logger with call verification
    let createMockLoggerWithVerify<'T> () : Mock<ILogger<'T>> = Mock<ILogger<'T>>()

    /// Helper function to check if a logger was called with a certain level
    let verifyLoggerCall (logMock: Mock<ILogger<'T>>) (logLevel: LogLevel) =
        logMock.Verify(
            (fun l ->
                l.Log(
                    logLevel,
                    It.IsAny<EventId>(),
                    It.IsAny<obj>(),
                    It.IsAny<exn>(),
                    It.IsAny<Func<obj, exn, string>>()
                )),
            Times.AtLeastOnce()
        )
