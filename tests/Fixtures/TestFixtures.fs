namespace RadioBrowser.Tests

open System
open Microsoft.Extensions.Logging
open Moq

module TestFixtures =
    /// Создает mock логгера для использования в тестах
    let createMockLogger<'T>() : ILogger<'T> =
        Mock<ILogger<'T>>().Object

    /// Создает более детальный mock логгера с проверкой вызовов
    let createMockLoggerWithVerify<'T>() : Mock<ILogger<'T>> =
        Mock<ILogger<'T>>()

    /// Вспомогательная функция для проверки, был ли вызван логгер с определенным уровнем
    let verifyLoggerCall (logMock: Mock<ILogger<'T>>) (logLevel: LogLevel) =
        logMock.Verify(
            (fun l -> l.Log(
                logLevel,
                It.IsAny<EventId>(),
                It.IsAny<obj>(),
                It.IsAny<exn>(),
                It.IsAny<Func<obj, exn, string>>()
            )),
            Times.AtLeastOnce()
        )
