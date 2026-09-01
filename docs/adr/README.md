# Architecture Decision Records

Each ADR captures one decision, the alternatives weighed, and the consequences accepted.
They are append-only: a decision that no longer holds gets a new ADR that supersedes the old one,
and the old one is marked `Superseded by NNNN` rather than edited.

| ADR | Title | Status |
| --- | --- | --- |
| [0001](0001-technology-stack.md) | Technology stack | Accepted |
| [0002](0002-modular-monolith-layout.md) | Modular monolith layout | Accepted |
| [0003](0003-api-conventions.md) | API conventions | Accepted |

## Writing a new ADR

Copy the structure of an existing file. Number sequentially, use `Status: Proposed` until it is
agreed, then `Accepted`. Add a row to the table above.
