[<AutoOpen>]
module RadioBrowser.HomePage

open System.Linq
open Microsoft.Extensions.Options
open Fun.Blazor

let loadCountries (store: IShareStore, listsService: IListsService) =
    async {
        match store.Countries.Value with
        | Loaded countries ->
            if not (countries.Any()) then
                store.Countries.Publish(Loading countries)
                let! result = listsService.GetCountries()

                match result with
                | Ok retVal -> store.Countries.Publish(Loaded retVal)
                | Error m -> store.Countries.Publish(Failed(countries, m))
        | _ -> ()
    }

let loadTasks (store: IShareStore, listsService: IListsService) =
    async {
        match store.Tags.Value with
        | Loaded tags ->
            if not (tags.Any()) then
                store.Tags.Publish(Loading tags)
                let! result = listsService.GetTags()

                match result with
                | Ok retVal -> store.Tags.Publish(Loaded retVal)
                | Error m -> store.Tags.Publish(Failed(tags, m))
        | _ -> ()
    }

let homePage =
    html.inject
        (fun (options: IOptions<AppSettings>, store: IShareStore, listsService: IListsService, hook: IComponentHook) ->
            hook.AddFirstAfterRenderTask(fun _ ->
                task {
                    do!
                        Async.Parallel [ loadCountries (store, listsService); loadTasks (store, listsService) ]
                        |> Async.Ignore
                })

            let countryCode = options.Value.CurrentRegion.TwoLetterISORegionName
            stationsByCountry countryCode)
