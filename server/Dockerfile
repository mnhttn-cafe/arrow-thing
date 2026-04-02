FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore dependencies layer (cached unless .csproj files change)
COPY ArrowThing.sln .
COPY ArrowThing.Domain/ArrowThing.Domain.csproj ArrowThing.Domain/
COPY ArrowThing.Server/ArrowThing.Server.csproj ArrowThing.Server/
COPY ArrowThing.Server.Tests/ArrowThing.Server.Tests.csproj ArrowThing.Server.Tests/
RUN dotnet restore ArrowThing.Server/ArrowThing.Server.csproj

COPY ArrowThing.Domain/ ArrowThing.Domain/
COPY ArrowThing.Server/ ArrowThing.Server/
RUN dotnet publish ArrowThing.Server/ArrowThing.Server.csproj \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ArrowThing.Server.dll"]
