# syntax=docker/dockerfile:1

# The API, and beside it the migrator a release runs before starting it (docs/SPEC.md §9.5):
#
#   docker run … ghcr.io/<owner>/ritocode-api:<commit>                                  the API, on 8080
#   docker run … --entrypoint dotnet ghcr.io/<owner>/ritocode-api:<commit> migrator/Ritocode.DbMigrator.dll
#
# The migrator reads its own settings from /app/migrator wherever it is started.
#
# Both read `Database__ConnectionString` from the environment. Built by .github/workflows/release-images.yml.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

COPY global.json Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/ src/

# The migrator references the API, so the API's appsettings.json reaches its output too; the last
# line fails the build unless the migrator's own settings are the ones kept.
RUN dotnet publish src/Ritocode.Api --configuration Release --output /out/api \
 && dotnet publish src/Ritocode.DbMigrator --configuration Release --output /out/migrator \
 && grep -q MaxRetryCount /out/migrator/appsettings.json

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app

# Npgsql loads GSSAPI to offer encrypted authentication, and prints an error on every connection
# when the library is missing.
RUN apt-get update \
 && apt-get install --yes --no-install-recommends libgssapi-krb5-2 \
 && rm -rf /var/lib/apt/lists/*

COPY --from=build /out/api ./
COPY --from=build /out/migrator ./migrator/

# Where the production Compose file mounts the data-protection key ring. Owned by the app's user, so a
# new named volume, which takes the image's ownership, is writable by it.
RUN mkdir /keys && chown "$APP_UID" /keys

# The non-root user the .NET images provide; the port is theirs too (ASPNETCORE_HTTP_PORTS=8080).
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Ritocode.Api.dll"]
