[<AutoOpen>]
module RadioBrowser.StationsList

open System.Collections.Generic
open System.Linq
open Microsoft.Extensions.Localization
open Microsoft.FluentUI.AspNetCore.Components
open Fun.Blazor

let stationsList (store: IShareStore, localizer: IStringLocalizer<SharedResources>) =
    div {
        style' "padding:10px;"

        adapt {
            let! stationsState = store.Stations
            let! selectedStation = store.SelectedStation
            let! selectedStationIsFavorite = store.SelectedStationIsFavorite

            let isCurrent (curr: Station) =
                match selectedStation with
                | NotSelected -> false
                | Selected item -> curr.Id = item.Id

            let getSelectedClass (curr: Station) =
                if isCurrent curr then
                    "station selected-station"
                else
                    "station"

            let isFavorite (curr: Station) =
                match selectedStation with
                | NotSelected -> curr.IsFavorite
                | Selected item ->
                    if curr.Id = item.Id then
                        selectedStationIsFavorite
                    else
                        curr.IsFavorite

            let renderStationsList (stations: IEnumerable<Station>) =
                fragment {
                    div {
                        class' "stations-list"

                        for station in stations.ToArray() do
                            div {
                                class' (getSelectedClass station)

                                onclick (fun _ ->
                                    let selected = Selected station
                                    store.SelectedStationIsFavorite.Publish station.IsFavorite
                                    store.SelectedStation.Publish selected)

                                stationItem (station, isFavorite station)
                            }
                    }

                    watchElementVisibleComponent
                }

            match stationsState with
            | Loading stations ->
                if stations.Any() then
                    renderStationsList stations
                else
                    loadingState localizer
            | Failed(stations, m) ->
                if stations.Any() then
                    renderStationsList stations
                else
                    div {
                        style' "text-align:center;color:red;"
                        string (localizer["Fail"]) + ": " + m
                    }
            | Loaded stations ->
                if stations.Any() then
                    renderStationsList stations
                else
                    div {
                        style' "text-align:center;"

                        localizer["StationsNotFound"]
                    }
            | EndOfList stations -> renderStationsList stations
        }
    }