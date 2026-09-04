# Problem Package Specification

A **problem package** is the unit in which training content is authored, reviewed and shipped. It
holds everything needed to put a task in front of a user and to reach a verdict on their answer:
the prose, the code they start from, which files they may change, and the validator pipeline that
grades the result.

This document is the format's contract. It is implemented by
`src/Modules/Ritocode.Modules.Problems/Packaging`, and the reference package in
[`content/problems/example-order-total`](../content/problems/example-order-total) is validated
against that implementation by `tests/Ritocode.Modules.Problems.Tests`. A change to the format
changes all three, in one commit.

- Defined by [#8](https://github.com/shoraLBRT/ritocode/issues/8).
- **Current `schema_version`: 1.**

## Why a manifest at all

A package could be "just a folder", with conventions in someone's head. ADR 0005 lists that among
the forbidden shortcuts, for one reason: every task authored before the format exists gets redone
once it arrives. The manifest also carries three things a folder cannot state — which files a user
is allowed to change, what the verdict is computed from, and the limits that keep a workspace and
its evaluation bounded.

## Package layout

```
example-order-total/
  problem.yaml         the manifest; the only required file at a fixed path
  description.md       task prose, Markdown, referenced by `description`
  starter/             the workspace root: exactly what the user opens
  fixtures/
    passing/           a known-good answer, overlaid on the starter tree
    failing/           a known-bad answer, overlaid on the starter tree
```

Only `problem.yaml` has a fixed name and location. Every other path is declared in the manifest;
the layout above is the convention the example follows and the one new packages should copy.

Files that are not the manifest, not the description, not under the workspace root and not inside a
declared fixture directory are ignored. They are allowed — an authoring note, a script — but nothing
reads them.

## problem.yaml

```yaml
schema_version: 1

slug: example-order-total
title: Untangle the order total calculator
difficulty: medium
language: csharp
tags:
  - refactoring
  - code-quality
description: description.md

hints:
  - OrderTotal.Calculate does three jobs at once; name them before you move anything.
  - The tests describe the behaviour that must survive. Nothing else has to.

workspace:
  root: starter
  editable:
    - src/**/*.cs
  readonly:
    - tests/**
    - "*.md"

limits:
  max_files: 200
  max_file_bytes: 262144
  max_total_bytes: 5242880

validators:
  - id: compile
    type: compile
    weight: 30
    required: true
    timeout_seconds: 120
    with:
      command: [dotnet, build, --warnaserror]
  - id: tests
    type: test
    weight: 70
    required: true
    timeout_seconds: 300
    with:
      command: [dotnet, test, --no-build]

fixtures:
  passing: fixtures/passing
  failing: fixtures/failing
```

### Top-level fields

| Field | Type | Required | Rule |
| --- | --- | --- | --- |
| `schema_version` | integer | yes | Must equal `1`. A loader that meets a version it does not know rejects the package rather than guessing. |
| `slug` | string | yes | `^[a-z0-9]+(-[a-z0-9]+)*$`, at most 128 characters. Becomes `problems.slug`, so it is the catalog URL and must not change once published. |
| `title` | string | yes | 1–200 characters, no line breaks. |
| `difficulty` | enum | yes | `easy`, `medium` or `hard` — the values of `Difficulty`. |
| `language` | string | yes | `^[a-z0-9][a-z0-9+#.-]*$`, at most 32 characters. Selects the runner image; the registry of known values belongs to [#22](https://github.com/shoraLBRT/ritocode/issues/22), so the manifest checks the shape and not the membership. |
| `tags` | list of string | yes | 1–8 entries, unique, each matching the `slug` pattern and at most 32 characters. |
| `description` | string | yes | Package-relative path to a UTF-8 Markdown file. Must exist and not be empty. |
| `hints` | list of string | no | 0–5 entries, each 1–280 characters. Order is the order shown. |
| `workspace` | mapping | yes | See [Workspace and allowed paths](#workspace-and-allowed-paths). |
| `limits` | mapping | no | See [Limits](#limits). Every field defaults. |
| `validators` | list | yes | 1–8 entries. See [Validators](#validators). |
| `fixtures` | mapping | no | See [Fixtures](#fixtures). |

Prose lives in a file rather than in the manifest because a task description is Markdown of real
length, and YAML block scalars make it painful to write and unreadable in a diff.

An unknown key anywhere in the manifest is an error, not a warning. A typo in `timeout_seconds` that
is silently ignored is a validator running with the wrong timeout for as long as nobody notices.

### Workspace and allowed paths

| Field | Type | Required | Rule |
| --- | --- | --- | --- |
| `root` | string | no, defaults to `starter` | Package-relative directory materialised into the workspace. Must exist and hold at least one file. |
| `editable` | list of glob | yes | At least one entry. Files the user may change. |
| `readonly` | list of glob | no | Files the user sees but may not change. |

Globs are matched against paths relative to the workspace root, with `/` as the separator,
case-sensitively. `*` matches within one segment, `**` matches across segments, `?` matches a single
character.

Two rules make the declaration total, and both are checked when the package is loaded:

1. **Every file under the workspace root matches exactly one list.** A file matched by neither is an
   error, and so is a file matched by both. There is no default class, because the default would
   decide security policy by omission.
2. **At least one file is editable.** A task nobody can change is not a task.

`readonly` is enforced twice, and the second time is the one that matters. The write endpoint
([#12](https://github.com/shoraLBRT/ritocode/issues/12)) rejects a write to a read-only path, and
the orchestrator restores every read-only file from the package before the validators run
([#17](https://github.com/shoraLBRT/ritocode/issues/17)). A submitted tree can therefore be tampered
with and still be graded honestly: the tests that decide the verdict are the package's tests, never
the ones in the submission.

Hidden files — tests the user never sees — are deliberately not in this version. They would be a
third path class and an overlay applied at evaluation time, and the slice does not need one. When
they arrive they arrive as an added key and a `schema_version` bump.

Declared paths are ordinary relative paths: no leading `/`, no `..` segment, no Windows drive
letter, no backslash. Symbolic links inside a package are rejected — a link is a path that means
something different on the machine that resolves it, which is the opposite of reproducible.

### Limits

Bounds on the workspace tree, applied to the materialised package and again to the submitted tree
([#36](https://github.com/shoraLBRT/ritocode/issues/36)).

| Field | Type | Default | Range |
| --- | --- | --- | --- |
| `max_files` | integer | 200 | 1–2000 |
| `max_file_bytes` | integer | 262144 (256 KiB) | 1–4194304 (4 MiB) |
| `max_total_bytes` | integer | 5242880 (5 MiB) | 1–104857600 (100 MiB), and not below `max_file_bytes` |

These are limits on **content**, not on execution. CPU, memory, pid and network limits are
properties of the sandbox, not of a task, and an author cannot be allowed to raise them by editing a
file — they are fixed by the runner and belong to ADR 0006. The only execution bound the manifest
sets is a per-validator `timeout_seconds`, which the runner treats as an upper bound it may lower,
never as one it must honour.

### Validators

`validators` is an ordered pipeline. The orchestrator runs the entries in the order written, and
stops at the first failure of an entry marked `required` — a task that does not compile has nothing
to test.

| Field | Type | Required | Rule |
| --- | --- | --- | --- |
| `id` | string | yes | `^[a-z0-9]+(-[a-z0-9]+)*$`, at most 32 characters, unique within the pipeline. Identifies the entry in the report, so it is stable across runs. |
| `type` | string | yes | Same pattern and length. Selects the validator plugin ([#18](https://github.com/shoraLBRT/ritocode/issues/18)); the plugin registry, and the rejection of an unknown type, live there. |
| `weight` | integer | yes | 0–100. **The weights of a pipeline must sum to exactly 100.** |
| `required` | boolean | no, defaults to `true` | A failed required entry fails the submission whatever the score. |
| `timeout_seconds` | integer | yes | 1–900. |
| `with` | mapping | no | Free-form configuration handed to the plugin. The manifest carries it; it does not interpret it. |

Two entries may share a `type` with different `id`s and different `with` — one pipeline can run two
test suites. Weights summing to 100 rather than being normalised is deliberate: normalisation means
adding a validator silently reweights the others, and the author does not see it happen.

`with` is the seam that keeps this format stable while validators are still being written. The
compile and test validators of the slice read `command`; a lint validator will read something else,
and neither this document nor the loader has to change for it.

### Fixtures

| Field | Type | Required | Rule |
| --- | --- | --- | --- |
| `passing` | string | no | Package-relative directory holding a known-good answer. |
| `failing` | string | no | Package-relative directory holding a known-bad answer. |

A fixture is an **overlay**: each file in it replaces the file at the same relative path in the
workspace tree. Every fixture file's path must be matched by `editable` — a fixture may only change
what a user could have changed, or it proves nothing about the task.

Fixtures are how a package earns the claim that its verdict discriminates:
[#42](https://github.com/shoraLBRT/ritocode/issues/42) requires a known-good and a known-bad answer
per problem, and [#38](https://github.com/shoraLBRT/ritocode/issues/38) evaluates them and asserts
that the good one passes, the bad one fails, and both are reproducible. They are content, never
served to a user, and never materialised into a workspace.

## validator_config

`problem_versions.validator_config` (`jsonb`, [DATABASE_SCHEMA.md](DATABASE_SCHEMA.md)) stores the
pipeline, and nothing else from the manifest. The projection is mechanical:

```json
{"schemaVersion":1,"validators":[{"id":"compile","type":"compile","weight":30,"required":true,"timeoutSeconds":120,"with":{"command":["dotnet","build","--warnaserror"]}}]}
```

- Field names are `camelCase`, matching the JSON the API already emits.
- Every field is written, including one left to its default in the manifest. A stored pipeline
  states what it does; it does not depend on the loader's defaults, which may change.
- Keys inside `with` are sorted ordinally, and scalars keep their YAML type: `120` is a number,
  `"120"` is a string, `true` is a boolean.

The projection is **canonical**: the same pipeline produces byte-identical JSON regardless of how
the YAML was written or in what order its keys appeared. Determinism is the product's central
claim, and a pipeline that serialises differently on two machines makes "the same submission" an
untestable phrase.

The rest of the manifest is not stored here. `slug`, `title`, `difficulty`, `description` and `tags`
become columns on `problems`; the package tree becomes the bundle at
`problem_versions.snapshot_reference`. `hints` and `limits` have no column yet — they travel with
the bundle until an issue needs them in SQL.

## Loading a package

`ProblemPackageLoader` reads a directory and returns either a `ProblemPackage` — the manifest, the
classified file list, and the canonical `validator_config` — or a validation `AppError` whose
`Fields` name the manifest paths that are wrong, in the shape [ADR 0003](adr/0003-api-conventions.md)
already defines for every other validation failure in the system.

It reports **every** problem it finds, not the first. An author fixing a package one error per run
is an author who stops writing packages. The two exceptions are the gates: a manifest that does not
parse has no fields to check, and a manifest whose fields are wrong cannot be compared against
files — the paths to compare are the ones that are wrong.

Checks, in order:

1. `problem.yaml` exists, is UTF-8, and parses as YAML with no duplicate and no unknown keys.
2. The manifest satisfies every rule in the tables above.
3. `description` exists, is a file inside the package, and is not empty.
4. `workspace.root` exists, is a directory inside the package, and holds at least one file.
5. Every file under the workspace root matches exactly one of `editable` / `readonly`, and at least
   one is editable.
6. The workspace tree is within `limits`.
7. No entry in the package is a symbolic link, and no declared path escapes the package.
8. Each declared fixture directory exists, is not empty, and every file in it has a
   workspace-relative path that `editable` matches.

Loading never executes anything from the package. A manifest is data; running a package's code is
the sandbox runner's job, and only the sandbox runner's — `docs/AGENT_GUIDELINES.md` and ADR 0005
are both explicit, and a content pipeline is exactly the place where that rule gets bent by
accident.

## Constraints on a package

Beyond what a loader can check:

- **Deterministic.** The same submission scores the same, on any machine, at any time. No test that
  depends on wall-clock time, network access, locale, file ordering or a random seed the package
  does not fix.
- **Self-contained.** Everything the task needs is inside the package or inside the runner image for
  its `language`. Nothing is fetched during evaluation; the sandbox has no network.
- **Honest about scope.** The task is stated in `description.md` and the verdict follows from it.
  A validator that grades something the description never asked for is a bug in the package.
- **Small.** A workspace a person can read in one sitting. The limits above are a ceiling, not a
  target.

## Changing this format

`schema_version` is how the format moves. Adding an optional field with a default is a
backwards-compatible change and keeps version 1. Anything else — a removed field, a renamed field, a
tightened rule, a new required field — raises the version, and the loader keeps rejecting what it
does not understand.
