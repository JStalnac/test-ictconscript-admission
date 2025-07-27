FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build
WORKDIR /app

COPY . ./
RUN dotnet restore
RUN dotnet publish Logbook/src/Logbook.fsproj -o out -c Release

FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine
WORKDIR /app
COPY --from=build /app/out .
COPY sample-data/data.json ./sample-data/data.json
ENTRYPOINT ["dotnet", "Logbook.dll"]
