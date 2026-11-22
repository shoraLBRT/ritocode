namespace Ritocode.Modules.Attempts.Admin;

// The shapes the admin area's per-task and per-card statistics answer with (#305, SPEC §3.4, §5, §8).
// Each is a report over every task or card, not a collection of resources, so it is not paged: the
// page sorts it whole.

/// <summary>Every task the product knows, and every task slug an attempt names.</summary>
public sealed record TaskStatsReport(IReadOnlyList<TaskStatsView> Tasks);

/// <summary>
/// One task across every learner (#305). Shares are of <paramref name="Learners"/>, and null when it is 0.
/// </summary>
/// <param name="Title">The title in the default locale, or the slug when Content does not know the task.</param>
/// <param name="Difficulty"><c>easy</c>, <c>medium</c> or <c>hard</c>; null when Content does not know the task.</param>
/// <param name="Lines">The material's total lines; null when Content does not know the task.</param>
/// <param name="Band">The difficulty's size band; null when Content does not know the task.</param>
/// <param name="FirstAttempts">Submitted attempts that count toward progress — one per learner.</param>
/// <param name="PracticeAttempts">Submitted attempts after the first.</param>
/// <param name="Learners">Learners with a submitted attempt at the task (equal to <paramref name="FirstAttempts"/>).</param>
/// <param name="SolvedShare">Of those learners, the share whose status is solved, as the task list counts it.</param>
/// <param name="RevealedShare">Of those learners, the share who revealed the answer.</param>
/// <param name="Time">From start to submit over first attempts; null without one.</param>
/// <param name="Open">Attempts never submitted, by the step they stopped at.</param>
/// <param name="MeanScoreShare">The mean of score over maximum across first attempts, 0 to 1; null without one.</param>
/// <param name="RarelySolved">At least <see cref="StatsReader.OutlierMinLearners"/> learners and a solved share below <see cref="StatsReader.RarelySolvedShare"/>.</param>
public sealed record TaskStatsView(
    string Task,
    string Title,
    string? Difficulty,
    bool Published,
    int? Lines,
    SizeBandView? Band,
    int FirstAttempts,
    int PracticeAttempts,
    int Learners,
    double? SolvedShare,
    double? RevealedShare,
    TimeSpreadView? Time,
    OpenAttemptsView Open,
    double? MeanScoreShare,
    bool RarelySolved);

/// <summary>A difficulty's size band (SPEC §3.4): the most lines, and the estimate of the whole task in minutes.</summary>
public sealed record SizeBandView(int MaxLines, int MinutesFrom, int MinutesTo);

/// <summary>
/// Times from start to submit in whole seconds: the median and the quartiles, the spread being the
/// range between them. Quartiles interpolate linearly between the sorted times.
/// </summary>
public sealed record TimeSpreadView(int Count, long MedianSeconds, long LowerQuartileSeconds, long UpperQuartileSeconds);

/// <summary>Open attempts by the furthest step reached — where learners stopped.</summary>
public sealed record OpenAttemptsView(int Diagnosis, int Treatment);

/// <summary>Every card met or picked in a first attempt.</summary>
public sealed record CardStatsReport(int FirstAttempts, IReadOnlyList<CardStatsView> Cards);

/// <summary>
/// One card across every learner's first attempts (#305) — the learner's per-card progress measures
/// (<c>ProgressCalculator</c>), for everyone and every task, revealed or not.
/// </summary>
/// <param name="Class">The card's class; null when Content no longer knows the card.</param>
/// <param name="Met">First attempts whose key lists the card.</param>
/// <param name="Found">Of those, the ones that picked it.</param>
/// <param name="Missed">Of those, the ones that did not.</param>
/// <param name="PickedWhenAbsent">First attempts that picked it where the key does not list it.</param>
/// <param name="TreatedRight">Found, and treated with a leaf the key lists.</param>
/// <param name="PickedMoreThanFound">Picked when absent more often than found.</param>
public sealed record CardStatsView(
    string Card,
    string Name,
    string? Class,
    string? ClassName,
    int Met,
    int Found,
    int Missed,
    int PickedWhenAbsent,
    int TreatedRight,
    bool PickedMoreThanFound);
