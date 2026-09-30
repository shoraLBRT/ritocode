# Content Format

- **Status:** Draft for the maintainer's review, with [`SPEC.md`](SPEC.md)
- **Date:** 2026-09-30
- **Replaces:** `PROBLEM_PACKAGE_SPEC.md`, which described a refactoring exercise graded by validators

This file defines how problem cards, the taxonomy, materials and tasks are written as files under
`content/`. What they mean, and how they are used, is in [`SPEC.md`](SPEC.md) §3. Validation, ingest
and both authoring skills read these files and nothing else.

The format has one design goal besides correctness: **writing a task must stay cheap.** An author
writes prose and a short list of findings. Anything the system can compute — shortlists, the
material overview, line counts — it computes.

---

## 1. Layout

```
content/
  LICENSE                     the content licence
  taxonomy/
    classes.yaml              the six classes: identifiers and order
    treatments.yaml           the treatment tree: branches, leaves, order
    ru.yaml                   Russian labels and descriptions for both
  problems/
    <card-slug>/
      card.yaml               language-neutral: class, weight
      ru.md                   Russian text of the card
  materials/
    <material-slug>/
      material.yaml           language, optional author notes
      files/                  the project itself, as the learner sees it
  tasks/
    <task-slug>/
      task.yaml               language-neutral: material, difficulty, answer key
      ru.md                   Russian text of the task
```

**The locale split is the whole localisation mechanism.** Everything that does not change between
languages — slugs, classes, weights, leaves, answer keys, code — sits in `.yaml` files and the
material. Everything that does sits in one file per locale, named by the locale. Adding English
means adding `en.md` and `en.yaml` beside the Russian ones; nothing existing is edited.

`ru` is the **default locale**: every card, task and taxonomy entry must have it. Any other locale
may be incomplete; a missing text falls back to the default.

---

## 2. Identifiers

- **Slugs** are lower-case ASCII, digits and hyphens, starting with a letter, at most 64 characters:
  `secrets-in-repo`, `god-class`. A slug is the directory name; it is not repeated inside the files.
- **A slug is permanent.** Attempts and signals refer to cards and tasks by slug. Renaming a card's
  title is free; renaming its slug is removing one card and adding another.
- **Leaves** are addressed as `branch.leaf`: `auto.secrets`, `accept.fits-context`.
- **Classes** are `disproportion`, `project-knowledge`, `hygiene`, `growth`, `false-confidence` and
  `domain`.

---

## 3. Taxonomy

### `taxonomy/classes.yaml`

```yaml
classes:
  - disproportion
  - project-knowledge
  - hygiene
  - growth
  - false-confidence
  - domain
```

The order is the order of the groups on every screen.

### `taxonomy/treatments.yaml`

```yaml
branches:
  - id: brief
    leaves: [scale, lifetime, scope, requirements, reuse]
  - id: rule
    leaves: [structure, limits, conventions, forbidden, tests]
  - id: auto
    leaves: [linter, types, secrets, metrics, dependencies, architecture, test-quality]
  - id: manual
    leaves: [split, dedupe, extract-config, handle-errors, validate, representation, invariant, remove, tests]
  - id: accept
    leaves: [fits-context, not-worth-it, debt]
```

The tree and its meaning are defined in [`SPEC.md`](SPEC.md) §3.3. A leaf identifier is unique
within its branch; `rule.tests` and `manual.tests` are different leaves.

### `taxonomy/ru.yaml`

```yaml
classes:
  disproportion:
    name: Несоразмерность
    description: Решение не по масштабу задачи — больше или меньше, чем нужно.
  hygiene:
    name: Гигиена и безопасность
    description: То, что должно быть закрыто оградой, а не внимательностью.
  # … every class

treatments:
  brief:
    name: Постановка задачи агенту
    leaves:
      scale: масштаб — сколько пользователей и данных
      lifetime: срок жизни и кто будет поддерживать
      scope: границы — что не трогать и чего не добавлять
      requirements: обязательные свойства — безопасность, точность, совместимость
      reuse: использовать то, что уже есть в проекте
  accept:
    name: Оставить осознанно
    leaves:
      fits-context: в этом контексте это нормально
      not-worth-it: исправление дороже проблемы
      debt: записать как долг с условием пересмотра
  # … every branch and every leaf
```

---

## 4. Problem cards

### `problems/<slug>/card.yaml`

```yaml
class: hygiene
weight: 3          # 1, 2 or 3
```

### `problems/<slug>/ru.md`

Front matter for the short fields, then one section per long field. **Section headings are fixed
English keys in every locale**, so the parser never depends on the language of the content.

```markdown
---
name: Секреты в репозитории
summary: >-
  Пароли, ключи и токены прямо в коде или в закоммиченных конфигах.
  Адреса и пути без секретов — это «Хардкод конфигурации».
keywords: [пароль, токен, ключ, credentials, api key]
---

## Signs

Строка подключения с паролем в константе; `.env` в репозитории; токен в тесте.

## Why AI does it

Агенту нужен работающий пример прямо сейчас, а о том, куда положить секрет, никто не сказал.

## Cost

- Пароль в истории git остаётся навсегда — откат не помогает.
- Доступ получает каждый, у кого есть доступ к репозиторию, включая CI и подрядчиков.

## Acceptable when

Никогда.

## Detection

Сканер секретов в CI и на pre-commit.

## Treatment

Убрать значение из кода и истории, отозвать и перевыпустить доступ, дальше — сканер.
Неправильное лечение: вынести пароль в другой файл того же репозитория.

## Sources

OWASP: hardcoded credentials · CWE-798

## Counter-arguments

Нет.
```

| Key | Where | Required |
| --- | --- | --- |
| `name` | front matter | yes |
| `summary` | front matter, one line of prose | yes |
| `keywords` | front matter, list | no |
| `## Signs` | section | yes |
| `## Why AI does it` | section | no |
| `## Cost` | section | yes |
| `## Acceptable when` | section; "never" is a valid answer | yes |
| `## Detection` | section | no |
| `## Treatment` | section | yes |
| `## Sources` | section | no |
| `## Counter-arguments` | section | no |

Sections are Markdown. An unknown heading at the second level is an error, so a typo in a heading
never silently drops a field. The order of sections is free.

The summary is what the learner sees in step 1, together with the name. It must say how the problem
is **recognised**, and, when a neighbouring card is close, what this card is **not**.

---

## 5. Materials

### `materials/<slug>/material.yaml`

```yaml
language: python
notes: >-
  Optional. For authors only, never shown to learners: what this code was
  written to contain, and which tasks use it.
```

### `materials/<slug>/files/`

The project exactly as the learner sees it: directory structure, file names, content. Rules:

- UTF-8 text only. No binary files, no symbolic links.
- Code, comments and string literals in **English**.
- **No hint comments** — nothing that names or excuses the problem.
- Lines of at most **79 characters**.
- Dependencies declared in `requirements.txt` or in `pyproject.toml` under `[project]
  dependencies`, if the project has any. The material overview lists them.
- **Secrets are fake and never in a real provider's format.** GitHub scans every push, and a string
  shaped like a real key — `AKIA…`, `ghp_…`, `sk_live_…`, `xoxb-…` — will be blocked or flagged. Use
  plain passwords and connection strings, which make the same point. If a task genuinely needs a
  provider-shaped token, its material path is added to `paths-ignore` in
  `.github/secret_scanning.yml` in the same pull request.

Ingest computes the **overview** — the file tree, lines per file, total lines, declared
dependencies — from these files. An author never writes it.

---

## 6. Tasks

### `tasks/<slug>/task.yaml`

```yaml
material: invoice-mailer
difficulty: easy            # easy | medium | hard
findings:
  - card: secrets-in-repo
    leaves: [auto.secrets, rule.conventions]
  - card: money-in-float
    leaves: [manual.representation]
  - card: swallowed-error
    leaves: [manual.handle-errors]
```

- `findings` may be empty — a clean task is `findings: []`.
- Each finding names an existing card and **at least one** existing leaf. Several leaves mean *any
  of them is right* ([`SPEC.md`](SPEC.md) §5.2).
- A card appears at most once per task.
- A finding is an object on purpose: a later version adds an optional `location` to it.

### `tasks/<slug>/ru.md`

```markdown
---
title: Счёт клиенту по почте
---

## Context

Скрипт бухгалтера небольшой студии. Запускается руками раз в месяц, счетов — десяток.
Поддерживает сам бухгалтер. Если сломается — счёт отправят вручную.

## Brief

«Напиши скрипт, который считает сумму счёта и отправляет его клиенту на почту.»

## Notes

### money-in-float

Десять счетов в месяц — а копейки всё равно расходятся с бухгалтерией.

## Lesson

Масштаб маленький, но деньги и секреты от масштаба не зависят.
```

| Key | Where | Required |
| --- | --- | --- |
| `title` | front matter | yes |
| `## Context` | section; prose, optionally a list of facts | yes |
| `## Brief` | section; the agent's instruction, verbatim | yes |
| `## Notes` | section; one `###` subsection per card slug, shown beside that finding in the review | no |
| `## Lesson` | section; shown at the end of the review | no |

A `###` subsection under Notes must name a card that is among the task's findings.

The matching material for this example — about twenty lines — is
`materials/invoice-mailer/files/invoice.py`:

```python
import smtplib

SMTP_PASSWORD = "mail-Pass-2024"


def invoice_total(items):
    total = 0.0
    for item in items:
        total += item["price"] * item["qty"]
    return round(total, 2)


def send_invoice(customer_email, items):
    body = f"Total: {invoice_total(items)}"
    try:
        with smtplib.SMTP("smtp.example.com", 587) as smtp:
            smtp.login("billing", SMTP_PASSWORD)
            smtp.sendmail("billing@example.com", customer_email, body)
    except Exception:
        pass
```

The hardcoded SMTP host is not a finding here: in a script one person runs once a month, it is
acceptable, and the author has left it out of the key. A second task over the same material, with a
context of a hosted service used by many studios, would list `hardcoded-config` with
`manual.extract-config` — that is what one material serving several contexts means.

---

## 7. Validation

`content validate` checks the whole tree and reports every fault at once. It runs in the authoring
skills, in CI on every pull request, and inside ingest before anything is written.

**Errors** — ingest refuses to run:

- a slug that is malformed or duplicated;
- a missing required file or a required field missing in the default locale;
- an unknown section heading in a card or task text;
- a card whose class is not in `classes.yaml`, or whose weight is not 1, 2 or 3;
- a finding naming a card or leaf that does not exist, a finding with no leaves, or a card listed
  twice in one task;
- a Notes subsection naming a card that is not among the task's findings;
- a task whose material does not exist, or a material that is not UTF-8 text;
- a card removed from `content/` while a published task still lists it.

**Warnings** — reported, not blocking:

- material over its difficulty's line band ([`SPEC.md`](SPEC.md) §3.4);
- a line longer than 79 characters;
- an easy task whose shortlist cannot reach its target size because the catalogue is too small;
- a material no task uses.

---

## 8. What ingest derives

Computed at ingest, never written by an author:

- **The material overview:** file tree, lines per file, total lines, declared dependencies.
- **The shortlist of an easy task:** its findings' cards plus 15–20 others drawn from the whole
  catalogue, chosen deterministically from the task's slug so that every learner sees the same
  list.
- **The content revision:** the commit the content was loaded from, stamped on everything ingest
  writes and recorded on every attempt.

---

## 9. Licence

Everything under `content/` — cards, taxonomy texts, materials and tasks — is published under
**CC BY-SA 4.0** unless a file says otherwise. This is a default the maintainer can change before
the first card is committed; it keeps the catalogue open to the community authorship planned for a
later stage.
