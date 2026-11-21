using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Ritocode.Api.Tests.Infrastructure;
using Ritocode.Modules.Attempts.Domain;
using Ritocode.Modules.Attempts.Persistence;

namespace Ritocode.Api.Tests.Endpoints;

/// <summary>
/// The admin area's per-task and per-card statistics (#305), over attempts written directly into a
/// database of the class's own.
/// </summary>
/// <remarks>
/// The medium task: L1 solves it first time (600 s) and practises; L2 misses its finding, picks
/// <c>secrets-in-repo</c> instead (1200 s), reveals the answer, then solves it in practice — gave up;
/// L3 finds it with a wrong leaf (900 s); L4 stopped at step 1 and L5 at step 2. The easy task: five
/// learners miss <c>secrets-in-repo</c> in 300 s each.
/// </remarks>
public sealed class AdminStatsEndpointTests(AdminStatsTestApi api) : IClassFixture<AdminStatsTestApi>
{
    private const string Hardcoded = "hardcoded-config";

    private const string Secrets = "secrets-in-repo";

    [Fact]
    public async Task EachTask_CountsAttemptsLearnersAndWhereOpenOnesStopped()
    {
        await SeedAsync();

        using var report = await GetJsonAsync("/api/v1/admin/stats/tasks");
        var medium = Task(report, ContentTestApi.MediumTask);

        Assert.Equal("Счета для сотни студий", medium.GetProperty("title").GetString());
        Assert.Equal("medium", medium.GetProperty("difficulty").GetString());
        Assert.True(medium.GetProperty("published").GetBoolean());
        Assert.Equal(3, medium.GetProperty("firstAttempts").GetInt32());
        Assert.Equal(2, medium.GetProperty("practiceAttempts").GetInt32());
        Assert.Equal(3, medium.GetProperty("learners").GetInt32());
        Assert.Equal(1, medium.GetProperty("open").GetProperty("diagnosis").GetInt32());
        Assert.Equal(1, medium.GetProperty("open").GetProperty("treatment").GetInt32());
    }

    [Fact]
    public async Task SolvedShare_IsTheTaskListsStatus_SoPracticeAfterTheRevealDoesNotSolveIt()
    {
        await SeedAsync();

        using var report = await GetJsonAsync("/api/v1/admin/stats/tasks");
        var medium = Task(report, ContentTestApi.MediumTask);

        Assert.Equal(1.0 / 3, medium.GetProperty("solvedShare").GetDouble(), 6);
        Assert.Equal(1.0 / 3, medium.GetProperty("revealedShare").GetDouble(), 6);
    }

    [Fact]
    public async Task TimeAndMeanScore_AreOfFirstAttemptsOnly()
    {
        await SeedAsync();

        using var report = await GetJsonAsync("/api/v1/admin/stats/tasks");
        var medium = Task(report, ContentTestApi.MediumTask);

        // 600, 900 and 1200 seconds; the practice attempts (100 and 60) are left out.
        var time = medium.GetProperty("time");
        Assert.Equal(3, time.GetProperty("count").GetInt32());
        Assert.Equal(900, time.GetProperty("medianSeconds").GetInt64());
        Assert.Equal(750, time.GetProperty("lowerQuartileSeconds").GetInt64());
        Assert.Equal(1050, time.GetProperty("upperQuartileSeconds").GetInt64());

        // 30/30, 0/30 and 15/30; the practice attempts scored 30/30.
        Assert.Equal(0.5, medium.GetProperty("meanScoreShare").GetDouble(), 6);

        var band = medium.GetProperty("band");
        Assert.Equal(300, band.GetProperty("maxLines").GetInt32());
        Assert.Equal(10, band.GetProperty("minutesFrom").GetInt32());
        Assert.Equal(15, band.GetProperty("minutesTo").GetInt32());
        Assert.True(medium.GetProperty("lines").GetInt32() > 0);
    }

    [Fact]
    public async Task ATaskSolvedByAlmostNobody_IsMarked_AndOneWithFewLearnersIsNot()
    {
        await SeedAsync();

        using var report = await GetJsonAsync("/api/v1/admin/stats/tasks");
        var easy = Task(report, ContentTestApi.EasyTask);

        Assert.Equal(5, easy.GetProperty("learners").GetInt32());
        Assert.Equal(0, easy.GetProperty("solvedShare").GetDouble());
        Assert.Equal(300, easy.GetProperty("time").GetProperty("medianSeconds").GetInt64());
        Assert.Equal(5, easy.GetProperty("band").GetProperty("minutesTo").GetInt32());
        Assert.True(easy.GetProperty("rarelySolved").GetBoolean());

        // One in three solved it, but three learners are too few to say.
        Assert.False(Task(report, ContentTestApi.MediumTask).GetProperty("rarelySolved").GetBoolean());
    }

    [Fact]
    public async Task EachCard_IsMeasuredOverFirstAttemptsOnly()
    {
        await SeedAsync();

        using var report = await GetJsonAsync("/api/v1/admin/stats/cards");
        Assert.Equal(8, report.RootElement.GetProperty("firstAttempts").GetInt32());

        // L1 found and treated it, L3 found it with a wrong leaf, L2 missed it; practice found it twice more.
        var hardcoded = Card(report, Hardcoded);
        Assert.Equal([3, 2, 1, 0, 1], Figures(hardcoded));
        Assert.False(hardcoded.GetProperty("pickedMoreThanFound").GetBoolean());

        // Missed by all five on the easy task, picked by L2 on the medium one, where the key lacks it.
        var secrets = Card(report, Secrets);
        Assert.Equal([5, 0, 5, 1, 0], Figures(secrets));
        Assert.Equal("Секреты в репозитории", secrets.GetProperty("name").GetString());
        Assert.Equal("hygiene", secrets.GetProperty("class").GetString());
        Assert.True(secrets.GetProperty("pickedMoreThanFound").GetBoolean());
    }

    [Theory]
    [InlineData("task=" + ContentTestApi.EasyTask, 5)]
    [InlineData("task=" + ContentTestApi.MediumTask, 7)]
    [InlineData("card=" + Secrets, 6)]
    [InlineData("card=" + Hardcoded, 5)]
    [InlineData("card=" + Hardcoded + "&task=" + ContentTestApi.EasyTask, 0)]
    public async Task TheAttemptsList_NarrowsToATaskOrACard(string query, int expected)
    {
        await SeedAsync();

        using var page = await GetJsonAsync($"/api/v1/admin/attempts?{query}");

        Assert.Equal(expected, page.RootElement.GetProperty("totalItems").GetInt32());
    }

    /// <summary>Met, found, missed, picked when absent, treated right.</summary>
    private static int[] Figures(JsonElement card) =>
    [
        card.GetProperty("met").GetInt32(),
        card.GetProperty("found").GetInt32(),
        card.GetProperty("missed").GetInt32(),
        card.GetProperty("pickedWhenAbsent").GetInt32(),
        card.GetProperty("treatedRight").GetInt32(),
    ];

    private static JsonElement Task(JsonDocument report, string slug) =>
        report.RootElement.GetProperty("tasks").EnumerateArray().Single(task => task.GetProperty("task").GetString() == slug);

    private static JsonElement Card(JsonDocument report, string slug) =>
        report.RootElement.GetProperty("cards").EnumerateArray().Single(card => card.GetProperty("card").GetString() == slug);

    private static readonly SemaphoreSlim Seeding = new(1, 1);
    private static bool seeded;

    private async Task SeedAsync()
    {
        await Seeding.WaitAsync(TestContext.Current.CancellationToken);
        try
        {
            if (seeded)
            {
                return;
            }

            var start = new DateTimeOffset(2011, 5, 2, 9, 0, 0, TimeSpan.Zero);
            Guid[] learner = [.. Enumerable.Range(0, 10).Select(index => Guid.CreateVersion7(start.AddSeconds(index)))];

            var found = Line(Hardcoded, "found", 30, treated: true);
            var wrongLeaf = Line(Hardcoded, "found", 15, treated: false);
            var missed = Line(Hardcoded, "missed", 0);
            var extra = Line(Secrets, "extra", 0);

            var reachedTreatment = Attempt.Start(learner[4], ContentTestApi.MediumTask, start);
            reachedTreatment.Reach(AttemptStep.Treatment);

            var l2First = Submitted(learner[1], ContentTestApi.MediumTask, start, 1200, 0, first: true, missed, extra);

            List<Attempt> attempts =
            [
                Submitted(learner[0], ContentTestApi.MediumTask, start, 600, 30, first: true, found),
                Submitted(learner[0], ContentTestApi.MediumTask, start.AddHours(1), 100, 30, first: false, found),
                l2First,
                Submitted(learner[1], ContentTestApi.MediumTask, start.AddHours(2), 60, 30, first: false, found),
                Submitted(learner[2], ContentTestApi.MediumTask, start, 900, 15, first: true, wrongLeaf),
                Attempt.Start(learner[3], ContentTestApi.MediumTask, start),
                reachedTreatment,
                .. learner.Skip(5).Select(id => Submitted(id, ContentTestApi.EasyTask, start, 300, 0, first: true, Line(Secrets, "missed", 0))),
            ];

            await using var scope = api.Services.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<AttemptsDbContext>();
            context.Attempts.AddRange(attempts);
            context.Reveals.Add(Reveal.Of(l2First, start.AddHours(1)));
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);

            seeded = true;
        }
        finally
        {
            Seeding.Release();
        }
    }

    private static string Line(string card, string outcome, int points, bool? treated = null)
    {
        var treatment = treated is { } matched
            ? $$""","treatment":{"matched":{{(matched ? "true" : "false")}},"matchedLeaves":[],"wrongLeaves":[]}"""
            : string.Empty;

        return $$"""{"card":"{{card}}","outcome":"{{outcome}}","points":{{points}},"keyLeaves":null{{treatment}}}""";
    }

    private static Attempt Submitted(Guid userId, string task, DateTimeOffset started, int seconds, int score, bool first, params string[] lines)
    {
        var attempt = Attempt.Start(userId, task, started);
        attempt.Submit(
            started.AddSeconds(seconds),
            "first",
            "{\"picks\":[]}",
            $$"""{"total":{{score}},"maximum":30,"isCorrect":false,"cards":[{{string.Join(",", lines)}}]}""",
            "{}",
            score,
            30,
            countsTowardProgress: first);

        return attempt;
    }

    private async Task<JsonDocument> GetJsonAsync(string path)
    {
        var response = await api.Client.GetAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken);
        var text = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"Expected 200, got {(int)response.StatusCode}: {text}");
        return JsonDocument.Parse(text);
    }
}
