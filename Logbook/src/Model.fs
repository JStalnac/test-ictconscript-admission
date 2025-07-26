namespace Logbook.Model

open System
open System.Data
open System.Globalization
open Donald

type Entry = {
    Id : int
    Title : string
    Body : string
    IsoTime : DateTime
    Latitude : float option
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
    Message : string
    Errors : string list
}
