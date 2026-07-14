[<AutoOpen>]
module RadioBrowser.StationsService

open System
open Microsoft.Extensions.Options

type StationsService(handler: IHttpHandler, dataAccess: IFavoritesDataAccess, options: IOptions<AppSettings>) =
    let stations = "stations"

    let getQuery (parameters: GetStationParameters) =
        [ "offset", string parameters.Offset
          "hidebroken", string parameters.Hidebroken ]

    let getSearchQuery (searchParameters: SearchStationParameters, parameters: GetStationParameters) =
        [ if searchParameters.Name.IsSome then
              "name", searchParameters.Name.Value
          if searchParameters.NameExact.IsSome then
              "nameExact", string searchParameters.NameExact.Value
          if searchParameters.CountryCode.IsSome then
              "countrycode", searchParameters.CountryCode.Value
          if searchParameters.Tag.IsSome then
              "tag", searchParameters.Tag.Value
          if searchParameters.tagExact.IsSome then
              "tagExact", string searchParameters.tagExact.Value
          if not (String.IsNullOrWhiteSpace options.Value.Codec) then
              "codec", options.Value.Codec
          if not (String.IsNullOrWhiteSpace options.Value.Language) then
              "language", options.Value.Language
          "offset", string parameters.Offset
          "limit", string parameters.Limit
          "order", options.Value.DefaultOrder
          "reverse", string options.Value.ReverseOrder
          "hidebroken", string parameters.Hidebroken ]

    let getStations (result: Result<string, string>) =
        match result with
        | Error m -> Error m
        | Ok json ->
            let response = StationsProvider.ParseList json

            let favorites =
                response |> Array.map (fun x -> x.Stationuuid) |> dataAccess.IsFavorites

            let isFavorite (id: Guid) =
                match favorites.ContainsKey id with
                | true -> favorites.[id]
                | false -> false

            response
            |> Array.map (fun x -> StationMapper.toStation (x, isFavorite x.Stationuuid))
            |> Ok

    interface IStationsService with
        member _.GetStationsByClicks parameters =
            async {
                let! jsonString =
                    handler.GetJsonStringAsync($"{stations}/topclick/{parameters.Limit}", getQuery parameters)

                return getStations jsonString
            }

        member _.GetStationsByVotes parameters =
            async {
                let! jsonString =
                    handler.GetJsonStringAsync($"{stations}/topvote/{parameters.Limit}", getQuery parameters)

                return getStations jsonString
            }

        member _.SearchStations(searchParameters, parameters) =
            async {
                let! jsonString =
                    handler.GetJsonStringAsync($"{stations}/search", getSearchQuery (searchParameters, parameters))

                return getStations jsonString
            }

        member _.Settings: AppSettings = options.Value

        member _.GetFavoriteStations(name: string option, parameters: GetStationParameters) =
            async { return dataAccess.GetFavorites(name, parameters) }

        member _.GetStations(uuids: Guid array) : Async<Result<Station array, string>> =
            async {
                let query = [ "uuids", String.Join(",", uuids) ]

                let! jsonString = handler.GetJsonStringAsync($"{stations}/byuuid", query)

                return getStations jsonString
            }

        member _.FavoritesDataAccess: IFavoritesDataAccess = dataAccess

        member _.ClickStation(uuid: Guid) =
            handler.GetJsonStringAsync($"url/{uuid}", []) |> Async.Ignore |> Async.Start

        member _.VoteStation(uuid: Guid) =
            handler.GetJsonStringAsync($"vote/{uuid}", []) |> Async.Ignore |> Async.Start
