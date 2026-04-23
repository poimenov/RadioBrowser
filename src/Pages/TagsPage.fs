[<AutoOpen>]
module RadioBrowser.TagsPage

open System
open Microsoft.Extensions.Localization
open Microsoft.FluentUI.AspNetCore.Components
open FSharp.Data.Adaptive
open Fun.Blazor

let tagsPage =
    html.inject
        (fun
            (store: IShareStore,
             listsService: IListsService,
             localizer: IStringLocalizer<SharedResources>,
             hook: IComponentHook) ->
            hook.AddFirstAfterRenderTask(fun _ ->
                task {
                    store.HeaderTitle.Publish localizer["Tags"]

                    do!
                        Async.Parallel [ loadCountries (store, listsService); loadTasks (store, listsService) ]
                        |> Async.Ignore
                })

            fragment {
                adapt {
                    let! tagsState, setTags = store.Tags.WithSetter()
                    let! searchString, setSearchString = cval("").WithSetter()

                    match tagsState with
                    | Loading _ -> loadingState localizer
                    | Failed(tags, m) ->
                        div {
                            style' "text-align:center;color:red;"
                            string (localizer["Fail"]) + ": " + m
                        }
                    | EndOfList tags
                    | Loaded tags ->
                        let arrangeTags (arr: NameAndCountProvider.NameAndCount[]) =
                            let ind = arr |> Array.mapi (fun i t -> (i, t))

                            let odds =
                                ind
                                |> Array.filter (fun (i, _) -> i % 2 = 1)
                                |> Array.map (fun (_, t) -> t)
                                |> Array.rev

                            let ev =
                                ind |> Array.filter (fun (i, _) -> i % 2 = 0) |> Array.map (fun (_, t) -> t)

                            Array.append odds ev

                        let filteredTags =
                            if String.IsNullOrWhiteSpace searchString then
                                arrangeTags tags
                            else
                                tags
                                |> Array.filter (fun t -> t.Name.ToLower().Contains(searchString.ToLower()))
                                |> arrangeTags

                        let getColor () =
                            let r = Random().Next(150, 255)
                            let g = Random().Next(150, 255)
                            let b = Random().Next(150, 255)
                            r, g, b

                        let invertColor (rgb: int * int * int) =
                            rgb |> fun (r, g, b) -> 255 - r, 255 - g, 255 - b

                        let colorToRGBString (rgb: int * int * int) =
                            rgb |> fun (r, g, b) -> $"rgb({r},{g},{b})"


                        let calculateFontSize (minCount: int) (maxCount: int) (count: int) =
                            let minSize = 10.0
                            let maxSize = 64.0

                            minSize
                            + (maxSize - minSize) * float (count - minCount) / float (maxCount - minCount)

                        if tags.Length = 0 then
                            div {
                                style' "text-align:center;"
                                localizer["NoTagsFound"]
                            }
                        else
                            div {
                                style' "margin-bottom:10px;"

                                FluentSearch'' {
                                    style' "width:330px;"
                                    Placeholder(string (localizer["TagName"]))
                                    Immediate true
                                    Value searchString
                                    ValueChanged(fun s -> setSearchString s)
                                }
                            }

                            if filteredTags.Length = 0 then
                                div {
                                    style' "text-align:center;"
                                    localizer["NoTagsFound"]
                                }
                            else
                                let minCount = filteredTags |> Array.map (fun x -> x.Stationcount) |> Array.min
                                let maxCount = filteredTags |> Array.map (fun x -> x.Stationcount) |> Array.max

                                div {
                                    class' "tags-list"

                                    for tag in filteredTags do
                                        let rColor = getColor ()
                                        let invertedColor = invertColor rColor
                                        let rSize = calculateFontSize minCount maxCount tag.Stationcount
                                        let fSize = Math.Round rSize |> int
                                        let heightSize = rSize + 2.0 * Math.Round(rSize / 5.0) |> int

                                        a {
                                            class' "tag-item"

                                            title'
                                                $"""{tag.Name.ToUpper()} ({localizer["StationsCount"]}: {tag.Stationcount})"""

                                            style'
                                                $"font-size:{fSize}px;color:{colorToRGBString invertedColor};background-color:{colorToRGBString rColor};"

                                            href $"/stationsByTag/{tag.Name}"

                                            span {
                                                style' $"height:{heightSize}px;"
                                                tag.Name.ToUpper()
                                            }
                                        }
                                }
                }
            })
