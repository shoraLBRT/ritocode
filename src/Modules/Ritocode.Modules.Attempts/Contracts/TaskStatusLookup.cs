using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Ritocode.Modules.Attempts.Lifecycle;
using Ritocode.Modules.Attempts.Persistence;
using Ritocode.Modules.Attempts.Scoring;
using Ritocode.Shared.Contracts.Attempts;

namespace Ritocode.Modules.Attempts.Contracts;

/// <summary>The Attempts module's answer to <see cref="ITaskStatusLookup"/>, over its own schema.</summary>
internal sealed class TaskStatusLookup(AttemptsDbContext context) : ITaskStatusLookup
{
    public async Task<IReadOnlyDictionary<string, string>> FindStatusesAsync(
        Guid userId,
        IReadOnlyCollection<string> taskSlugs,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(taskSlugs);

        if (taskSlugs.Count == 0)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        var slugs = taskSlugs.Distinct(StringComparer.Ordinal).ToList();

        // A learner's submitted attempts at a page of tasks: a few dozen rows at the MVP's scale, each
        // result read here to tell a solving attempt from another.
        var submitted = await context.OwnedBy(userId)
            .Where(attempt => attempt.SubmittedAt != null && slugs.Contains(attempt.TaskSlug))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var reveals = await context.RevealsOf(userId)
            .Where(reveal => slugs.Contains(reveal.TaskSlug))
            .ToDictionaryAsync(reveal => reveal.TaskSlug, reveal => reveal.RevealedAt, StringComparer.Ordinal, cancellationToken)
            .ConfigureAwait(false);

        var byTask = submitted.ToLookup(attempt => attempt.TaskSlug, StringComparer.Ordinal);

        return slugs.ToDictionary(
            slug => slug,
            slug => Name(TaskStatuses.Of(
                byTask[slug].Select(attempt => attempt.AsSubmitted()),
                reveals.TryGetValue(slug, out var revealedAt) ? revealedAt : null)),
            StringComparer.Ordinal);
    }

    /// <summary>The status as the API names it: camelCase, as every enum it answers with (ADR 0003).</summary>
    public static string Name(LearnerTaskStatus status) => JsonNamingPolicy.CamelCase.ConvertName(status.ToString());
}
