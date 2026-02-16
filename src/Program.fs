module Program

open System
open System.Globalization
open System.IO
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Options
open Microsoft.FluentUI.AspNetCore.Components
open log4net.Config
open Photino.Blazor
open RadioBrowser
open System.Net.Http

[<STAThread>]
[<EntryPoint>]
let main args =
    let DATA_DIRECTORY = "DATA_DIRECTORY"
    let builder = PhotinoBlazorAppBuilder.CreateDefault args
    builder.RootComponents.Add<AppComponent> "#app"

    let configuration =
        ConfigurationBuilder()
            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
            .AddJsonFile(
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, AppSettings.AppConfigFileName),
                true,
                false
            )
            .Build()

    builder.Services.AddFunBlazorWasm() |> ignore
    builder.Services.AddFluentUIComponents() |> ignore

    builder.Services.AddLogging(fun logging -> logging.ClearProviders().AddLog4Net() |> ignore)
    |> ignore

    builder.Services.AddLocalization(fun options -> options.ResourcesPath <- "Resources")
    |> ignore

    builder.Services.AddSingleton<IConfiguration> configuration |> ignore
    builder.Services.Configure<AppSettings> configuration |> ignore
    builder.Services.AddSingleton<IPlatformService, PlatformService>() |> ignore
    builder.Services.AddSingleton<IProcessService, ProcessService>() |> ignore

    builder.Services.AddSingleton<ILinkOpeningService, LinkOpeningService>()
    |> ignore

    builder.Services.AddScoped<IApiUrlProvider, ApiUrlProvider>() |> ignore
    builder.Services.AddScoped<IHttpHandler, HttpHandler>() |> ignore
    builder.Services.AddScoped<IStationsService, StationsService>() |> ignore
    builder.Services.AddScoped<IListsService, ListsService>() |> ignore
    builder.Services.AddScoped<IServices, Services>() |> ignore

    builder.Services.AddHttpClient(
        radioBrowserHttpClientName,
        fun client -> client.DefaultRequestHeaders.UserAgent.ParseAdd radioBrowserHttpClientName
    )
    |> ignore

    builder.Services.AddScoped<IMetadataService, MetadataService>() |> ignore

    builder.Services.AddScoped<IFavoritesDataAccess, FavoritesDataAccess>()
    |> ignore

    builder.Services.AddScoped<IHistoryDataAccess, HistoryDataAccess>() |> ignore

    let application = builder.Build()
    AppDomain.CurrentDomain.SetData("DataDirectory", AppSettings.AppDataPath)
    Environment.SetEnvironmentVariable(DATA_DIRECTORY, AppSettings.AppDataPath)
    let logger = application.Services.GetRequiredService<ILogger<_>>()
    logger.LogInformation "Starting application"

    match FileInfo AppSettings.LogConfigPath with
    | file when file.Exists -> XmlConfigurator.Configure file |> ignore
    | _ -> logger.LogWarning "Config file not found"

    let settings = application.Services.GetRequiredService<IOptions<AppSettings>>()
    CultureInfo.DefaultThreadCurrentCulture <- CultureInfo.GetCultureInfo settings.Value.CultureName
    CultureInfo.DefaultThreadCurrentUICulture <- CultureInfo.GetCultureInfo settings.Value.CultureName

    // customize window
    application.MainWindow
        .RegisterSizeChangedHandler(
            EventHandler<Drawing.Size>(fun _ args ->
                settings.Value.WindowWidth <- args.Width
                settings.Value.WindowHeight <- args.Height
                lock settings.Value (fun () -> settings.Value.Save()))
        )
        .SetSize(settings.Value.WindowWidth, settings.Value.WindowHeight)
        .SetIconFile(Path.Combine(AppSettings.WwwRootFolderName, AppSettings.FavIconFileName))
        .SetTitle
        AppSettings.ApplicationName
    |> ignore

    AppDomain.CurrentDomain.UnhandledException.Add(fun e ->
        match e.ExceptionObject with
        | :? Exception as ex ->
            application.Services.GetRequiredService<ILogger<_>>().LogError(ex, ex.Message)
            application.MainWindow.ShowMessage(ex.Message, "Error") |> ignore
        | _ ->
            application.Services
                .GetRequiredService<ILogger<_>>()
                .LogError("Non-exception thrown: {0}", e.ExceptionObject))

    application.Run()
    0
