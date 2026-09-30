---
name: author-card
description: Draft a Ritocode problem card — one entry of the problem catalogue under content/problems/ — from a name or an idea, in the file format of docs/CONTENT_FORMAT.md, in Russian, checked by content validate. Use whenever the maintainer wants a new card or a batch of cards ("add a card for …", "draft cards for the growth class", "write the catalogue"), and before writing any card by hand. It reads the live catalogue every time, so the summary of the new card is written against the cards that exist now.
---

# Author a problem card

A problem card describes one thing that goes wrong in code an agent wrote: how it is recognised,
what it costs, when it is acceptable, how it is treated. The format is
[`docs/CONTENT_FORMAT.md`](../../../docs/CONTENT_FORMAT.md) §4; what a card is for is
[`docs/SPEC.md`](../../../docs/SPEC.md) §3.2. Read both before the first card of a session.

You draft; **the maintainer decides**. A card you write is a proposal they edit and commit.

## 1. Read the live catalogue — every run

Never work from memory or from a list in this file. Read, from the working tree:

- `content/taxonomy/classes.yaml` and `content/taxonomy/ru.yaml` — the six classes and what each is.
- Every `content/problems/*/card.yaml` and `content/problems/*/ru.md` — at least the `name`,
  `summary` and class of each card.

Then answer, before writing anything:

1. **Does a card for this already exist, under another name?** If so, stop and say which. Improving
   an existing card is an edit to it, not a new card.
2. **Which existing cards are its nearest neighbours** — the ones a learner could confuse it with?
   Name them. The new summary must say what this card is *not*, against them.

## 2. Choose the slug, the class and the weight

- **Slug**: lower-case English, digits and hyphens, starting with a letter, at most 64 characters —
  `god-class`, `secrets-in-repo`. It is permanent: attempts name cards by slug. **Refuse to write
  into a directory that already exists.**
- **Class**: one of the six, by *cause*, because the cause decides the treatment (SPEC §3.1). Give a
  one-line reason.
- **Weight**: 1, 2 or 3 — how much missing it costs, everywhere. Secrets in the repository are 3; a
  magic number is 1. Give a one-line reason.

## 3. Write the two files

`content/problems/<slug>/card.yaml`:

```yaml
class: <class>
weight: <1|2|3>
```

`content/problems/<slug>/ru.md` — front matter, then sections under **fixed English headings**:

```markdown
---
name: <Russian name>
summary: >-
  <One line: how it is recognised. When a neighbour is close, what this card is not.>
keywords: [<extra words a learner might search for>]
---

## Signs
## Why AI does it
## Cost
## Acceptable when
## Detection
## Treatment
## Sources
## Counter-arguments
```

`Signs`, `Cost`, `Acceptable when` and `Treatment` are required; the rest are optional but expected.
The reader is a **vibe coder** who knows little about architecture (SPEC §2.1):

- Plain Russian, short sentences, no jargon a beginner would not know — or explain it in passing.
- **Signs**: what is visible in the code, concretely — names, shapes, sizes. The learner sees only
  the name and summary during a task, so the summary must be enough to recognise the problem.
- **Why AI does it**: the mechanism, not blame — what in the brief or the model produces it.
- **Cost**: what becomes more expensive, as a short list. Include the cost to the next agent run
  where there is one (a bigger file in its context, a pattern it will copy).
- **Acceptable when**: the conditions under which it is fine — the field the product turns on
  (SPEC §3.2). "Никогда." is a valid answer only when it is true.
- **Detection**: what can find it automatically — a linter rule, a threshold, a scanner. Stack-
  independent where possible.
- **Treatment**: the right treatment, and one sentence on the wrong one.
- **Sources**: Fowler, Beck, Martin, OWASP, CWE, Sonar rules, research. **Name and reference only —
  never quote more than a few words.**
- **Counter-arguments**: where the industry disagrees, in plain text. No links to other cards.

## 4. Check it

```bash
dotnet run --project src/Ritocode.ContentTool -- validate content
```

Fix every error. A warning is fine to leave if you say why.

## 5. Hand it over

Tell the maintainer, in a few lines: the slug, the class and the weight with their reasons, the
neighbours the summary delimits it from, and anything you were unsure of. Do not commit on their
behalf unless they asked you to.

## Never

- Never overwrite an existing card.
- Never invent a source. If you are not sure a rule id or a book says what you claim, leave it out.
- Never write the answer to a task into a card. A card is general; a task's key is the task's.
- Never add relations between cards (SPEC §3.2): a neighbour is delimited in the summary, not linked.
