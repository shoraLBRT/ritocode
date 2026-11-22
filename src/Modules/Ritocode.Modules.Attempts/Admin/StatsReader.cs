using Microsoft.EntityFrameworkCore;
using Ritocode.Modules.Attempts.Domain;
using Ritocode.Modules.Attempts.Persistence;
using Ritocode.Modules.Attempts.Progress;
using Ritocode.Modules.Attempts.Scoring;
using Ritocode.Shared.Contracts.Content;

namespace Ritocode.Modules.Attempts.Admin;

/// <summary>
/// The admin area's per-task and per-card statistics (#305): how learners fare, to calibrate the size
/// bands and the scoring parameters (SPEC §3.4, §5, §8). Every user's rows, which is why its endpoints
/// are mapped behind the admin policy and nowhere else.
/// </summary>
public interface IStatsReader
{
    Task<TaskStatsReport> TasksAsync(CancellationToken cancellationToken);

    Task<CardStatsReport> CardsAsync(CancellationToken cancellationToken);
}

/// <remarks>
/// Computed on read from every attempt and reveal, in memory: a few thousand rows at the MVP's scale.
/// Nothing is stored, so the figures follow the results and never need rewriting.
/// </remarks>
internal sealed class StatsReader(AttemptsDbContext context, ITaskSummaryLookup tasks, ICardClassLookup cards) : IStatsReader
{
    /// <summary>The fewest learners for a task to be marked rarely solved: below it the share says little.</summary>
    public const int OutlierMinLearners = 5;

    /// <summary>A solved share below this marks a task solved by almost nobody.</summary>
    public const double RarelySolvedShare = 0.2;

    public async Task<TaskStatsReport> TasksAsync(CancellationToken cancellationToken)
    {
        // One query after another: a DbContext runs one at a time.
        var attempts = await context.EveryonesAttempts()
            .Select(attempt => new Row(
                attempt.UserId,
                attempt.TaskSlug,
                attempt.StartedAt,
                attempt.Step,
                attempt.SubmittedAt,
                attempt.CountsTowardProgress,
                attempt.Score,
                attempt.MaxScore,
                attempt.Result))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var reveals = (await context.EveryonesReveals()
            .Select(reveal => new { reveal.UserId, reveal.TaskSlug, reveal.RevealedAt })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false))
            .ToDictionary(reveal => (reveal.UserId, reveal.TaskSlug), reveal => reveal.RevealedAt);

        var known = (await tasks.ListAsync(cancellationToken).ConfigureAwait(false))
            .ToDictionary(task => task.Slug, StringComparer.Ordinal);

        var byTask = attempts.ToLookup(attempt => attempt.TaskSlug, StringComparer.Ordinal);

        var slugs = known.Keys
            .Union(byTask.Select(group => group.Key), StringComparer.Ordinal)
            .Order(StringComparer.Ordinal);

        return new TaskStatsReport([.. slugs.Select(slug => TaskOf(slug, known.GetValueOrDefault(slug), byTask[slug], reveals))]);
    }

    public async Task<CardStatsReport> CardsAsync(CancellationToken cancellationToken)
    {
        var results = await context.EveryonesAttempts()
            .Where(attempt => attempt.CountsTowardProgress && attempt.Result != null)
            .Select(attempt => attempt.Result!)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // The learner's own measures, for everyone: every first attempt counts as revealed here, since
        // the admin reads the keys anyway (ADR 0017 hides them from the learner, not from the author).
        var firstAttempts = results
            .Select(result => new FirstAttempt(AttemptsJson.Read<DiagnosisScore>(result), AnswerRevealed: true))
            .ToList();

        var slugs = firstAttempts.SelectMany(attempt => attempt.Score.Cards).Select(line => line.Card).Distinct(StringComparer.Ordinal).ToList();
        var catalogue = await cards.FindAsync(slugs, cancellationToken).ConfigureAwait(false);
        var progress = ProgressCalculator.Calculate(firstAttempts, catalogue);

        return new CardStatsReport(
            firstAttempts.Count,
            [
                .. progress.Cards.Select(card => new CardStatsView(
                    card.Card,
                    card.Name,
                    card.Class,
                    card.Class is null ? null : catalogue.ClassNames.GetValueOrDefault(card.Class) ?? card.Class,
                    card.Met,
                    card.Found,
                    card.Missed,
                    card.PickedWhenAbsent,
                    card.TreatedRight,
                    card.PickedWhenAbsent > card.Found)),
            ]);
    }

    /// <summary>The median and quartiles of <paramref name="seconds"/>, or null when there are none.</summary>
    internal static TimeSpreadView? SpreadOf(IReadOnlyList<long> seconds)
    {
        if (seconds.Count == 0)
        {
            return null;
        }

        var sorted = seconds.Order().ToArray();

        return new TimeSpreadView(sorted.Length, Quantile(sorted, 0.5), Quantile(sorted, 0.25), Quantile(sorted, 0.75));
    }

    private static TaskStatsView TaskOf(
        string slug,
        TaskSummary? summary,
        IEnumerable<Row> rows,
        Dictionary<(Guid UserId, string TaskSlug), DateTimeOffset> reveals)
    {
        var all = rows.ToList();
        var submitted = all.Where(row => row.SubmittedAt is not null).ToList();
        var first = submitted.Where(row => row.CountsTowardProgress).ToList();
        var open = all.Where(row => row.SubmittedAt is null).ToList();

        // A learner is solved as the task list says (TaskStatuses): an attempt, first or practice,
        // solved it no later than the reveal.
        var learners = submitted.GroupBy(row => row.UserId).ToList();
        var solved = learners.Count(learner => TaskStatuses.Of(
            learner.Select(row => new SubmittedAttempt(row.SubmittedAt!.Value, AttemptOutcome.Of(AttemptsJson.Read<DiagnosisScore>(row.Result!)).Solved)),
            reveals.TryGetValue((learner.Key, slug), out var revealedAt) ? revealedAt : null) == LearnerTaskStatus.Solved);
        var revealed = learners.Count(learner => reveals.ContainsKey((learner.Key, slug)));

        var scored = first.Where(row => row.MaxScore > 0).ToList();
        double? solvedShare = learners.Count == 0 ? null : (double)solved / learners.Count;

        return new TaskStatsView(
            slug,
            summary?.Title ?? slug,
            summary?.Difficulty,
            summary?.Published ?? false,
            summary?.Lines,
            summary is null ? null : new SizeBandView(summary.Band.MaxLines, summary.Band.MinutesFrom, summary.Band.MinutesTo),
            first.Count,
            submitted.Count - first.Count,
            learners.Count,
            solvedShare,
            learners.Count == 0 ? null : (double)revealed / learners.Count,
            SpreadOf([.. first.Select(row => (long)(row.SubmittedAt!.Value - row.StartedAt).TotalSeconds)]),
            new OpenAttemptsView(
                open.Count(row => row.Step == AttemptStep.Diagnosis),
                open.Count(row => row.Step == AttemptStep.Treatment)),
            scored.Count == 0 ? null : scored.Average(row => (double)row.Score!.Value / row.MaxScore!.Value),
            learners.Count >= OutlierMinLearners && solvedShare < RarelySolvedShare);
    }

    /// <summary>The <paramref name="q"/> quantile by linear interpolation between closest ranks, rounded to the second.</summary>
    private static long Quantile(long[] sorted, double q)
    {
        var position = q * (sorted.Length - 1);
        var lower = (int)Math.Floor(position);
        var upper = (int)Math.Ceiling(position);
        var value = sorted[lower] + ((sorted[upper] - sorted[lower]) * (position - lower));

        return (long)Math.Round(value, MidpointRounding.AwayFromZero);
    }

    private sealed record Row(
        Guid UserId,
        string TaskSlug,
        DateTimeOffset StartedAt,
        AttemptStep Step,
        DateTimeOffset? SubmittedAt,
        bool CountsTowardProgress,
        int? Score,
        int? MaxScore,
        string? Result);
}
