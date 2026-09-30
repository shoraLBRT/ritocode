# content

The training content of Ritocode, in the format of
[`docs/CONTENT_FORMAT.md`](../docs/CONTENT_FORMAT.md), under the licence in [`LICENSE`](LICENSE).

```
taxonomy/     the six classes and the treatment tree, with Russian labels
problems/     one directory per problem card        (none yet — #124)
materials/    one directory per material             (none yet — #42)
tasks/        one directory per task                 (none yet — #42)
```

Check it before committing — CI runs the same command on every pull request:

```bash
dotnet run --project src/Ritocode.ContentTool -- validate content
```

## legacy-problems/

The C# refactoring packages of the product Ritocode was before 2026-09-30, in the old package
format. They moved here in [#120](https://github.com/shoraLBRT/ritocode/issues/120) so that
`problems/` could hold problem cards, and are deleted with the old format in
[#121](https://github.com/shoraLBRT/ritocode/issues/121). Until then the development seeder and the
old format's tests still read them. The empty `Directory.Build.props` and `Directory.Packages.props`
here exist for them — they keep the repository's MSBuild settings away from those packages — and go
with them.
