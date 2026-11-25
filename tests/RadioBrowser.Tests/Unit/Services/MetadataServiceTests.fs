namespace RadioBrowser.Tests.Unit.Services

open Xunit
open FsUnit.Xunit
open RadioBrowser

module MetadataServiceTests =

    [<Theory>]
    [<InlineData("StreamTitle='Artist - Title';", "Artist - Title")>]
    [<InlineData("StreamTitle='Artist - Title-';", "Artist - Title")>]
    [<InlineData("StreamTitle='Some Title text=\"Other\"';", "Some Title - Other")>]
    [<InlineData("StreamTitle='just title';", "just title")>]
    [<InlineData("Invalid format", "NONE")>]
    [<InlineData("StreamTitle='';", null)>]
    [<InlineData("StreamTitle='   ';", null)>]
    let ``ExtractStreamTitle parses various formats correctly`` (input: string, expected: string) =
        let result = MetadataParser.ExtractStreamTitle(input)

        let expectedResult: Result<string option, string> =
            match expected with
            | "NONE" -> Ok None
            | null -> Ok None
            | _ -> Ok(Some expected)

        Assert.Equal(expectedResult, result)
