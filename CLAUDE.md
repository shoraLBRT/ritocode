# CLAUDE.md

**Read [docs/CONCEPT.md](docs/CONCEPT.md) first — it defines what Ritocode is — then
[docs/SPEC.md](docs/SPEC.md), which specifies what gets built.** Ritocode is a catalogue of what
breaks in AI-written code and a trainer that teaches people to recognise it
([ADR 0010](docs/adr/0010-diagnosis-of-ai-written-code.md)).

**Work is taken from [docs/ROADMAP.md](docs/ROADMAP.md)**, lowest open stage first;
[docs/PROJECT_STATE.md](docs/PROJECT_STATE.md) says what exists and how to verify a change.

**Ritocode follows [habze](https://github.com/shoraLBRT/habze)**, the maintainer's way of working
for every project built by agents: the `session`, `session-reserve` and `session-full` skills, and
the rules of its [SPEC](https://github.com/shoraLBRT/habze/blob/main/docs/SPEC.md) §2–4 that
[AGENTS.md](AGENTS.md) repeats. habze has no `STANDARD.md` version yet, so this repository follows
no version and is **not yet adopted** ([shoraLBRT/habze#13](https://github.com/shoraLBRT/habze/issues/13));
the gaps are listed in [PROJECT_STATE.md](docs/PROJECT_STATE.md#habze-adoption). The skills are not
in this repository yet ([shoraLBRT/habze#8](https://github.com/shoraLBRT/habze/issues/8)) and know
nothing about Ritocode, so everything a session needs here is in these documents; where a skill and
these documents disagree, these documents win.

See [AGENTS.md](AGENTS.md) for how to work in this repository.
