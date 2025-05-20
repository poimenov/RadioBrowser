[<AutoOpen>]
module RadioBrowser.AppSettings

open System
open System.IO
open System.Reflection
open Microsoft.FluentUI.AspNetCore.Components
open System.Globalization

type public AppSettings() =
    static member ApplicationName = "RadioBrowser"
    static member FavIconFileName = "favicon.ico"
    static member DataBaseFileName = $"{AppSettings.ApplicationName}.db"
    static member LogConfigFileName = "log4net.config"
    static member AppConfigFileName = "appsettings.json"
    static member WwwRootFolderName = "wwwroot"

    static member AppDataPath =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            AppSettings.ApplicationName
        )

    static member AssemblyFolderPath =
        Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)

    static member DataBasePath =
        Path.Combine(AppSettings.AppDataPath, AppSettings.DataBaseFileName)

    static member LogConfigPath =
        Path.Combine(AppSettings.AssemblyFolderPath, AppSettings.LogConfigFileName)

    static member WwwRootFolderPath =
        Path.Combine(AppSettings.AssemblyFolderPath, AppSettings.WwwRootFolderName)

    member val WindowWidth: int = 1024 with get, set
    member val WindowHeight: int = 768 with get, set
    member val AccentColor: OfficeColor = OfficeColor.Windows with get, set
    member val CultureName: string = "en-US" with get, set
    member val LimitCount = 20 with get, set
    member val HideBroken = true with get, set
    member val CurrentRegion = RegionInfo.CurrentRegion with get
