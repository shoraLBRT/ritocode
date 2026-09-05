# Storage Layout

Three things live in object storage rather than in PostgreSQL: **problem bundles**, **workspace
snapshots** and **evaluation artifacts**. This document fixes where each one goes — the buckets, the
key form, and what the database columns that point at them actually contain.

It is written *before* any code puts an object anywhere. A key is the most expensive kind of string
to change: once rows reference it, a different layout is a data migration rather than an edit, and
`problem_versions.snapshot_reference`, `workspaces.snapshot_reference` and
`submission_reports.logs_reference` are all `varchar(512)` columns whose content this document is
the only description of.

- Defined by [#5](https://github.com/shoraLBRT/ritocode/issues/5) *(partial)*. The storage client
  that reads and writes these keys is the next item in [SLICE_PLAN.md](SLICE_PLAN.md).
- **Retention and deletion are out of scope**, deferred with
  [#43](https://github.com/shoraLBRT/ritocode/issues/43). Nothing here says when an object dies.
- **Last updated:** 2026-09-05

## Buckets are roles; their names are configuration

There are three buckets, one per role. `compose.yaml` already creates them for local development.

| Role | Local bucket | Holds | Written by | Read by |
| --- | --- | --- | --- | --- |
| `problem-bundles` | `problem-bundles` | One archive per published problem version | Ingest ([#9](https://github.com/shoraLBRT/ritocode/issues/9)) | Workspace creation ([#10](https://github.com/shoraLBRT/ritocode/issues/10)), the orchestrator ([#17](https://github.com/shoraLBRT/ritocode/issues/17)) |
| `workspace-snapshots` | `workspace-snapshots` | One archive per workspace: its current working tree | File write ([#12](https://github.com/shoraLBRT/ritocode/issues/12)) | File read ([#11](https://github.com/shoraLBRT/ritocode/issues/11)), submission ([#14](https://github.com/shoraLBRT/ritocode/issues/14)) |
| `evaluation-artifacts` | `evaluation-artifacts` | Per submission: the frozen input tree and everything the run produced | The orchestrator ([#17](https://github.com/shoraLBRT/ritocode/issues/17)) and [#23](https://github.com/shoraLBRT/ritocode/issues/23) | The report API ([#16](https://github.com/shoraLBRT/ritocode/issues/16)) |

Three buckets rather than one bucket with three prefixes, because a bucket is the coarsest unit an
S3-compatible provider gives for lifecycle rules, and these three roles have three different answers
to *when may this be deleted*: a bundle outlives every workspace built from it, a workspace snapshot
dies with its workspace, and an artifact's life is tied to a report a user may still be reading.
Splitting them later means copying objects and rewriting every stored reference; splitting them now
costs two lines of `mc mb`, already written.

**The names above are the local names, not the layout.** Bucket names are globally unique on real
S3, so a deployment prefixes them (`ritocode-prod-problem-bundles`) and the application resolves a
role to a physical name through configuration — the option shape lands with the storage client.
Nothing inside a key ever names the environment, so an object copied between deployments keeps its
key.

## What a reference column contains

A reference is a **role and a key**, joined by `/`:

```
problem-bundles/problem-versions/018f3a2c-6b4e-7c31-9d05-2a1f4e8b7c60/bundle.tar.gz
└──── role ────┘└─────────────────────────── key ───────────────────────────────┘
```

It is **not** a URL. A stored `http://minio:9000/...` stops resolving the moment the endpoint, the
port or the provider changes, and it writes a deployment detail into data that outlives the
deployment. It is not a bare key either: the role is what lets a reference be read on its own — in
`psql`, in a log line, in a report — without knowing which column it came from, and what keeps the
physical bucket name, which is configuration, out of stored rows.

Two forms, told apart by the trailing slash, so a reader needs no schema to know which one it holds:

| Form | Ends with | Names | Example column |
| --- | --- | --- | --- |
| Object reference | anything but `/` | exactly one object | `problem_versions.snapshot_reference` |
| Prefix reference | `/` | every object beneath it | `submission_reports.logs_reference` |

A prefix reference exists because one submission produces a set of files whose membership is not
known when the column is written — two validators today, four after
[#19](https://github.com/shoraLBRT/ritocode/issues/19) is finished. The alternative, one archive per
submission, would make a user interface unpack the whole thing to show one validator's stderr. The
cost is that deleting a prefix reference is a list-then-delete rather than one call, which is
[#43](https://github.com/shoraLBRT/ritocode/issues/43)'s problem to solve once.

## The layout

`{id}` is a lower-case canonical UUID, 36 characters. `{validator}` is a validator's `id` from the
package manifest, already constrained to `^[a-z0-9]+(-[a-z0-9]+)*$` and 32 characters by
[PROBLEM_PACKAGE_SPEC.md](PROBLEM_PACKAGE_SPEC.md#validators).

```
problem-bundles/
  problem-versions/{problem_version_id}/bundle.tar.gz

workspace-snapshots/
  workspaces/{workspace_id}/tree.tar.gz

evaluation-artifacts/
  submissions/{submission_id}/
    input/tree.tar.gz
    validators/{validator}/stdout.txt
    validators/{validator}/stderr.txt
    validators/{validator}/output.tar.gz
```

| Reference | Stored in | Mutability |
| --- | --- | --- |
| `problem-bundles/problem-versions/{id}/bundle.tar.gz` | `problem_versions.snapshot_reference` | Write once |
| `workspace-snapshots/workspaces/{id}/tree.tar.gz` | `workspaces.snapshot_reference` | Overwritten on every save |
| `evaluation-artifacts/submissions/{id}/` | `submission_reports.logs_reference` | Prefix; the objects beneath it are write-once |
| `evaluation-artifacts/submissions/{id}/input/tree.tar.gz` | Nowhere — derived from the submission id | Write once |

The entity segment (`problem-versions/`, `workspaces/`, `submissions/`) is one segment of apparent
redundancy against the bucket role. It is kept so that a listing says what it is holding, and so a
second kind of object arriving in a bucket does not sit ambiguously beside bare UUIDs at the root.

### The submission input tree is a copy, not a pointer

At enqueue, the workspace's snapshot is copied server-side to
`evaluation-artifacts/submissions/{id}/input/tree.tar.gz`, and **the evaluation reads that copy,
never the live workspace key**.

The workspace key is overwritten every time the user saves. Evaluating from it would grade a
submission against whatever the tree happened to be when the worker reached it, and re-evaluating
the same submission — which [#38](https://github.com/shoraLBRT/ritocode/issues/38) does on purpose —
would read different bytes and could honestly produce a different verdict. That makes *the same
submission produces the same result* false at the storage layer, underneath anything
[ADR 0006](adr/0006-sandbox-execution-model.md) can guarantee, and the one test written to protect
that claim is where it would surface.

This is the only key not stored in a column: `submissions` has no reference column, so the key is
derived from the submission id instead. It is the single exception to rule 3 below, and a cheap one
to remove — [#14](https://github.com/shoraLBRT/ritocode/issues/14) can add the column when it builds
the lifecycle, and should if this layout ever moves.

## Rules a key obeys

1. **Built only from identifiers the platform generated.** UUIDs, and the validator `id` that a
   validated manifest schema has already constrained to a slug. Never a filename, never a
   user-supplied path, never a problem `slug` — a key assembled from user text is a path-traversal
   question in a place nobody thinks to look for one, and `slug` additionally belongs to a row that
   can change. Whatever is interpolated is checked against its pattern where the key is built, not
   only where it was parsed.
2. **Lower-case ASCII, `/` as the separator, fixed depth per class.** No spaces, no percent-encoded
   bytes, no nesting that varies with content. Every key above can be reconstructed by a person
   reading the table.
3. **A key is constructed on write and read back from the stored reference — never recomputed for a
   row that already has one.** This is what lets the layout change without a data migration: old
   rows keep pointing at where their objects actually are, and only new writes follow the new rule.
   Code that rebuilds a key from an id in order to find an existing object makes this document
   load-bearing forever.
4. **A reference fits in `varchar(512)`.** The longest this layout can produce is 127 characters, at
   `evaluation-artifacts/submissions/{36}/validators/{32}/output.tar.gz`. The margin is deliberate:
   a provider that prefixes keys, or a later class of artifact, has room without a migration.
5. **No date or hash partitioning.** An object store at this scale does not need a key spread, and a
   date in a key is a second source of truth for a timestamp that is already a column.

## Object formats

`.tar.gz` is tar plus gzip: both are in the .NET base class library (`System.Formats.Tar`,
`GZipStream`), so a tree becomes an object with no new package and no new decision. `stdout.txt` and
`stderr.txt` are UTF-8 text, captured and truncated by the runner per
[ADR 0006](adr/0006-sandbox-execution-model.md) §5. `output.tar.gz` is that validator's `/out` mount,
collected by [#23](https://github.com/shoraLBRT/ritocode/issues/23).

Archive **bytes are not reproducible**, and are not required to be — tar records timestamps and gzip
records its own. Determinism is a property of the normalised projection of a run
([ADR 0006](adr/0006-sandbox-execution-model.md) §6), never of an artifact's bytes. A test asserting
that two bundles are byte-identical is asserting the wrong thing.

A workspace snapshot is written as one whole object per save. A put is atomic per object, so a
reader sees the previous tree or the new one and never half of either; two saves racing means the
later put wins, which is the same answer `workspaces.updated_at` gives.

## What is deliberately not here

- **Fixtures are not uploaded.** A package's `passing` and `failing` trees are the known-good and
  known-bad answers. Nothing in the running system reads them — they are content for
  [#38](https://github.com/shoraLBRT/ritocode/issues/38), which has the committed package tree — and
  an object that must never be served to a user is safest as an object that does not exist. The
  bundle is the manifest, the description and the workspace root, which is why it can be served
  without filtering.
- **Retention, lifecycle rules and deletion.** Deferred with
  [#43](https://github.com/shoraLBRT/ritocode/issues/43). Every object written today is written
  forever; that is a known hole rather than an oversight, and the bucket split above is what keeps
  the eventual rules from having to reason about mixed content.
- **Presigned URLs and direct browser access.** Every read goes through the API during the slice.
  Nothing here forbids presigning later — the keys carry no secret — but they do carry ids that the
  ownership checks in [#35](https://github.com/shoraLBRT/ritocode/issues/35) exist to guard.
- **A version segment for this layout.** Rule 3 makes it unnecessary: references are stored, so two
  layouts coexist without a `v1/` in the key to say which is which.
