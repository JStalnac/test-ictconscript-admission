module Logbook.Entries

open System
open System.Text.Json
open System.Text.Json.Serialization
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Logging
open Microsoft.AspNetCore.Http
open Falco
open Donald
open Logbook.Model
open Logbook.Validation
open Logbook.Storage

module All =
    let get (ctx : HttpContext)=
        use storage =
            ctx.RequestServices.GetRequiredService<StorageContext>()

        let entries =
            storage.Connection
            |> Db.newCommand "SELECT * FROM entries ORDER BY isoTime DESC"
            |> Db.query Entry.ofDataReader

        Response.ofJson entries ctx

module Create =
    type Model = {
        [<JsonPropertyName("title")>]
        Title : string
        [<JsonPropertyName("body")>]
        Body : string
        [<JsonPropertyName("lat")>]
        Latitude : float option
        [<JsonPropertyName("lon")>]
        Longitude : float option
    }

    let validateModel title body lat lon =
        let validateTitle =
            title 
            |> ValidationResult.mapResult (fun title ->
                if String.length title <= 120 then
                    Valid
                else
                    Invalid [ "the length of 'title' must be less than or equal to 120 characters" ])
                id
        let validateBody =
            body
            |> ValidationResult.mapResult (fun _ -> Valid) id
        let validateLocation =
            let validateCoord err max x =
                match x with
                | x when Double.IsFinite(x) && x <= abs max -> Valid
                | _ -> Invalid [ err ]
            let validateLat = validateCoord "invalid 'lat'" 90.0
            let validateLon = validateCoord "invalid 'lon'" 180.0
            match lat, lon with
            | Ok None, Ok None -> Valid
            | Ok (Some lat), Ok (Some lon) ->
                validateLat lat
                <&> validateLon lon
            | Ok (Some lat), Ok None ->
                validateLat lat
                <&> Invalid [ "missing 'lon'" ]
            | Ok None, Ok (Some lon) ->
                Invalid [ "missing 'lat'" ]
                <&> validateLon lon
            | Error err1, Error err2 ->
                Invalid [ err1; err2 ]
            | Ok _, Error err ->
                Invalid [ err ]
            | Error err, Ok _ -> 
                Invalid [ err ]

        let valid =
            validateTitle
            <&> validateBody
            <&> validateLocation

        let unwrap res =
            match res with
            | Ok x -> x
            | Error err -> failwithf "Attempted to unwrap Error value (%A)" err

        match valid with
        | Valid ->
            Ok {
                Title = title |> unwrap
                Body = body |> unwrap
                Latitude = lat |> unwrap
                Longitude = lon |> unwrap
            }
        | Invalid errors -> Error errors

    let post (ctx : HttpContext) =
        let logger = getLogger ctx "Logbook.Entries.Create"

        ctx
        |> Request.validateJson
            (fun (body : JsonDocument) ->
                use body = body
                let root = body.RootElement
                let title = 
                    match root.TryGetProperty("title") with
                    | true, prop -> Ok (prop.GetString())
                    | false, _ -> Error "missing 'title'"
                let body = 
                    match root.TryGetProperty("body") with
                    | true, prop -> Ok (prop.GetString())
                    | false, _ -> Error "missing 'body'"
                let lat = 
                    match root.TryGetProperty("lat") with
                    | true, prop ->
                        let mutable lat = 0.0
                        match prop.ValueKind with
                        | JsonValueKind.Number when prop.TryGetDouble(&lat) ->
                            Ok (Some lat)
                        | _ -> Error "'lat' must be float"
                    | false, _ ->
                        Ok None
                let lon = 
                    match root.TryGetProperty("lon") with
                    | true, prop ->
                        let mutable lon = 0.0
                        match prop.ValueKind with
                        | JsonValueKind.Number when prop.TryGetDouble(&lon) ->
                            Ok (Some lon)
                        | _ -> Error "'lon' must be float"
                    | false, _ ->
                        Ok None
                validateModel title body lat lon)
            (fun res ->
                match res with
                | Ok model ->
                    logger.LogInformation("Request body: {Body}", sprintf "%A" model)
                    fun ctx ->
                        use storage =
                            ctx.RequestServices.GetRequiredService<StorageContext>()
                        let time = DateTime.UtcNow
                        let id = 
                            storage.Connection
                            |> Db.newCommand (
                                "INSERT INTO entries(title, body, isotime, lat, lon) " +
                                "VALUES (@title, @body, @isotime, @lat, @lon) " +
                                "RETURNING id"
                            ) |> Db.setParams [
                                    "title", sqlString model.Title
                                    "body", sqlString model.Body
                                    "isotime",
                                        time
                                        |> Entry.dateTimeToString
                                        |> sqlString
                                    "lat", sqlDoubleOrNull model.Latitude
                                    "lon", sqlDoubleOrNull model.Longitude
                                ]
                            |> Db.querySingle (fun read -> read.ReadInt32("id"))
                            |> Option.get

                        let entry = {
                            Id = id
                            Title = model.Title
                            Body = model.Body
                            IsoTime =
                                // Round the time to the precision we need
                                time
                                |> Entry.dateTimeToString
                                |> Entry.stringToDateTime
                            Latitude = model.Latitude
                            Longitude = model.Longitude
                        }
                        ctx |> Response.ofJson entry
                | Error errors ->
                    Response.withStatusCode StatusCodes.Status400BadRequest
                    >> Response.ofJson (errorMessageWithErrors "Invalid request" errors))

module Single =
    let get (ctx : HttpContext) =
        let route = Request.getRoute ctx
        let id = route.GetInt32("id")

        let logger = getLogger ctx "Entries.Single.get"
        logger.LogInformation("Searching for entry {Id}", id)

        use storage = 
            ctx.RequestServices.GetRequiredService<StorageContext>()

        let entry =
            storage.Connection
            |> Db.newCommand "SELECT * FROM entries WHERE id = @id"
            |> Db.setParams [
                    "id", sqlInt32 id
                ]
            |> Db.querySingle Entry.ofDataReader

        match entry with
        | Some entry ->
            Response.ofJson entry ctx
        | None ->
            (Response.withStatusCode StatusCodes.Status404NotFound
            >> Response.ofJson (errorMessage "Entry not found"))
                ctx
