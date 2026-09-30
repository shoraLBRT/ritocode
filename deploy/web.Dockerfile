# syntax=docker/dockerfile:1

# The frontend: the bundle with `/` and `/problems` prerendered from content/ (docs/SPEC.md §4.1),
# served by Caddy on port 80 with the rule the prerender needs (deploy/web.Caddyfile). TLS and the
# route to the API are the edge's, in the production Compose file of #135.
#
# Build arguments are read at build time and inlined, as `npm run build` always does:
#   SITE_ORIGIN        the site's address, for the sitemap and canonical links — required
#   VITE_API_BASE_URL  where the browser finds the API; same origin by default
#   VITE_DEMO_TASK     the landing page's demo task; the frontend's default when empty

# The content export is made by the backend's own parser (src/Ritocode.ContentTool).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS content
WORKDIR /source

COPY global.json Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/ src/
COPY content/ content/

RUN dotnet run --project src/Ritocode.ContentTool --configuration Release -- export /out/content-export.json content

FROM node:22-alpine AS build
WORKDIR /frontend

COPY frontend/package.json frontend/package-lock.json ./
RUN npm ci

COPY frontend/ ./
COPY --from=content /out/content-export.json /content-export.json

ARG SITE_ORIGIN
ARG VITE_API_BASE_URL=/api/v1
ARG VITE_DEMO_TASK=
ENV CONTENT_EXPORT=/content-export.json \
    SITE_ORIGIN=${SITE_ORIGIN} \
    VITE_API_BASE_URL=${VITE_API_BASE_URL} \
    VITE_DEMO_TASK=${VITE_DEMO_TASK}

RUN npm run build:static

FROM caddy:2-alpine

COPY deploy/web.Caddyfile /etc/caddy/Caddyfile
COPY --from=build /frontend/dist /srv

EXPOSE 80
