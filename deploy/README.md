# Deploy

What runs in production and how it is built ([SPEC.md](../docs/SPEC.md) §9.5). The production
Compose file and the release command arrive with
[#135](https://github.com/shoraLBRT/ritocode/issues/135) and
[#136](https://github.com/shoraLBRT/ritocode/issues/136).

## Images

Built by `.github/workflows/release-images.yml` on every push to `main`, tagged with the full commit
and `main`, public on GitHub Container Registry ([ADR 0011](../docs/adr/0011-release-images.md)).

| Image | Dockerfile | Runs |
| --- | --- | --- |
| `ghcr.io/shoralbrt/ritocode-api` | `api.Dockerfile` | the API on port 8080, as a non-root user; the migrator with `--entrypoint dotnet … migrator/Ritocode.DbMigrator.dll` |
| `ghcr.io/shoralbrt/ritocode-web` | `web.Dockerfile` | the static frontend, `/` and `/problems` prerendered, on port 80 (`web.Caddyfile`) |

Both read their configuration from the environment: `Database__ConnectionString` for the API and the
migrator. The web image takes the site's address at build time from the repository variable
`SITE_ORIGIN`.

## Logs

The API writes **one JSON object per line** to standard output (the console's `json` formatter,
`src/Ritocode.Api/appsettings.json`), in UTC:

- every request has one summary line — method, path, status, duration — from
  `Microsoft.AspNetCore.HttpLogging`;
- every line of a request carries its **request id** in `Scopes` — the `X-Request-Id` the response
  and every error body carry — and, for a signed-in caller, the **user id**. Nothing else about the
  person is logged: no e-mail, no name, no headers, no query, no body;
- levels come from `Logging:LogLevel`, overridable by environment variable, such as
  `Logging__LogLevel__Default=Warning`.

To find a request's lines by the id a user reports:

```bash
docker compose logs --no-log-prefix api | jq -c 'select(any(.Scopes[]?; .RequestId == "<id>"))'
```

ASP.NET adds a scope of its own that also has a `RequestId` key: the server's connection trace id.
It has a different format and never equals a client's id, so the query above finds exactly the
request.

**Rotation is Docker's.** The production Compose file (#135) gives the API, and every other
service, the `json-file` driver with a cap, so the disk never fills:

```yaml
logging:
  driver: json-file
  options:
    max-size: "10m"
    max-file: "5"
```
