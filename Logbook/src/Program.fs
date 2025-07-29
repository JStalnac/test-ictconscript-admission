open System.Data
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.Logging
open Microsoft.Extensions.DependencyInjection
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.Data.Sqlite
open Falco
open Falco.Routing
open Falco.OpenApi
open Logbook
open Logbook.Model
open Logbook.Storage

let routes = [
        get "/entries" Entries.Read.getAll
            |> OpenApi.name "GetLogbookEntries"
            |> OpenApi.summary "Get all logbook entries"
        post "/entries" Entries.Create.post
            |> OpenApi.name "CreateLogbookEntry"
            |> OpenApi.summary "Create a new logbook entry"
            |> OpenApi.acceptsType typeof<Entries.Create.Model>
            |> OpenApi.returnType typeof<Entry>
        get "/entries/{id:int}" Entries.Read.get
            |> OpenApi.name "GetLogbookEntry"
            |> OpenApi.summary "Get a logbook entry"
            |> OpenApi.route [
                { Type = typeof<int>; Name = "id"; Required = true }
            ]
        get "/health"
            (Response.withStatusCode StatusCodes.Status200OK
            >> Response.ofPlainText "OK")
            |> OpenApi.name "HealthCheck" 
            |> OpenApi.summary "Service health check"
    ]

let configureServices (services : IServiceCollection) =
    services
        .AddScoped<IDbConnection, SqliteConnection>(fun s ->
            let connectionString =
                let config = s.GetRequiredService<IConfiguration>()
                config.GetConnectionString("Sqlite")
            new SqliteConnection(connectionString)
        )
        .AddScoped<StorageContext>(fun s ->
            let conn = s.GetRequiredService<IDbConnection>()
            conn.Open()
            { Connection = conn }
        )
        .AddOpenApi()
        .AddFalcoOpenApi()
        |> ignore


[<EntryPoint>]
let main args =
    let builder = WebApplication.CreateBuilder(args)
    builder.WebHost.UseUrls("http://*:8000") |> ignore
    configureServices builder.Services

    let app = builder.Build()

    using (app.Services.CreateScope()) (fun scope ->
        use storage =
            scope.ServiceProvider.GetRequiredService<StorageContext>()
        initDb storage
        let loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
        let logger = loggerFactory.CreateLogger("Logbook")
        let sampleData =
            scope.ServiceProvider.GetRequiredService<IConfiguration>()
                .GetValue("SampleData")
        populateSampleEntries storage logger sampleData
    )

    app.MapOpenApi() |> ignore

    app.UseRouting()
        .UseExceptionHandler(fun exceptionHandler ->
            exceptionHandler.Run(
                Response.withStatusCode 500
                >> Response.ofJson (errorMessage "Internal server error"))
        )
        .UseSwaggerUI(fun opts ->
            opts.SwaggerEndpoint("/openapi/v1.json", "v1")
        )
        .UseFalco(routes)
        |> ignore

    app.Run(
        Response.withStatusCode StatusCodes.Status404NotFound
        >> Response.ofJson (errorMessage "Not found"))

    0
