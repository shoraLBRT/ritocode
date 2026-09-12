# content

Training content, authored as **problem packages** — the format is
[`docs/PROBLEM_PACKAGE_SPEC.md`](../docs/PROBLEM_PACKAGE_SPEC.md).

```
problems/
  split-the-invoice/        easy    — decimal arithmetic that loses a penny
  no-double-booking/        medium  — an interval comparison that disagrees with itself at a boundary
  respect-the-precedence/   hard    — an expression evaluator that associates the wrong way
  example-order-total/      the reference package: what the format looks like, checked by tests
```

The first three are the catalog content of
[#42](https://github.com/shoraLBRT/ritocode/issues/42), all in **C#** — the language the maintainer
chose on 2026-09-11, recorded under *Open questions* in
[`docs/PROJECT_STATE.md`](../docs/PROJECT_STATE.md). Each one ships a known-good and a known-bad
answer under `fixtures/`, and the two genuinely disagree: the bad one is the plausible near-miss, not
a deliberately broken file, so a verdict that passes both says something is wrong with the verdict.

`example-order-total` exists to keep the specification honest. It is loaded and validated by
`tests/Ritocode.Modules.Problems.Tests`, so a change to the format that this package does not
satisfy fails the build. It is a fixture rather than catalog content and is not meant to be solved —
but it sits in this directory, and the development seeder publishes everything in this directory, so
a local host serves four problems rather than three.

**Every package here is graded by `compile` and then `test`, and nothing else** — the remaining
validators are stage two. So each of the three catalog problems starts from code that **fails at
least one of its own tests**: a task whose starter already passes would hand an untouched workspace a
perfect score. The prose asks for the refactoring, the failing test is what can actually be checked,
and each `description.md` says so under *What is graded*.

This tree is content, not code: no project here is in `Ritocode.slnx`, and nothing here is built by
CI. The empty `Directory.Build.props` and `Directory.Packages.props` stop the repository's own
MSBuild settings — central package versions, warnings as errors — from reaching a package that has
to build inside a runner image instead, on its own terms. All four packages pin the same three
package versions on purpose: the runner image's offline cache
([#22](https://github.com/shoraLBRT/ritocode/issues/22)) has to hold every dependency any package
names, and one set is cheaper to warm than four.
