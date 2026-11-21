using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Ritocode.Modules.Attempts.Persistence;
using Ritocode.Shared.PersonalData;

namespace Ritocode.Modules.Attempts.PersonalData;

/// <summary>The Attempts module's part of an export: attempts, signals, reveals and study marks.</summary>
public sealed record AttemptsExport(
    IReadOnlyList<AttemptExport> Attempts,
    IReadOnlyList<SignalExport> Signals,
    IReadOnlyList<RevealExport> Reveals,
    IReadOnlyList<StudyMarkExport> StudyMarks);

/// <summary>
/// An attempt as it is stored. <paramref name="Result"/> and <paramref name="Review"/> carry the answer
/// key, so they are in an export only once the learner has revealed the task's answer, as everywhere
/// else (ADR 0017); before that they are <see langword="null"/>.
/// </summary>
public sealed record AttemptExport(
    Guid Id,
    string Task,
    DateTimeOffset StartedAt,
    string Step,
    DateTimeOffset? SubmittedAt,
    string? ContentRevision,
    JsonElement? Answer,
    JsonElement? Result,
    JsonElement? Review,
    int? Score,
    int? MaxScore,
    bool CountsTowardProgress);

/// <summary>A signal the learner sent from a review.</summary>
public sealed record SignalExport(Guid Id, Guid Attempt, string Task, string Card, string? Comment, DateTimeOffset CreatedAt, DateTimeOffset? ResolvedAt);

/// <summary>A task whose answer the learner revealed.</summary>
public sealed record RevealExport(string Task, DateTimeOffset RevealedAt);

/// <summary>A card the learner marked «Изучено».</summary>
public sealed record StudyMarkExport(string Card, DateTimeOffset StudiedAt);

/// <summary>
/// The Attempts module's part of <see cref="IPersonalData"/>. Every row is reached through the module's
/// owner-scoped lookups (<see cref="OwnedAttempts"/> and its siblings), so the owner is in every query.
/// </summary>
internal sealed class AttemptsPersonalData(AttemptsDbContext context) : IPersonalData
{
    public string Module => "Attempts";

    public bool HoldsTheAccount => false;

    public async Task<object> ExportAsync(Guid userId, CancellationToken cancellationToken)
    {
        var reveals = await context.RevealsOf(userId)
            .OrderBy(reveal => reveal.RevealedAt)
            .Select(reveal => new RevealExport(reveal.TaskSlug, reveal.RevealedAt))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var revealed = reveals.Select(reveal => reveal.Task).ToHashSet(StringComparer.Ordinal);

        var attempts = await context.OwnedBy(userId)
            .OrderBy(attempt => attempt.StartedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var signals = await context.SignalsOf(userId)
            .OrderBy(signal => signal.CreatedAt)
            .Select(signal => new SignalExport(signal.Id, signal.AttemptId, signal.TaskSlug, signal.Card, signal.Comment, signal.CreatedAt, signal.ResolvedAt))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var marks = await context.StudyMarksOf(userId)
            .OrderBy(mark => mark.StudiedAt)
            .Select(mark => new StudyMarkExport(mark.CardSlug, mark.StudiedAt))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new AttemptsExport(
            [.. attempts.Select(attempt =>
            {
                var keyShown = revealed.Contains(attempt.TaskSlug);

                return new AttemptExport(
                    attempt.Id,
                    attempt.TaskSlug,
                    attempt.StartedAt,
                    JsonNamingPolicy.CamelCase.ConvertName(attempt.Step.ToString()),
                    attempt.SubmittedAt,
                    attempt.ContentRevision,
                    Json(attempt.Answer),
                    keyShown ? Json(attempt.Result) : null,
                    keyShown ? Json(attempt.Review) : null,
                    attempt.Score,
                    attempt.MaxScore,
                    attempt.CountsTowardProgress);
            })],
            signals,
            reveals,
            marks);
    }

    public async Task<int> EraseAsync(Guid userId, CancellationToken cancellationToken)
    {
        // A signal names an attempt, so signals go before attempts.
        var erased = await context.SignalsOf(userId).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        erased += await context.RevealsOf(userId).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        erased += await context.StudyMarksOf(userId).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        erased += await context.OwnedBy(userId).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);

        return erased;
    }

    private static JsonElement? Json(string? stored)
    {
        if (stored is null)
        {
            return null;
        }

        using var document = JsonDocument.Parse(stored);
        return document.RootElement.Clone();
    }
}
