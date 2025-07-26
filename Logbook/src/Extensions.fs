namespace Logbook

open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Logging
open Microsoft.Extensions.DependencyInjection
open Logbook.Model

[<AutoOpen>]
module Extensions =
    let getLogger (ctx : HttpContext) category =
        let loggerFactory = ctx.RequestServices.GetRequiredService<ILoggerFactory>()
        loggerFactory.CreateLogger category

    let errorMessage msg =
        { Message = msg; Errors = Unchecked.defaultof<_> }

    let errorMessageWithErrors msg errors =
        { Message = msg; Errors = errors }

    module Request =
        open System.Text.Json
        open Falco

        let validateJson (map : 'a -> 'b) (next : 'b -> HttpHandler) : HttpHandler =
            fun ctx ->
                task {
                    try
                        let! json = Request.getJson ctx
                        let body = map json
                        return! next body ctx
                    with
                        | :? JsonException as ex ->
                            let logger = getLogger ctx "Logbook"
                            logger.LogDebug("Invalid JSON in request: {Reason}", ex.Message)
                            return!
                                Response.withStatusCode StatusCodes.Status400BadRequest
                                >> Response.ofJson (errorMessage "Invalid JSON")
                                <| ctx
                }
