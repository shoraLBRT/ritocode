using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using Ritocode.Modules.Attempts.Domain;
using Ritocode.Modules.Attempts.Persistence;
using Ritocode.Modules.Attempts.Scoring;
using Ritocode.Shared.Contracts.Content;
using Ritocode.Shared.Contracts.Users;
using Ritocode.Shared.Errors;
using Ritocode.Shared.Paging;

namespace Ritocode.Modules.Attempts.Lifecycle;

/// <summary>An attempt from start to result (docs/SPEC.md §5.4). Every method acts for one user, on that user's rows only.</summary>
public interface IAttemptLifecycle
{
    Task<Result<AttemptView>> StartAsync(Guid userId, string taskSlug, CancellationToken cancellationToken);

    Task<Result<AttemptView>> RecordStepAsync(Guid userId, Guid attemptId, AttemptStep step, CancellationToken cancellationToken);

    /// <summary>Scores the answer and stores it with its result. The result is never rewritten afterwards.</summary>
    Task<Result<AttemptView>> SubmitAsync(Guid userId, Guid attemptId, DiagnosisAnswer answer, CancellationToken cancellationToken);

    Task<Result<AttemptView>> GetAsync(Guid userId, Guid attemptId, CancellationToken cancellationToken);

    /// <summary>The user's attempts, newest first, optionally at one task.</summary>
    Task<Page<AttemptSummaryView>> ListAsync(Guid userId, string? taskSlug, PageRequest request, CancellationToken cancellationToken);
}

/// <remarks>
/// The constructor is the module's dependency list (ADR 0007 §1): Attempts depends on Users, to know
/// the caller is a user, and on Content, for the task and its key.
/// </remarks>
internal sealed class AttemptLifecycle(
    AttemptsDbContext context,
    IUserLookup users,
    ITaskForAttemptLookup tasks,
    IOptions<ScoringParameters> scoring,
    IOptions<AttemptRateLimitOptions> rateLimit,
    TimeProvider clock) : IAttemptLifecycle
{
    public const string AttemptNotFoundCode = "attempt_not_found";

    /// <summary>
    /// The code the Content module answers for a task nobody can open, chosen here rather than handed
    /// over (ADR 0007 §2), so a client branches on one meaning whichever endpoint it asked.
    /// </summary>
    public const string TaskNotFoundCode = "task_not_found";

    public const string AlreadySubmittedCode = "attempt_already_submitted";

    public const string RateLimitedCode = "attempt_rate_limited";

    public async Task<Result<AttemptView>> StartAsync(Guid userId, string taskSlug, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(taskSlug);

        // attempts.user_id has no foreign key (ADR 0004): this is what keeps a row from naming a user
        // who does not exist.
        if (await users.FindAsync(userId, cancellationToken).ConfigureAwait(false) is null)
        {
            return AppError.Unauthenticated(message: "The authenticated identity does not name a user.");
        }

        // An unpublished task cannot be started, as it cannot be opened; it can still be submitted.
        var task = await tasks.FindAsync(taskSlug, cancellationToken).ConfigureAwait(false);

        if (task is not { Published: true })
        {
            return AppError.NotFound(TaskNotFoundCode, $"There is no published task '{taskSlug}'.");
        }

        var attempt = Attempt.Start(userId, task.Slug, Now());

        context.Attempts.Add(attempt);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return View(attempt);
    }

    public async Task<Result<AttemptView>> RecordStepAsync(Guid userId, Guid attemptId, AttemptStep step, CancellationToken cancellationToken)
    {
        var attempt = await context.FindOwnedAsync(userId, attemptId, cancellationToken).ConfigureAwait(false);

        if (attempt is null)
        {
            return AttemptNotFound();
        }

        if (attempt.IsSubmitted)
        {
            return AlreadySubmitted();
        }

        attempt.Reach(step);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return View(attempt);
    }

    public async Task<Result<AttemptView>> SubmitAsync(Guid userId, Guid attemptId, DiagnosisAnswer answer, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(answer);

        var now = Now();
        var limit = rateLimit.Value;

        // Counted over the rows, so a restart or a second API instance does not reset it. A burst of
        // concurrent submits can each see room and together pass the cap by the size of the burst; the
        // cap exists to stop a script, not to be an exact count, so that race is accepted.
        var windowStart = now - limit.Window;
        var recent = await context.OwnedBy(userId)
            .CountAsync(candidate => candidate.SubmittedAt > windowStart, cancellationToken)
            .ConfigureAwait(false);

        if (recent >= limit.MaxSubmissions)
        {
            return AppError.RateLimited(
                RateLimitedCode,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"At most {limit.MaxSubmissions} answers can be checked in any {limit.Window:c}. Try again later."));
        }

        var attempt = await context.FindOwnedAsync(userId, attemptId, cancellationToken).ConfigureAwait(false);

        if (attempt is null)
        {
            return AttemptNotFound();
        }

        if (attempt.IsSubmitted)
        {
            return AlreadySubmitted();
        }

        // Tasks are never deleted, only unpublished, so an attempt's task is always found. A learner who
        // started before the task was unpublished may still check the answer.
        var task = await tasks.FindAsync(attempt.TaskSlug, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Attempt {attempt.Id} names the task '{attempt.TaskSlug}', which has no row.");

        // Against the answer as sent, so a field path names the pick the client sent at that index.
        if (Invalid(answer, task) is { } invalid)
        {
            return invalid;
        }

        var normalised = Normalise(answer);

        var key = task.Findings.Select(finding => new KeyFinding(finding.Card, finding.Weight, finding.Leaves)).ToList();
        var score = DiagnosisScoring.Score(normalised, key, scoring.Value);

        var first = !await context.OwnedBy(userId)
            .AnyAsync(candidate => candidate.TaskSlug == attempt.TaskSlug && candidate.CountsTowardProgress, cancellationToken)
            .ConfigureAwait(false);

        attempt.Submit(
            now,
            task.ContentRevision,
            AttemptsJson.Write(normalised),
            AttemptsJson.Write(score),
            score.Total,
            score.Maximum,
            countsTowardProgress: first);

        try
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException exception) when (first && IsFirstSubmissionRace(exception))
        {
            // Another first submit at this task committed between the check and this write.
            attempt.CountAsPractice();
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return View(attempt);
    }

    public async Task<Result<AttemptView>> GetAsync(Guid userId, Guid attemptId, CancellationToken cancellationToken)
    {
        var attempt = await context.FindOwnedAsync(userId, attemptId, cancellationToken).ConfigureAwait(false);

        return attempt is null ? AttemptNotFound() : View(attempt);
    }

    public async Task<Page<AttemptSummaryView>> ListAsync(Guid userId, string? taskSlug, PageRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var query = context.OwnedBy(userId);

        if (taskSlug is not null)
        {
            query = query.Where(candidate => candidate.TaskSlug == taskSlug);
        }

        var total = await query.LongCountAsync(cancellationToken).ConfigureAwait(false);

        var items = await query
            .OrderByDescending(candidate => candidate.StartedAt)
            .ThenByDescending(candidate => candidate.Id)
            .Skip((int)request.Offset)
            .Take(request.PageSize)
            .Select(candidate => new AttemptSummaryView(
                candidate.Id,
                candidate.TaskSlug,
                candidate.StartedAt,
                candidate.Step,
                candidate.SubmittedAt,
                candidate.SubmittedAt != null && !candidate.CountsTowardProgress,
                candidate.Score,
                candidate.MaxScore))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Page<AttemptSummaryView>.From(items, request, total);
    }

    /// <summary>
    /// The clock, to the microsecond PostgreSQL keeps, so the attempt a write returns reads the same as
    /// the one a later read returns.
    /// </summary>
    private DateTimeOffset Now()
    {
        var now = clock.GetUtcNow();
        return now.AddTicks(-(now.Ticks % TimeSpan.TicksPerMicrosecond));
    }

    public static AppError AttemptNotFound() => AppError.NotFound(AttemptNotFoundCode, "No such attempt.");

    private static AppError AlreadySubmitted() =>
        AppError.Conflict(AlreadySubmittedCode, "The attempt has been submitted; start a new one to answer again.");

    private static AttemptView View(Attempt attempt) => new(
        attempt.Id,
        attempt.TaskSlug,
        attempt.StartedAt,
        attempt.Step,
        attempt.SubmittedAt,
        attempt.IsSubmitted && !attempt.CountsTowardProgress,
        attempt.ContentRevision,
        attempt.Answer is null ? null : AttemptsJson.Read<DiagnosisAnswer>(attempt.Answer),
        attempt.Result is null ? null : AttemptsJson.Read<DiagnosisScore>(attempt.Result));

    /// <summary>The answer as stored: picks by card, each pick's leaves distinct and in order.</summary>
    private static DiagnosisAnswer Normalise(DiagnosisAnswer answer) => new(
    [
        .. answer.Picks
            .OrderBy(pick => pick.Card, StringComparer.Ordinal)
            .Select(pick => new PickedCard(pick.Card, [.. pick.Leaves.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)])),
    ]);

    /// <summary>A card the task does not offer, or a leaf the tree does not have, as a validation failure.</summary>
    private static AppError? Invalid(DiagnosisAnswer answer, TaskForAttempt task)
    {
        var offered = task.OfferedCards.ToHashSet(StringComparer.Ordinal);
        var leaves = task.Leaves.ToHashSet(StringComparer.Ordinal);
        var fields = new Dictionary<string, string[]>(StringComparer.Ordinal);

        for (var index = 0; index < answer.Picks.Count; index++)
        {
            var pick = answer.Picks[index];

            if (!offered.Contains(pick.Card))
            {
                fields[$"picks[{index}].card"] = [$"The task does not offer the card '{pick.Card}'."];
            }

            var unknown = pick.Leaves.Where(leaf => !leaves.Contains(leaf)).ToArray();

            if (unknown.Length > 0)
            {
                fields[$"picks[{index}].leaves"] = [.. unknown.Select(leaf => $"There is no leaf '{leaf}'.")];
            }
        }

        return fields.Count == 0 ? null : AppError.Validation("The answer names cards or leaves this task does not have.", fields);
    }

    private static bool IsFirstSubmissionRace(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: AttemptConfiguration.FirstSubmissionIndex,
        };
}
