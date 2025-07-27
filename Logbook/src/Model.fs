namespace Logbook.Model

open System
open System.Data
open System.Text.Json.Serialization
open System.Globalization
open Donald

type Entry = {
    [<JsonPropertyName("id")>]
    Id : int
    [<JsonPropertyName("title")>]
    Title : string
    [<JsonPropertyName("body")>]
    Body : string
    [<JsonPropertyName("isoTime")>]
    IsoTime : DateTime
    [<JsonPropertyName("lat")>]
    Latitude : float option
    [<JsonPropertyName("lon")>]
    Longitude : float option
}

module Entry =
    let dateTimeToString (dt : DateTime) =
        dt.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)

    let stringToDateTime (str : string) =
        DateTime.ParseExact(str, "yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)
            .ToUniversalTime()

    let ofDataReader (read : IDataReader) =
        {
            Id = read.ReadInt32("id")
            Title = read.ReadString("title")
            Body = read.ReadString("body")
            IsoTime = read.ReadString("isoTime") |> stringToDateTime
            Latitude = read.ReadDoubleOption("lat")
            Longitude = read.ReadDoubleOption("lon")
        }

type ErrorMessage = {
    [<JsonPropertyName("message")>]
    Message : string
    [<JsonPropertyName("errors")>]
    [<JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)>]
    Errors : string list
}
