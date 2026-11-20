namespace RadioBrowser.Tests.Unit.Services

open Microsoft.Extensions.Options
open Moq
open Xunit
open FsUnit.Xunit
open RadioBrowser
open RadioBrowser.Interfaces
open RadioBrowser.StationsService

module StationsServiceTests =
    [<Fact>]
    let ``SearchStations always sorts by votes descending`` () =
        let mutable requestParameters: (string * string) list = []

        let handler =
            { new IHttpHandler with
                member _.GetJsonStringAsync(_, parameters) =
                    requestParameters <- parameters
                    async.Return(Error "captured request") }

        let settings = AppSettings()
        settings.DefaultOrder <- "name"
        settings.ReverseOrder <- false

        let service =
            StationsService(handler, Mock<IFavoritesDataAccess>().Object, Options.Create settings)
            :> IStationsService

        service.SearchStations(
            SearchStationParameters(None, None, Some "US", Some "rock", None),
            GetStationParameters(0, 20, true)
        )
        |> Async.RunSynchronously
        |> ignore

        let parameters = Map.ofList requestParameters
        parameters.["order"] |> should equal "votes"
        parameters.["reverse"] |> should equal "true"
