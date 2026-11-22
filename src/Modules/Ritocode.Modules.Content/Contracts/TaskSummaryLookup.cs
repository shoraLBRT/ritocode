using Microsoft.EntityFrameworkCore;
using Ritocode.Modules.Content.Format;
using Ritocode.Modules.Content.Ingest;
using Ritocode.Modules.Content.Persistence;
using Ritocode.Shared.Contracts.Content;

namespace Ritocode.Modules.Content.Contracts;

/// <summary>The Content module's answer to <see cref="ITaskSummaryLookup"/>, over its own schema.</summary>
internal sealed class TaskSummaryLookup(ContentDbContext context) : ITaskSummaryLookup
{
    public async Task<IReadOnlyList<TaskSummary>> ListAsync(CancellationToken cancellationToken)
    {
        // One query after another: a DbContext runs one at a time.
        var tasks = await context.Tasks.AsNoTracking()
            .Select(task => new { task.Slug, task.Material, task.Difficulty, task.Texts, task.UnpublishedAt })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var overviews = await context.Materials.AsNoTracking()
            .Select(material => new { material.Slug, material.Overview })
            .ToDictionaryAsync(material => material.Slug, material => material.Overview, StringComparer.Ordinal, cancellationToken)
            .ConfigureAwait(false);

        return
        [
            .. tasks
                .OrderBy(task => task.Slug, StringComparer.Ordinal)
                .Select(task => new TaskSummary(
                    task.Slug,
                    ContentJson.Read<Dictionary<string, TaskText>>(task.Texts).GetValueOrDefault(ContentRules.DefaultLocale)?.Title ?? task.Slug,
                    task.Difficulty,
                    task.UnpublishedAt is null,
                    overviews.TryGetValue(task.Material, out var overview) ? ContentJson.Read<MaterialOverview>(overview).TotalLines : 0,
                    BandOf(task.Difficulty))),
        ];
    }

    private static SizeBand BandOf(string difficulty)
    {
        var parsed = Enum.TryParse<TaskDifficulty>(difficulty, ignoreCase: true, out var value) ? value : TaskDifficulty.Hard;
        var (lines, _) = ContentRules.SizeBand(parsed);
        var (from, to) = ContentRules.TaskMinutes(parsed);

        return new SizeBand(lines, from, to);
    }
}
