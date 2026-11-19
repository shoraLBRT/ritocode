using Ritocode.Shared.Contracts.Attempts;
using Ritocode.Shared.Contracts.Users;

namespace Ritocode.Api.Admin;

/// <summary>
/// Usage over time (#304), computed on read from what the Attempts and Users modules answer. Days are UTC
/// days; a week bucket is seven days counted from the range's first, so the last one may be shorter.
/// </summary>
public sealed class ActivityReader(ILearnerActivityLookup activity, IUserSignupLookup signups)
{
    /// <summary>The days a learner must come back after their first active day in the range to count as returning.</summary>
    public const int ReturnAfterDays = 7;

    public async Task<ActivityReport> ReadAsync(DateOnly from, DateOnly to, int bucketDays, CancellationToken cancellationToken)
    {
        // Monthly active learners look back 30 days from the range's last, which may start before its first.
        var lookBack = to.AddDays(-29) < from ? to.AddDays(-29) : from;
        var moments = await activity.ReadAsync(Start(lookBack), Start(to.AddDays(1)), cancellationToken).ConfigureAwait(false);
        var created = await signups.SignupsAsync(Start(from), Start(to.AddDays(1)), cancellationToken).ConfigureAwait(false);

        return Compose(from, to, bucketDays, moments, created);
    }

    /// <summary>The report of <paramref name="moments"/> and <paramref name="created"/>, which may reach before <paramref name="from"/>.</summary>
    public static ActivityReport Compose(
        DateOnly from,
        DateOnly to,
        int bucketDays,
        LearnerActivity moments,
        IReadOnlyList<DateTimeOffset> created)
    {
        ArgumentNullException.ThrowIfNull(moments);
        ArgumentNullException.ThrowIfNull(created);

        // A learner's active days: a submitted attempt or a study mark, as the streak counts.
        var active = moments.AttemptsSubmitted.Concat(moments.CardsStudied)
            .Select(moment => (moment.UserId, Day: Day(moment.At)))
            .Distinct()
            .ToList();

        var buckets = new List<ActivityBucket>();
        for (var start = from; start <= to; start = start.AddDays(bucketDays))
        {
            var bucketStart = start;
            var end = start.AddDays(bucketDays - 1) < to ? start.AddDays(bucketDays - 1) : to;
            bool In(DateOnly day) => day >= bucketStart && day <= end;
            int Count(IReadOnlyList<LearnerMoment> list) => list.Count(moment => In(Day(moment.At)));

            buckets.Add(new ActivityBucket(
                start,
                end,
                active.Where(pair => In(pair.Day)).Select(pair => pair.UserId).Distinct().Count(),
                created.Count(moment => In(Day(moment))),
                Count(moments.AttemptsStarted),
                Count(moments.AttemptsSubmitted),
                Count(moments.AnswersRevealed),
                Count(moments.SignalsSent),
                Count(moments.CardsStudied)));
        }

        int ActiveWithin(int days) =>
            active.Where(pair => pair.Day > to.AddDays(-days) && pair.Day <= to).Select(pair => pair.UserId).Distinct().Count();

        var inRange = active.Where(pair => pair.Day >= from && pair.Day <= to)
            .GroupBy(pair => pair.UserId)
            .Select(days => (First: days.Min(pair => pair.Day), Last: days.Max(pair => pair.Day)))
            .ToList();

        var summary = new ActivitySummary(
            ActiveWithin(1),
            ActiveWithin(7),
            ActiveWithin(30),
            inRange.Count,
            inRange.Count(learner => learner.Last.DayNumber - learner.First.DayNumber >= ReturnAfterDays));

        return new ActivityReport(from, to, bucketDays == 7 ? "week" : "day", buckets, summary);
    }

    private static DateTimeOffset Start(DateOnly day) => new(day.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

    private static DateOnly Day(DateTimeOffset moment) => DateOnly.FromDateTime(moment.UtcDateTime);
}

/// <summary>The answer of <c>GET /admin/stats/activity</c>: the range, its buckets and figures over it.</summary>
public sealed record ActivityReport(DateOnly From, DateOnly To, string Bucket, IReadOnlyList<ActivityBucket> Buckets, ActivitySummary Summary);

/// <summary>One bucket, from its start to its end (UTC days, both included).</summary>
public sealed record ActivityBucket(
    DateOnly Start,
    DateOnly End,
    int ActiveLearners,
    int NewUsers,
    int AttemptsStarted,
    int AttemptsSubmitted,
    int AnswersRevealed,
    int SignalsSent,
    int CardsStudied);

/// <summary>
/// Learners active on the range's last day, in its last 7 and last 30 days; the learners active in the range,
/// and how many of them were active again at least a week after their first active day in it.
/// </summary>
public sealed record ActivitySummary(int DailyActive, int WeeklyActive, int MonthlyActive, int Learners, int ReturnedAfterWeek);
