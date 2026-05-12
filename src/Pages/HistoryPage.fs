[<AutoOpen>]
module RadioBrowser.HistoryPage

open System
open System.Globalization
open System.IO
open System.Linq
open System.Xml
open System.Xml.Linq
open System.Xml.Xsl
open Microsoft.FluentUI.AspNetCore.Components
open Fun.Blazor

let convertToXElement (record: HistoryRecord, settings: AppSettings) =
    XElement(
        "Record",
        [ XAttribute("StartTime", record.StartTime.ToString "o")
          XAttribute("Title", record.Title)
          XAttribute("Url", String.Format(settings.TrackSearchUrl, Uri.EscapeDataString record.Title))
          XAttribute("StationName", record.StationName) ]
    )

let transformXmlAsync (xmlContent: string) (xsltPath: string) (parameters: Map<string, string>) =
    async {
        try
            let xslt = new XslCompiledTransform()
            xslt.Load(xsltPath)

            let args = new XsltArgumentList()
            parameters |> Map.iter (fun k v -> args.AddParam(k, "", v))

            use xmlReader = XmlReader.Create(new StringReader(xmlContent))
            use outputWriter = new StringWriter()

            xslt.Transform(xmlReader, args, outputWriter)
            return Ok(outputWriter.ToString())
        with ex ->
            return Error ex.Message
    }

let openHistoryAsync
    (history: HistoryRecord list, parameters: list<string * string>, los: ILinkOpeningService, settings: AppSettings)
    =
    async {
        try
            let historyXml =
                XElement(
                    "History",
                    [ XAttribute("Date", DateTime.Today.ToLongDateString()) ],
                    history
                    |> List.sortByDescending (fun r -> r.StartTime)
                    |> List.map (fun r -> convertToXElement (r, settings))
                )
                    .ToString()

            let! htmlOutputResult =
                transformXmlAsync
                    historyXml
                    (Path.Combine(AppSettings.WwwRootFolderPath, "history.xslt"))
                    (Map.ofList parameters)


            match htmlOutputResult with
            | Error msg -> return Error msg
            | Ok htmlOutput ->
                let filePath = Path.Combine(AppSettings.AppDataPath, "history.htm")
                use streamWriter = new StreamWriter(filePath)
                do! streamWriter.WriteAsync htmlOutput |> Async.AwaitTask
                los.OpenUrl filePath
                return Ok()
        with ex ->
            return Error ex.Message
    }

let historyPage =
    html.inject (fun (store: IShareStore, services: IServices) ->
        adapt {
            store.HeaderTitle.Publish services.Localizer["History"]
            store.HeaderIcon.Publish(Icons.Regular.Size24.History())
            store.SearchMode.Publish History
            let! history, setHistory = store.History.WithSetter()

            if history.Length = 0 then
                div {
                    style' "text-align:center;"
                    services.Localizer["NoHistory"]
                }
            else
                let items =
                    history.OrderByDescending(fun r -> r.StartTime).AsQueryable<HistoryRecord>()

                FluentDataGrid'' {
                    Items items
                    AutoFit true

                    PropertyColumn'' {
                        Title(string (services.Localizer["Time"]))

                        Property(fun (item: HistoryRecord) -> item.StartTime.ToString("g", CultureInfo.CurrentCulture))
                    }

                    PropertyColumn'' {
                        Title(string (services.Localizer["Station"]))
                        Property(fun (item: HistoryRecord) -> item.StationName)
                    }

                    TemplateColumn'' {
                        Title(string (services.Localizer["Title"]))

                        ChildContent(fun (item: HistoryRecord) ->
                            let searchtext = string (services.Localizer["SearchOnYouTube"])

                            span {
                                class' "link"
                                title' $"{searchtext}: {item.Title}"

                                onclick (fun _ ->
                                    services.LinkOpeningService.OpenUrl(
                                        String.Format(
                                            services.StationsService.Settings.TrackSearchUrl,
                                            Uri.EscapeDataString item.Title
                                        )
                                    ))

                                item.Title
                            })
                    }
                }

                FluentButton'' {
                    Appearance Appearance.Accent
                    style' "margin:10px;"
                    Title(string (services.Localizer["OpenHistoryInBrowser"]))

                    OnClick(fun _ ->
                        task {
                            let parameters =
                                [ "titleText", string (services.Localizer["History"])
                                  "startTimeText", string (services.Localizer["Time"])
                                  "trackNameText", string (services.Localizer["Title"])
                                  "stationText", string (services.Localizer["Station"]) ]

                            let! result =
                                openHistoryAsync (
                                    history,
                                    parameters,
                                    services.LinkOpeningService,
                                    services.StationsService.Settings
                                )
                                |> Async.StartAsTask

                            match result with
                            | Error msg -> services.ToastService.ShowError msg
                            | Ok() -> ()
                        })

                    services.Localizer["OpenHistoryInBrowser"]
                }
        })
