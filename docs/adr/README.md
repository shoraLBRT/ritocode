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
| [0007](0007-cross-module-contract-form.md) | Cross-module contract form | Accepted |
| [0008](0008-authentication-seam.md) | Authentication seam | Proposed |
| [0010](0010-diagnosis-of-ai-written-code.md) | Ritocode teaches diagnosis of AI-written code | Accepted |

## Removed

The maintainer chose to remove, rather than keep as history, the ADRs that governed only the
product Ritocode was before 2026-09-30 ([#40](https://github.com/shoraLBRT/ritocode/issues/40)). Older
ADRs still mention them by number. Each remains readable at the tag `pre-diagnosis`.

| ADR | Title | Why it went |
| --- | --- | --- |
| 0005 | Vertical slice before breadth | Superseded by 0010; the slice plan it ordered is gone |
| 0006 | Sandbox execution model | Nothing the learner writes is executed any more (#119) |
| 0009 | Evaluation is a command the Submissions module issues | The evaluation pipeline and Submissions module are gone (#119) |

## Writing a new ADR

Copy the structure of an existing file. Number sequentially, use `Status: Proposed` until it is
agreed, then `Accepted`. Add a row to the table above.
