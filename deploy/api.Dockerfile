# syntax=docker/dockerfile:1

# The API, and beside it the migrator a release runs before starting it (docs/SPEC.md §9.5):
#
#   docker run … ghcr.io/<owner>/ritocode-api:<commit>                                  the API, on 8080
#   docker run … --entrypoint dotnet ghcr.io/<owner>/ritocode-api:<commit> migrator/Ritocode.DbMigrator.dll
#
# Both read `Database__ConnectionString` from the environment. Built by .github/workflows/release-images.yml.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

COPY global.json Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/ src/

RUN dotnet publish src/Ritocode.Api --configuration Release --output /out/api \
 && dotnet publish src/Ritocode.DbMigrator --configuration Release --output /out/migrator

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app

COPY --from=build /out/api ./
COPY --from=build /out/migrator ./migrator/

# The non-root user the .NET images provide; the port is theirs too (ASPNETCORE_HTTP_PORTS=8080).
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Ritocode.Api.dll"]
