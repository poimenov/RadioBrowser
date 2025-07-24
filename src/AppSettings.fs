[<AutoOpen>]
module RadioBrowser.AppSettings

open System
open System.Globalization
open System.IO
open System.Reflection
open System.Text.Json
open System.Text.Json.Serialization
open Microsoft.FluentUI.AspNetCore.Components

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
    //Possible values of the DefaultOrder: name, url, homepage, favicon, tags, country, state, language, votes, codec,
    //bitrate, lastcheckok, lastchecktime, clicktimestamp, clickcount, clicktrend, changetimestamp, random
    member val DefaultOrder = "votes" with get, set
    member val ReverseOrder = true with get, set

    [<JsonIgnore>]
    member val CurrentRegion = RegionInfo.CurrentRegion with get

    // The prefer codec used for the audio stream, e.g., "mp3", "aac", etc. (https://fi1.api.radio-browser.info/json/codecs)
    member val Codec: string = null with get, set
    // The prefer language of the audio stream, e.g., "german,english", etc. (https://fi1.api.radio-browser.info/json/languages)
    member val Language: string = null with get, set

    member this.Save() =
        let filePath =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, AppSettings.AppConfigFileName)

        if File.Exists filePath then
            let options = JsonSerializerOptions(WriteIndented = true)
            let jsonString = JsonSerializer.Serialize(this, options)
            File.WriteAllText(filePath, jsonString)
