using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Ritocode.Api.Tests.Infrastructure;
using Ritocode.Modules.Attempts.Domain;
using Ritocode.Modules.Attempts.Persistence;
using Ritocode.Modules.Users.Domain;
using Ritocode.Modules.Users.Persistence;

namespace Ritocode.Api.Tests.Endpoints;

/// <summary>
/// The admin area's usage over time (#304) for an admin. The rows are written directly, in March 2010, so
/// no other test's rows, all written now, fall into the range.
/// </summary>
public sealed class AdminActivityEndpointTests(AdminContentTestApi api) : IClassFixture<AdminContentTestApi>
{
    private const string Range = "from=2010-03-01&to=2010-03-14";

    [Fact]
    public async Task EachDay_CountsWhatHappenedOnIt_InUtcDays_AndTheSummaryLooksBackFromTheLastDay()
    {
        await SeedAsync();

        using var report = await GetJsonAsync($"/api/v1/admin/stats/activity?{Range}", HttpStatusCode.OK);
        var root = report.RootElement;
        Assert.Equal("day", root.GetProperty("bucket").GetString());

        var days = root.GetProperty("buckets").EnumerateArray().ToList();
        Assert.Equal(14, days.Count);
        Assert.Equal("2010-03-01", days[0].GetProperty("start").GetString());
        Assert.Equal("2010-03-14", days[13].GetProperty("end").GetString());

        // A: signed up, started, sent a signal, submitted and revealed on the 1st.
        Assert.Equal([1, 1, 1, 1, 1, 1, 0], Figures(days[0]));

        // B: signed up and started late on the 2nd, submitted after midnight UTC.
        Assert.Equal([0, 1, 1, 0, 0, 0, 0], Figures(days[1]));
        Assert.Equal([1, 0, 0, 1, 0, 0, 0], Figures(days[2]));

        // Marks: B on the 8th, A on the 9th; nothing on the other days.
        Assert.Equal([1, 0, 0, 0, 0, 0, 1], Figures(days[7]));
        Assert.Equal([1, 0, 0, 0, 0, 0, 1], Figures(days[8]));
        Assert.All(days.Skip(9), day => Assert.All(Figures(day), figure => Assert.Equal(0, figure)));

        var summary = root.GetProperty("summary");
        Assert.Equal(0, summary.GetProperty("dailyActive").GetInt32());
        Assert.Equal(2, summary.GetProperty("weeklyActive").GetInt32());

        // C, active only on 25 February, before the range, counts in the 30 days back from the 14th.
        Assert.Equal(3, summary.GetProperty("monthlyActive").GetInt32());

        // A came back 8 days after the 1st; B 5 days after the 3rd, which is not a week.
        Assert.Equal(2, summary.GetProperty("learners").GetInt32());
        Assert.Equal(1, summary.GetProperty("returnedAfterWeek").GetInt32());
    }

    [Fact]
    public async Task AWeekBucket_IsSevenDaysFromTheFirst()
    {
        await SeedAsync();

        using var report = await GetJsonAsync($"/api/v1/admin/stats/activity?{Range}&bucket=Week", HttpStatusCode.OK);
        var weeks = report.RootElement.GetProperty("buckets").EnumerateArray().ToList();

        Assert.Equal("week", report.RootElement.GetProperty("bucket").GetString());
        Assert.Equal(2, weeks.Count);
        Assert.Equal("2010-03-07", weeks[0].GetProperty("end").GetString());
        Assert.Equal([2, 2, 2, 2, 1, 1, 0], Figures(weeks[0]));
        Assert.Equal([2, 0, 0, 0, 0, 0, 2], Figures(weeks[1]));
    }

    [Fact]
    public async Task ARangeWithNothingInIt_HasEveryBucketAtZero()
    {
        using var report = await GetJsonAsync("/api/v1/admin/stats/activity?from=2009-01-01&to=2009-01-07", HttpStatusCode.OK);

        var days = report.RootElement.GetProperty("buckets").EnumerateArray().ToList();
        Assert.Equal(7, days.Count);
        Assert.All(days, day => Assert.All(Figures(day), figure => Assert.Equal(0, figure)));
        Assert.Equal(0, report.RootElement.GetProperty("summary").GetProperty("monthlyActive").GetInt32());
    }

    [Fact]
    public async Task NoRange_IsTheLast30Days()
    {
        using var report = await GetJsonAsync("/api/v1/admin/stats/activity", HttpStatusCode.OK);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        Assert.Equal(today.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), report.RootElement.GetProperty("to").GetString());
        Assert.Equal(30, report.RootElement.GetProperty("buckets").GetArrayLength());
    }

    [Theory]
    [InlineData("from=2010-03-14&to=2010-03-01", "from")]
    [InlineData("from=2009-01-01&to=2010-03-01", "from")]
    [InlineData("from=yesterday", "from")]
    [InlineData("to=2010-13-01", "to")]
    [InlineData("bucket=month", "bucket")]
    public async Task ABadRange_IsAValidationError_NamingTheParameter(string query, string parameter)
    {
        using var problem = await GetJsonAsync($"/api/v1/admin/stats/activity?{query}", HttpStatusCode.BadRequest);

        Assert.True(problem.RootElement.GetProperty("errors").TryGetProperty(parameter, out _));
    }

    /// <summary>The figures of a bucket, in the order the page shows them.</summary>
    private static int[] Figures(JsonElement bucket) =>
    [
        bucket.GetProperty("activeLearners").GetInt32(),
        bucket.GetProperty("newUsers").GetInt32(),
        bucket.GetProperty("attemptsStarted").GetInt32(),
        bucket.GetProperty("attemptsSubmitted").GetInt32(),
        bucket.GetProperty("answersRevealed").GetInt32(),
        bucket.GetProperty("signalsSent").GetInt32(),
        bucket.GetProperty("cardsStudied").GetInt32(),
    ];

    private static readonly SemaphoreSlim Seeding = new(1, 1);
    private static bool seeded;

    /// <summary>
    /// Learner A, active on 1 and 9 March and in February; learner B, active on 3 and 8 March; and C, who was
    /// active only in February, before the range. Written once for the class.
    /// </summary>
    private async Task SeedAsync()
    {
        await Seeding.WaitAsync(TestContext.Current.CancellationToken);
        try
        {
            if (seeded)
            {
                return;
            }

            static DateTimeOffset At(int month, int day, int hour, int minute = 0) => new(2010, month, day, hour, minute, 0, TimeSpan.Zero);

            var a = User.Create("activity-a@example.test", "activity-a", At(3, 1, 10));
            var b = User.Create("activity-b@example.test", "activity-b", At(3, 2, 23, 40));

            await using var scope = api.Services.CreateAsyncScope();
            var users = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
            users.Users.AddRange(a, b);
            await users.SaveChangesAsync(TestContext.Current.CancellationToken);

            var february = Submitted(a.Id, At(2, 20, 9), At(2, 20, 9, 30), ContentTestApi.MediumTask);
            var c = Submitted(Guid.CreateVersion7(), At(2, 25, 9), At(2, 25, 9, 30));
            var first = Submitted(a.Id, At(3, 1, 10, 5), At(3, 1, 10, 20));
            var late = Submitted(b.Id, At(3, 2, 23, 50), At(3, 3, 0, 10));

            var context = scope.ServiceProvider.GetRequiredService<AttemptsDbContext>();
            context.Attempts.AddRange(february, c, first, late);
            context.Signals.Add(Signal.Send(first, "secrets-in-repo", null, At(3, 1, 10, 25)));
            context.Reveals.Add(Reveal.Of(first, At(3, 1, 10, 30)));
            context.StudyMarks.Add(StudyMark.Of(b.Id, "secrets-in-repo", At(3, 8, 12)));
            context.StudyMarks.Add(StudyMark.Of(a.Id, "secrets-in-repo", At(3, 9, 12)));
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);

            seeded = true;
        }
        finally
        {
            Seeding.Release();
        }
    }

    private static Attempt Submitted(Guid userId, DateTimeOffset started, DateTimeOffset submitted, string task = ContentTestApi.EasyTask)
    {
        var attempt = Attempt.Start(userId, task, started);
        attempt.Submit(
            submitted,
            "first",
            "{\"picks\":[]}",
            """{"total":0,"maximum":30,"isCorrect":false,"cards":[]}""",
            "{}",
            0,
            30,
            countsTowardProgress: true);

        return attempt;
    }

    private async Task<JsonDocument> GetJsonAsync(string path, HttpStatusCode expected)
    {
        var response = await api.Client.GetAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken);
        var text = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.True(expected == response.StatusCode, $"Expected {expected}, got {(int)response.StatusCode}: {text}");
        return JsonDocument.Parse(text);
    }
}
