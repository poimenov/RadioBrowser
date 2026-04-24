[<AutoOpen>]
module RadioBrowser.HistoryDataAccess

open System
open System.Linq
open System.Threading.Tasks
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Options
open LiteDB


type HistoryDataAccess(options: IOptions<AppSettings>, logger: ILogger<HistoryDataAccess>) =
    let mapper = BsonMapper.Global

    do mapper.Entity<HistoryRecord>().Id(fun x -> x.StartTime) |> ignore

    let getHistory (db: LiteDatabase) =
        let retVal = db.GetCollection<HistoryRecord> "history"
        retVal.EnsureIndex((fun x -> x.StartTime), true) |> ignore
        retVal

    interface IHistoryDataAccess with
        member _.Add(record: HistoryRecord) : unit =
            try
                use db = new LiteDatabase(AppSettings.ConnectionString, mapper)
                let history = getHistory db

                if not (history.Exists(fun x -> x.StartTime = record.StartTime)) then
                    history.Insert record |> ignore

                let totalCount = history.Count()

                if totalCount > options.Value.HistoryTruncateCount then
                    let oldestRecord =
                        history
                            .Query()
                            .OrderByDescending(fun x -> x.StartTime)
                            .Offset(options.Value.HistoryTruncateCount - 1)
                            .Limit(1)
                            .ToList()
                        |> Seq.tryHead

                    match oldestRecord with
                    | Some record -> history.DeleteMany(fun x -> x.StartTime < record.StartTime) |> ignore
                    | None -> ()
            with ex ->
                logger.LogError(ex, "Error while adding history record")


        member _.GetHistory() : Result<HistoryRecord list, string> =
            try
                use db = new LiteDatabase(AppSettings.ConnectionString, mapper)
                let retVal = getHistory(db).FindAll().OrderBy(fun x -> x.StartTime) |> Seq.toList
                Ok retVal
            with ex ->
                logger.LogError(ex, "Error while getting history")
                Error ex.Message