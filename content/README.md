# content

The training content of Ritocode, in the format of
[`docs/CONTENT_FORMAT.md`](../docs/CONTENT_FORMAT.md), under the licence in [`LICENSE`](LICENSE).

```
taxonomy/     the six classes and the treatment tree, with Russian labels
problems/     one directory per problem card        (3 so far — #124)
materials/    one directory per material             (1 so far — #42)
tasks/        one directory per task                 (1 so far — #42)
```

Check it before committing — CI runs the same command on every pull request:

```bash
dotnet run --project src/Ritocode.ContentTool -- validate content
```

A development host (`ASPNETCORE_ENVIRONMENT=Development`) loads this tree into its database when it
starts, so an edit shows up on the next run.

Cards are drafted with the `author-card` skill, and materials and tasks with `author-task`, which
also runs the blind smoke test — both in [`.claude/skills/`](../.claude/skills/).
