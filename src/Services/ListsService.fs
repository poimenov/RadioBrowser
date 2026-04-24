[<AutoOpen>]
module RadioBrowser.ListsService

open Microsoft.Extensions.Options

type ListsService(handler: IHttpHandler, options: IOptions<AppSettings>) =
    let getNameAndCounts (listName: string) =
        async {
            let! result = handler.GetJsonStringAsync(listName, [])

            match result with
            | Error m -> return Error m
            | Ok jsonString -> return Ok(NameAndCountProvider.ParseList jsonString)
        }

    let getLimit =
        if options.Value.LimitTagsCount > 0 then
            options.Value.LimitTagsCount
        else
            300

    interface IListsService with
        member _.GetCodecs() = getNameAndCounts "codecs"
        member _.GetCountryCodes() = getNameAndCounts "countrycodes"

        member _.GetTags() =
            async {
                let parameters =
                    [ "limit", getLimit.ToString()
                      "offset", "0"
                      "order", "stationcount"
                      "reverse", "true"
                      "hidebroken", "true" ]

                let! result = handler.GetJsonStringAsync("tags", parameters)

                match result with
                | Error m -> return Error m
                | Ok jsonString -> return Ok(NameAndCountProvider.ParseList jsonString)
            }

        member _.GetCountries() =
            async {
                let! result = handler.GetJsonStringAsync("countries", [])

                match result with
                | Error m -> return Error m
                | Ok jsonString -> return Ok(CountriesProvider.ParseList jsonString)
            }

        member _.GetLanguages() =
            async {
                let! result = handler.GetJsonStringAsync("languages", [])

                match result with
                | Error m -> return Error m
                | Ok jsonString -> return Ok(LanguagesProvider.ParseList jsonString)
            }
