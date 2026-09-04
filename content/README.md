# content

Training content, authored as **problem packages** — the format is
[`docs/PROBLEM_PACKAGE_SPEC.md`](../docs/PROBLEM_PACKAGE_SPEC.md).

```
problems/
  example-order-total/    the reference package: what the format looks like, checked by tests
```

`example-order-total` exists to keep the specification honest. It is loaded and validated by
`tests/Ritocode.Modules.Problems.Tests`, so a change to the format that this package does not
satisfy fails the build. It is not catalog content and is not published; the catalog's first real
problems arrive with [#42](https://github.com/shoraLBRT/ritocode/issues/42), which is also where
the language of the shipped tasks is decided. The reference package is written in C# because the
repository is, and that choice binds nothing.

This tree is content, not code: no project here is in `Ritocode.slnx`, and nothing here is built by
CI. The empty `Directory.Build.props` and `Directory.Packages.props` stop the repository's own
MSBuild settings — central package versions, warnings as errors — from reaching a package that has
to build inside a runner image instead, on its own terms.
