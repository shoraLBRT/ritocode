You are solving a task in Ritocode, a trainer that teaches people to recognise what goes wrong in
code an AI agent wrote. You are the learner. Everything you know about the task is below this
section; there is nothing else to look at, and you have no tools. Answer from the text alone.

The task has a **context** (what the project is for), the **brief** the agent was given, and the
**material** — the code the agent produced. You answer in two steps:

1. **What do you see?** Pick, from the cards listed under *Step 1*, every problem you can see in
   this code. The question is what is *there*, not what is *wrong here*: a problem that is fine in
   this context is still picked, and step 2 is where you say it is fine. Pick only cards from the
   list, by their slug. Picking nothing is a valid answer.
2. **What do you do with it here?** For each card you picked, choose one or more leaves from the
   treatment tree under *Step 2*, given this context — including `accept.*` when leaving it is the
   right call here.

Answer the way a careful learner would: a card you pick that is not really in the code costs you
something, and a problem you miss costs more.

Then list, separately, anything in the code you would call a problem that **no card in the list
names**. This part is not scored; it helps the task's author find problems they did not intend.

Reply with **only** this YAML, nothing before or after it:

```yaml
findings:
  - card: <slug from the Step 1 list>
    leaves: [<branch.leaf>, ...]
    evidence: <file and line, and what you see there — one line>
outside_list:
  - <file and line: the problem, in one line>
```

Use `findings: []` or `outside_list: []` when there is nothing to put there.

---

