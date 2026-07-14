[<AutoOpen>]
module RadioBrowser.FavoritesDataAccess

open System
open System.Linq
open System.Threading.Tasks
open Microsoft.Extensions.Logging
open LiteDB

type FavoritesDataAccess(logger: ILogger<FavoritesDataAccess>) =
    let mapper = BsonMapper.Global

    do mapper.Entity<Station>().Id(fun x -> x.Id) |> ignore

    let getFavorites (db: LiteDatabase) =
        let retVal = db.GetCollection<Station> "favorites"
        retVal.EnsureIndex((fun x -> x.Id), true) |> ignore
        retVal

    interface IFavoritesDataAccess with
        member _.Add(station: Station) =
            use db = new LiteDatabase(AppSettings.ConnectionString, mapper)
            let favorites = getFavorites db

            if not (favorites.Exists(fun x -> x.Id = station.Id)) then
                station.IsFavorite <- true
                favorites.Insert station |> ignore

        member _.Update(stations: Station array) : Task<unit> =
            async {
                use db = new LiteDatabase(AppSettings.ConnectionString, mapper)
                let favorites = getFavorites db

                stations
                |> Array.iter (fun station ->
                    if favorites.Exists(fun x -> x.Id = station.Id) then
                        station.IsFavorite <- true

                        if not (favorites.Update station) then
                            logger.LogError(
                                "Failed to update station {0} with id {1} in favorites.",
                                station.Name,
                                station.Id
                            ))
            }
            |> Async.StartAsTask

        member _.Exists(id: Guid) =
            use db = new LiteDatabase(AppSettings.ConnectionString, mapper)
            getFavorites(db).Exists(fun x -> x.Id = id)

        member _.GetFavorites(name: string option, parameters: GetStationParameters) =
            try
                use db = new LiteDatabase(AppSettings.ConnectionString, mapper)

                let retVal =
                    match name with
                    | None -> getFavorites(db).FindAll().Skip(parameters.Offset).Take(parameters.Limit).ToArray()
                    | Some name when not (String.IsNullOrWhiteSpace name) ->
                        getFavorites(db)
                            .Find(
                                (fun x -> x.Name.ToLower().Contains(name.ToLower())),
                                parameters.Offset,
                                parameters.Limit
                            )
                            .ToArray()
                    | Some _ -> getFavorites(db).FindAll().Skip(parameters.Offset).Take(parameters.Limit).ToArray()

                Ok retVal
            with ex ->
                Error ex.Message

        member _.Remove(id: Guid) =
            use db = new LiteDatabase(AppSettings.ConnectionString, mapper)

            if not (getFavorites(db).Delete(BsonValue id)) then
                logger.LogError("Failed to remove station with id {0} from favorites.", id)

        member _.IsFavorites(ids: Guid array) =
            use db = new LiteDatabase(AppSettings.ConnectionString, mapper)
            let favorites = getFavorites db
            let exists (id: Guid, fvs: ILiteCollection<Station>) = fvs.Exists(fun x -> x.Id = id)
            ids |> Array.map (fun id -> id, exists (id, favorites)) |> Map.ofArray

        member _.FavoritesCount() : int =
            use db = new LiteDatabase(AppSettings.ConnectionString, mapper)
            getFavorites(db).Count()