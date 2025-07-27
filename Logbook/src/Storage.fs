module Logbook.Storage

open System
open System.IO
open System.Data
open System.Text.Json
open Microsoft.Extensions.Logging
open Donald

type StorageContext =
    { Connection : IDbConnection }
    interface IDisposable with
        member x.Dispose() =
            x.Connection.Dispose()

let initDb storage =
    storage.Connection
    |> Db.newCommand
        """
CREATE TABLE IF NOT EXISTS entries(
    id INTEGER PRIMARY KEY,
    title TEXT NOT NULL,
    body TEXT NOT NULL,
    isotime TEXT NOT NULL,
    lat REAL,
    lon REAL,
    CHECK (length(title) <= 120),
    CHECK (-90 <= lat <= 90),
    CHECK(-180 <= lon <= 180)
)
        """
    |> Db.exec

let populateSampleEntries storage (logger : ILogger) sampleDataFile =
    use tran = storage.Connection.BeginTransaction()

    let count = 
        tran
        |> Db.newCommandForTransaction "SELECT COUNT(id) FROM entries"
        |> Db.querySingle (fun read -> read.GetInt32(0))
        |> Option.get

    if count = 0 then
        logger.LogInformation("Populating database with sample data")
        File.ReadAllText(sampleDataFile)
        |> JsonSerializer.Deserialize
        |> Array.sortBy (fun (doc : JsonDocument) ->
            doc.RootElement.GetProperty("id").GetString() |> int)
        |> Array.iter (fun doc ->
            use doc = doc
            let getProp name =
                doc.RootElement.GetProperty(name : string)
            let id = 
                tran
                |> Db.newCommandForTransaction (
                    "INSERT INTO entries(id, title, body, isoTime, lat, lon) " + 
                    "VALUES(@id, @title, @body, @isoTime, @lat, @lon) RETURNING id"
                ) |> Db.setParams [
                    "id", (getProp "id").GetString() |> sqlInt32 
                    "title", (getProp "title").GetString() |> sqlString
                    "body", (getProp "body").GetString() |> sqlString
                    "isoTime", (getProp "isoTime").GetString() |> sqlString
                    "lat",
                        match getProp "lat" with
                        | d when d.ValueKind = JsonValueKind.Number ->
                            sqlDouble (d.GetDouble())
                        | _ -> SqlType.Null
                    "lon",
                        match getProp "lon" with
                        | d when d.ValueKind = JsonValueKind.Number ->
                            sqlDouble (d.GetDouble())
                        | _ -> SqlType.Null
                ]
                |> Db.querySingle (fun read -> read.ReadInt32("id"))
                |> Option.get
            if string id <> (getProp "id").GetString() then
                failwith "Inserted ID does not match ID in samples")

    tran.Commit()
