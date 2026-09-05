# Architecture Decision Records

Each ADR captures one decision, the alternatives weighed, and the consequences accepted.
They are append-only: a decision that no longer holds gets a new ADR that supersedes the old one,
and the old one is marked `Superseded by NNNN` rather than edited.

| ADR | Title | Status |
| --- | --- | --- |
| [0001](0001-technology-stack.md) | Technology stack | Accepted |
| [0002](0002-modular-monolith-layout.md) | Modular monolith layout | Accepted |
| [0003](0003-api-conventions.md) | API conventions | Accepted |
| [0004](0004-persistence-and-migrations.md) | Persistence and migrations | Accepted |
| [0005](0005-vertical-slice-before-breadth.md) | Vertical slice before breadth | Accepted |
| [0006](0006-sandbox-execution-model.md) | Sandbox execution model | Accepted |

## Writing a new ADR

Copy the structure of an existing file. Number sequentially, use `Status: Proposed` until it is
agreed, then `Accepted`. Add a row to the table above.
