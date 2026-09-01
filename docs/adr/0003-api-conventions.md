# 0003 — API conventions

- Status: Accepted
- Date: 2026-09-01
- Relates to: [#2](https://github.com/shoraLBRT/ritocode/issues/2)

## Context

The frontend and backend are built in parallel by different sessions. Without conventions fixed up
front, each module invents its own error shape and pagination, and the API client ends up with a
special case per endpoint.

## Decision

### Routing and versioning

All module endpoints live under a configured base path, `Api:BasePath`, default `/api/v1`. The
version is a path segment; a breaking change means `/api/v2`, not a header. Health probes
(`/health/live`, `/health/ready`) sit outside the versioned prefix because they serve
infrastructure, not clients.

Paths are lowercase kebab-case plural nouns: `/api/v1/problems`, `/api/v1/workspaces/{id}/files`.

### Payloads

JSON, camelCase, UTF-8. Timestamps are ISO 8601 in UTC with an explicit `Z`. Ids are opaque strings
to clients — never assume a format.

### Status codes

Domain code returns or throws an `AppError` carrying an `ErrorType`; the HTTP layer is the only
place that knows status codes. The mapping lives in `ErrorStatusCodeMap` and is covered by tests:

| `ErrorType` | Status |
| --- | --- |
| `Validation` | 400 |
| `Unauthenticated` | 401 |
| `Forbidden` | 403 |
| `NotFound` | 404 |
| `Conflict` | 409 |
| `PreconditionFailed` | 412 |
| `RateLimited` | 429 |
| `Unavailable` | 503 |
| `Unexpected` | 500 |

A resource the caller may not see returns 404, not 403, so the API does not confirm its existence.

### Error body

Every non-2xx response is RFC 9457 `application/problem+json` with two extensions:

```json
{
  "type": "https://ritocode.dev/errors/workspace_not_found",
  "title": "Not found",
  "status": 404,
  "detail": "Workspace does not exist.",
  "instance": "/api/v1/workspaces/42",
  "code": "workspace_not_found",
  "requestId": "9f2c1b7e4a0d4f2e8c3b5a6d7e8f9a0b"
}
```

- `code` is the stable, machine-readable identifier. Clients branch on it. `title` and `detail` are
  human-facing and may change without notice.
- `requestId` matches the `X-Request-Id` response header, so a user-reported failure maps to logs.
- Validation failures add `errors`: a map from camelCase field path to messages.

Unexpected failures always return `code: "internal_error"` with a generic `detail`. The real cause
goes to the log line carrying the same request id.

### Correlation

`RequestIdMiddleware` runs first. It honours an inbound `X-Request-Id` when it is at most 128
characters of `[A-Za-z0-9._:-]`, and otherwise generates one — a client-supplied value is echoed
into headers and logs, so it is not trusted verbatim. The id is echoed on every response, including
error responses re-executed by the exception handler.

### Pagination

Collection endpoints accept `?page=` (1-based, default 1) and `?pageSize=` (default 20, max 100).
Out-of-range values are rejected with 400 rather than clamped, so a client asking for 1000 rows
learns it cannot have them instead of silently receiving 100.

Responses use the envelope, never a bare array:

```json
{
  "items": [],
  "pageNumber": 1,
  "pageSize": 20,
  "totalItems": 0,
  "totalPages": 0,
  "hasNextPage": false,
  "hasPreviousPage": false
}
```

Offset pagination is chosen over cursors because catalog browsing needs addressable page numbers.
Endpoints over high-churn append-only data (submission history at scale) may need cursors later;
that will be a new ADR, not a silent change to this one.

### Validation

Request bodies are validated by `ValidationEndpointFilter<T>`, attached with
`.WithValidation<TRequest>()`. The filter runs the `IValidator<T>` registered by the owning module,
so handlers only ever see valid input, and every validation failure produces the same 400 body.
FluentValidation property names are converted to camelCase to match the JSON the client sent.

## Consequences

- Adding an endpoint that returns a bare array or an ad-hoc error object is a review failure, not a
  style preference.
- The status-code table is part of the public contract. Changing a mapping is an API break.
- `ErrorType` gaining a member without a mapping is caught by
  `ErrorStatusCodeMapTests.EveryErrorType_HasAnExplicitMapping`.
