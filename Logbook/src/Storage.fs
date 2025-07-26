module Logbook.Storage

open System
open System.IO
open System.Data
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

let populateSampleEntries storage sampleDataFile =
    use tran = storage.Connection.BeginTransaction()
    let count = 
        tran
        |> Db.newCommandForTransaction "SELECT COUNT(id) FROM entries"
        |> Db.querySingle (fun read -> read.GetInt32(0))
        |> Option.get
    if count = 0 then
        printfn "Populate sample data"

    tran.Commit()
