# 0008 — Authentication seam

- Status: Proposed
- Date: 2026-09-12
- Relates to: [`docs/SLICE_PLAN.md`](../SLICE_PLAN.md), [#6](https://github.com/shoraLBRT/ritocode/issues/6), [#35](https://github.com/shoraLBRT/ritocode/issues/35)
- Builds on: [0003](0003-api-conventions.md), [0004](0004-persistence-and-migrations.md), [0005](0005-vertical-slice-before-breadth.md), [0007](0007-cross-module-contract-form.md)

## Context

[ADR 0005](0005-vertical-slice-before-breadth.md) allows the slice to ship **a seeded development
identity instead of a login**, and names the seam that keeps the reduction reversible:
"`ICurrentUser` and the authentication middleware exist; no endpoint changes when a real provider is
wired in". It also forbids two things that the absence of a seam makes tempting — taking `user_id`
from a request body or query string, and serving a user-owned resource without checking it belongs
to the caller.

That settles **that** there is a seam. It does not settle its shape, and the shape is what the rest
of the slice is written against. Every endpoint from stage 3 on reads a user: workspace creation,
file read and write, submission, the report. If the shape is chosen in the first of those endpoints,
it is chosen in a constructor by one session, and the six that follow inherit it without it ever
having been decided.

Four questions come due together, and each has a wrong answer that is invisible until much later:

1. **What `ICurrentUser` exposes.** A username and an email on it would be convenient at the call
   site and would put a database read on the authentication path of every request.
2. **Where authentication happens.** Middleware that sets a user id in a dictionary satisfies the
   letter of "the seam exists" and is not substitutable: the replacement has to reproduce it rather
   than plug into it.
3. **Whether an endpoint is protected by default.** Every workspace and submission endpoint in
   stages 3 and 4 is protected; each one is written by a session that has to remember.
4. **What the seeded identity actually is.** `workspaces.user_id` and `submissions.user_id` are
   required and carry no foreign key ([ADR 0004](0004-persistence-and-migrations.md)), and the
   module creating such a row validates the reference itself
   ([ADR 0007](0007-cross-module-contract-form.md)). An identity with no row passes authentication
   and fails at the first workspace, several layers from the cause.

The token format — JWT, or opaque plus a server-side store — is **not** on this list. It is the
maintainer's decision, due in stage two, and the whole point of a seam is that it is invisible from
here.

## Decision

### 1. `ICurrentUser` is one value wide

```csharp
public interface ICurrentUser
{
    Guid? Id { get; }
}
```

It answers "who is calling" and nothing else. Anything further about the user is a question about a
row in the Users module's schema, which is what ADR 0007's `IUserLookup` is for — a contract with an
owner, a batching story and a `CancellationToken`, none of which belong on an ambient request
property.

`Id` is nullable because anonymous is a real state: the catalog is browsed by people who have no
identity, and from stage 4 the evaluation worker runs with no request at all. Code that cannot serve
an anonymous caller says so with `RequireId()`, which throws an `AppException` carrying
`ErrorType.Unauthenticated` — so the caller receives the same 401 body the middleware would have
produced, rather than an opaque 500 that reads as a platform fault.

It lives in `Ritocode.Shared/Identity`, beside `Storage` and for the same reason: it is host
infrastructure that every module consumes and none owns. This is **not** a cross-module contract in
ADR 0007's sense and is deliberately not under `Contracts/`. A contract is one module asking another
a question about its rows; this reports what the authentication middleware already established, and
no module answers it. The distinction is load-bearing, because ADR 0007 §7's third assertion
requires every contract interface to be implemented in a `Ritocode.Modules.*` assembly — and this
one correctly is not.

### 2. Authentication is a real scheme, owned by the Auth module

The development identity is an `AuthenticationHandler`, registered as the default scheme by
`AuthModule`. Not middleware that assigns a user id, and not a service that reads a header.

This is what makes the reduction reversible in the way ADR 0005 claims. Stage two replaces the
handler with one that reads a session token and looks it up; the scheme name, the claim, the
authorisation policies, `ICurrentUser` and every endpoint stay exactly as they are. A hand-rolled
mechanism would have to be unpicked instead of swapped, which is the difference between "less of
Phase 1" and "a worse Phase 1".

The Auth module owns it because the Auth module owns authentication — its replacement is the login
and session issuance that complete #6, and those are unambiguously its work. The host keeps only the
pipeline position, because where `UseAuthentication` sits relative to CORS and the endpoints is a
host concern and not a module's to decide.

The user identifier travels as a named claim, `ritocode:user_id`, declared once in
`RitocodeClaimTypes`. Named explicitly rather than reusing `ClaimTypes.NameIdentifier` so that a
stage-two handler maps its token's subject onto it in code a reader can find, instead of relying on
a framework's inbound claim mapping to do it invisibly.

### 3. Authenticated by default, anonymous by exception

The host sets a fallback authorisation policy requiring an authenticated user. An endpoint that
states no authorisation requirement of its own is protected.

The alternative — open by default, `RequireAuthorization()` on each protected endpoint — is one word
shorter per endpoint and fails in the direction that costs the most. A workspace endpoint that
forgets is a workspace readable by anyone who can guess an id, which is the third row of ADR 0005's
forbidden list, and nothing fails until somebody notices. Under a fallback policy the same omission
is a 401 on the first test that calls it.

Endpoints meant to stay open say `AllowAnonymous()` where they are mapped. Every endpoint that
exists today already did, because the catalog was written in anticipation of this. The four of them
are pinned by tests against a host with no identity, so an `AllowAnonymous` lost in a refactor fails
`dotnet test` rather than surprising the first person who opens the catalog while signed out.

### 4. The seeded identity is configured, fixed, and has a row

`Authentication:DevelopmentIdentity` carries `Enabled`, `UserId`, `Email` and `Username`. The Auth
module's handler asserts that identity; the Users module keeps a matching row in `users.users`. The
two agree because they read the same configured identifier, not because either calls the other — a
module may not write another module's schema, and the settings therefore live in `Ritocode.Shared`
where both can bind them.

The identifier is **configuration rather than generated**. A new one per start would orphan every
workspace and submission created under the previous one, silently, because there is no foreign key
to complain.

`Enabled` defaults to **off**. Switched on, this authenticates every request as one fixed user with
no credential — a development convenience in Development and an authentication bypass anywhere else.
It is not refused outside Development, because the slice is meant to be put in front of real people
on a deployed host that still has no login; it logs a warning naming the environment instead. The
seeding runs as an `IHostedService` so it completes before the first request, and a failure is
logged rather than thrown: a host that will not start because a database is briefly down is a worse
answer than one that starts unready, and `/health/ready` already reports that.

### 5. A rejected request answers in the ADR 0003 body

A challenge writes the same `application/problem+json` envelope as every other failure —
`code: "unauthenticated"`, a `requestId`, and the correlation header — rather than an empty 401. The
frontend already branches on it through `ApiError.isUnauthenticated`.

No `WWW-Authenticate` header is offered. There is no credential a client could be told to present
yet, and a scheme a browser understands would put a native credential prompt in front of a
single-page application.

### 6. What this deliberately leaves out

Login, session issuance and `/me`, per `SLICE_PLAN.md`. With a seeded identity there is no
signed-out state in the browser and no login route to redirect to, which also settles the question
[#26](https://github.com/shoraLBRT/ritocode/issues/26) left open: **the frontend route guard renders
in place rather than redirecting**, and it is not written yet because there is nothing yet for it to
guard against.

Authorisation beyond "is authenticated" is out too. Ownership checks are
[#35](https://github.com/shoraLBRT/ritocode/issues/35) in stage 3, and ADR 0005 is explicit that
they are not hardening to be deferred — they are the other half of having authentication. This ADR
supplies the identity those checks compare against and claims nothing more.

## Alternatives considered

**A middleware that reads a header and stores a user id.** Fewer moving parts than a scheme, and it
satisfies "the seam exists". Rejected: it is not substitutable. ASP.NET Core's authorisation stack —
`AllowAnonymous`, fallback policies, the challenge path that produces the 401 — is built on
authentication schemes, so a bespoke mechanism means reimplementing all of it, and the replacement
in stage two plugs into nothing.

**`ICurrentUser` carrying a user summary.** One call instead of two once a screen needs a username.
Rejected: it puts a database read on every authenticated request including those that never look at
it, and it duplicates `IUserLookup` with no owner and no batching story. The endpoint that needs a
username can ask the module that owns it.

**`Guid Id` that throws when anonymous, instead of `Guid?`.** Reads better at the call site.
Rejected: anonymous is a legitimate state — the catalog, the worker — and a property that throws
makes every caller that can handle it write a `try`. `Guid?` plus `RequireId()` puts the decision at
the call site, where the two cases actually differ.

**Open by default, `RequireAuthorization()` per endpoint.** The framework's own default, and
explicit at each endpoint. Rejected in §3: the omission fails silently and in the expensive
direction.

**Refuse to start when the development identity is enabled outside Development.** Safer, and what a
production system should do. Rejected for the slice specifically: ADR 0005's whole purpose is to put
the journey in front of real people, and those people reach a deployed host that has no login for
several more weeks. A warning naming the environment keeps the fact visible without forbidding the
thing the slice exists to do. This is worth revisiting the moment a real session provider lands, and
the entry is in **Open questions**.

**Defer all of this to whichever endpoint needs a user first.** Cheapest today, and exactly what
ADR 0007 refused for cross-module contracts. Rejected for the same reason: the shape would be
decided in a constructor, the reasoning would not survive the session, and six endpoints would
inherit it unexamined.

## Consequences

- Every endpoint written from stage 3 on takes its user from `ICurrentUser` and is protected unless
  it says otherwise. The forbidden reduction — `user_id` from the request — has no reason to be
  reached for, because the alternative is one constructor parameter.
- The Users module writes rows for the first time. It owns the development identity's row, which is
  also the first user that [#10](https://github.com/shoraLBRT/ritocode/issues/10) will validate
  through `IUserLookup` in the next box.
- A deployment that enables the development identity has no authentication. That is the reduction
  ADR 0005 chose, it is off by default, and it is logged loudly where it matters.
- Replacing the handler in stage two changes the Auth module and nothing else. If that turns out to
  be false, this ADR was wrong and gets superseded rather than edited.
- The token format stays undecided and stays cheap to decide, which was the point.
