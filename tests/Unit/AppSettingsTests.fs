namespace RadioBrowser.Tests.Unit

open Xunit
open FsUnit.Xunit
open RadioBrowser

module AppSettingsTests =
    
    [<Fact>]
    let ``AppSettings.ApplicationName should be RadioBrowser`` () =
        AppSettings.ApplicationName |> should equal "RadioBrowser"

    [<Fact>]
    let ``AppSettings static file names should be consistent`` () =
        AppSettings.FavIconFileName |> should not' (be EmptyString)
        AppSettings.DataBaseFileName |> should not' (be EmptyString)
        AppSettings.LogConfigFileName |> should not' (be EmptyString)
        AppSettings.AppConfigFileName |> should not' (be EmptyString)
        AppSettings.WwwRootFolderName |> should not' (be EmptyString)

    [<Fact>]
    let ``AppSettings data base file name should contain application name`` () =
        (AppSettings.DataBaseFileName.Contains(AppSettings.ApplicationName)) |> should be True

    [<Fact>]
    let ``AppSettings.AppDataPath should not be empty`` () =
        AppSettings.AppDataPath |> should not' (be EmptyString)

    [<Fact>]
    let ``AppSettings.DataBasePath should end with database filename`` () =
        AppSettings.DataBasePath |> should endWith AppSettings.DataBaseFileName

    [<Fact>]
    let ``AppSettings.ConnectionString should contain Filename`` () =
        (AppSettings.ConnectionString.Contains("Filename=")) |> should be True
        (AppSettings.ConnectionString.Contains(AppSettings.DataBasePath)) |> should be True

    [<Fact>]
    let ``AppSettings instance should have default values`` () =
        let settings = new AppSettings()
        settings.WindowWidth |> should equal 1024
        settings.WindowHeight |> should equal 768
        settings.LimitCount |> should equal 20
        settings.HideBroken |> should be True
        settings.DefaultOrder |> should equal "votes"
        settings.ReverseOrder |> should be True

    [<Fact>]
    let ``AppSettings window dimensions should be positive`` () =
        let settings = new AppSettings()
        settings.WindowWidth |> should be (greaterThan 0)
        settings.WindowHeight |> should be (greaterThan 0)

    [<Fact>]
    let ``AppSettings can be configured with custom values`` () =
        let settings = new AppSettings()
        let newWidth = 1920
        let newHeight = 1080
        let newLimit = 50
        
        settings.WindowWidth <- newWidth
        settings.WindowHeight <- newHeight
        settings.LimitCount <- newLimit
        
        settings.WindowWidth |> should equal newWidth
        settings.WindowHeight |> should equal newHeight
        settings.LimitCount |> should equal newLimit

    [<Theory>]
    [<InlineData("en-US")>]
    [<InlineData("ru-RU")>]
    [<InlineData("de-DE")>]
    let ``AppSettings culture name can be set to valid cultures`` (cultureName: string) =
        let settings = new AppSettings()
        settings.CultureName <- cultureName
        settings.CultureName |> should equal cultureName
