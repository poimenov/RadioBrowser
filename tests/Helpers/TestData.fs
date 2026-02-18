namespace RadioBrowser.Tests

module TestData =
    /// Тестовые данные для настроек приложения
    let validAppSettings = 
        {| 
            WindowWidth = 1024
            WindowHeight = 768
            LimitCount = 20
            HideBroken = true
            DefaultOrder = "votes"
            ReverseOrder = true
        |}

    let invalidAppSettings =
        {|
            WindowWidth = -100
            WindowHeight = -100
            LimitCount = 0
            HideBroken = true
            DefaultOrder = ""
            ReverseOrder = false
        |}

    /// Тестовые URL'ы
    let testApiUrl = "https://test.api.radio-browser.info"
    let fallbackApiUrl = "https://fallback.api.radio-browser.info"
    let testBrowserUrl = "https://www.radio-browser.info"
