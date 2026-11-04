using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Ritocode.Api.Tests.Infrastructure;
using Ritocode.Modules.Attempts.Domain;
using Ritocode.Modules.Attempts.Persistence;

namespace Ritocode.Api.Tests.Endpoints;

/// <summary>
/// Start, step, submit, read and history (docs/SPEC.md §5.4, §9.3) through the real composition root,
/// against a real PostgreSQL holding the reference content. The reference task's key: secrets
/// (weight 3, <c>auto.secrets</c> or <c>rule.conventions</c>), money in a float (3,
/// <c>manual.representation</c>) and a swallowed error (2, <c>manual.handle-errors</c>) — 120 at most.
/// </summary>
public sealed class AttemptEndpointsTests(ContentTestApi api) : IClassFixture<ContentTestApi>
{
    // Secrets found and treated (+45), money found and mistreated (+30 −2), an extra card (−3), the
    // swallowed error missed (−6).
    private static readonly object Answer = new
    {
        picks = new object[]
        {
            new { card = "money-in-float", leaves = new[] { "manual.split" } },
            new { card = "secrets-in-repo", leaves = new[] { "auto.secrets" } },
            new { card = "hardcoded-config", leaves = new[] { "manual.extract-config" } },
        },
    };

    [Fact]
    public async Task Starting_CreatesAnOpenAttempt_WithoutTheKey()
    {
        var response = await PostAsync("/api/v1/attempts", new { task = ContentTestApi.EasyTask });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var started = await JsonAsync(response);
        var id = started.RootElement.GetProperty("id").GetString();
        Assert.EndsWith($"/api/v1/attempts/{id}", response.Headers.Location!.ToString(), StringComparison.Ordinal);

        Assert.Equal(ContentTestApi.EasyTask, started.RootElement.GetProperty("task").GetString());
        Assert.Equal("diagnosis", started.RootElement.GetProperty("step").GetString());
        Assert.Equal(JsonValueKind.Null, started.RootElement.GetProperty("submittedAt").ValueKind);
        Assert.Equal(JsonValueKind.Null, started.RootElement.GetProperty("result").ValueKind);

        // UTC with an explicit Z (ADR 0003), and the same instant written as read back.
        var startedAt = started.RootElement.GetProperty("startedAt").GetString();
        Assert.EndsWith("Z", startedAt, StringComparison.Ordinal);

        using var read = await GetJsonAsync($"/api/v1/attempts/{id}", HttpStatusCode.OK);
        AssertNoKey(read.RootElement.GetRawText());
        Assert.Equal(startedAt, read.RootElement.GetProperty("startedAt").GetString());
    }

    [Fact]
    public async Task AnUnknownTask_CannotBeStarted()
    {
        var response = await PostAsync("/api/v1/attempts", new { task = "no-such-task" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using var body = await JsonAsync(response);
        Assert.Equal("task_not_found", body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task AStartWithoutATask_IsAValidationError()
    {
        var response = await PostAsync("/api/v1/attempts", new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var body = await JsonAsync(response);
        Assert.True(body.RootElement.GetProperty("errors").TryGetProperty("task", out _));
    }

    [Fact]
    public async Task TheStepReached_MovesForward_AndNeverBack()
    {
        var id = await StartAsync(ContentTestApi.EasyTask);

        using (var forward = await JsonAsync(await PatchAsync($"/api/v1/attempts/{id}", new { step = "treatment" }), HttpStatusCode.OK))
        {
            Assert.Equal("treatment", forward.RootElement.GetProperty("step").GetString());
        }

        using (var back = await JsonAsync(await PatchAsync($"/api/v1/attempts/{id}", new { step = "diagnosis" }), HttpStatusCode.OK))
        {
            Assert.Equal("treatment", back.RootElement.GetProperty("step").GetString());
        }

        using var invalid = await JsonAsync(await PatchAsync($"/api/v1/attempts/{id}", new { step = "review" }), HttpStatusCode.BadRequest);
        Assert.True(invalid.RootElement.GetProperty("errors").TryGetProperty("step", out _));
    }

    [Fact]
    public async Task Submitting_ScoresTheAnswer_AndRevealsTheKey()
    {
        var id = await StartAsync(ContentTestApi.EasyTask);

        using var submitted = await JsonAsync(await PostAsync($"/api/v1/attempts/{id}/submit", Answer), HttpStatusCode.OK);
        var root = submitted.RootElement;

        Assert.NotEqual(JsonValueKind.Null, root.GetProperty("submittedAt").ValueKind);
        Assert.Equal("first", root.GetProperty("contentRevision").GetString());
        Assert.Equal("treatment", root.GetProperty("step").GetString());

        var result = root.GetProperty("result");
        Assert.Equal(64, result.GetProperty("total").GetInt32());
        Assert.Equal(120, result.GetProperty("maximum").GetInt32());
        Assert.False(result.GetProperty("isCorrect").GetBoolean());

        var lines = result.GetProperty("cards").EnumerateArray()
            .Select(line => (line.GetProperty("card").GetString(), line.GetProperty("outcome").GetString(), line.GetProperty("points").GetInt32()))
            .ToList();
        Assert.Equal(
            [
                ("secrets-in-repo", "found", 45),
                ("money-in-float", "found", 28),
                ("swallowed-error", "missed", -6),
                ("hardcoded-config", "extra", -3),
            ],
            lines);

        // The key of a missed card is revealed too.
        var missed = result.GetProperty("cards")[2];
        Assert.Equal(["manual.handle-errors"], missed.GetProperty("keyLeaves").EnumerateArray().Select(leaf => leaf.GetString()));

        // The author's words come with the key: the note for a finding of this key, and the lesson.
        var review = root.GetProperty("review");
        Assert.StartsWith("Десять счетов в месяц", review.GetProperty("notes").GetProperty("money-in-float").GetString(), StringComparison.Ordinal);
        Assert.StartsWith("Масштаб маленький", review.GetProperty("lesson").GetString(), StringComparison.Ordinal);

        // The answer is stored as sent, in order: picks by card.
        Assert.Equal(
            ["hardcoded-config", "money-in-float", "secrets-in-repo"],
            root.GetProperty("answer").GetProperty("picks").EnumerateArray().Select(pick => pick.GetProperty("card").GetString()));
    }

    [Fact]
    public async Task ASubmittedAttempt_CannotBeSubmittedOrMovedAgain()
    {
        var id = await StartAsync(ContentTestApi.EasyTask);
        (await PostAsync($"/api/v1/attempts/{id}/submit", Answer)).EnsureSuccessStatusCode();

        using var again = await JsonAsync(await PostAsync($"/api/v1/attempts/{id}/submit", Answer), HttpStatusCode.Conflict);
        Assert.Equal("attempt_already_submitted", again.RootElement.GetProperty("code").GetString());

        using var step = await JsonAsync(await PatchAsync($"/api/v1/attempts/{id}", new { step = "treatment" }), HttpStatusCode.Conflict);
        Assert.Equal("attempt_already_submitted", step.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task TheFirstSubmittedAttempt_Counts_AndLaterOnesArePractice()
    {
        var first = await StartAsync(ContentTestApi.MediumTask);
        var second = await StartAsync(ContentTestApi.MediumTask);
        var answer = new { picks = new[] { new { card = "hardcoded-config", leaves = new[] { "manual.extract-config" } } } };

        // The one submitted first counts, whichever was started first.
        using (var submitted = await JsonAsync(await PostAsync($"/api/v1/attempts/{second}/submit", answer), HttpStatusCode.OK))
        {
            Assert.False(submitted.RootElement.GetProperty("practice").GetBoolean());
            Assert.True(submitted.RootElement.GetProperty("result").GetProperty("isCorrect").GetBoolean());
        }

        using (var submitted = await JsonAsync(await PostAsync($"/api/v1/attempts/{first}/submit", answer), HttpStatusCode.OK))
        {
            Assert.True(submitted.RootElement.GetProperty("practice").GetBoolean());
        }

        using var history = await GetJsonAsync($"/api/v1/attempts?task={ContentTestApi.MediumTask}", HttpStatusCode.OK);
        var items = history.RootElement.GetProperty("items").EnumerateArray()
            .Select(item => (item.GetProperty("id").GetString(), item.GetProperty("practice").GetBoolean(), item.GetProperty("score").GetInt32()))
            .ToList();

        // Newest started first.
        Assert.Equal([(second.ToString(), false, 15), (first.ToString(), true, 15)], items);
        Assert.Equal(2, history.RootElement.GetProperty("totalItems").GetInt32());
    }

    [Fact]
    public async Task AnAnswer_NamingACardTheTaskDoesNotOffer_OrALeafThatDoesNotExist_IsRefused()
    {
        var id = await StartAsync(ContentTestApi.EasyTask);
        var answer = new
        {
            picks = new object[]
            {
                new { card = "secrets-in-repo", leaves = new[] { "auto.secrets" } },
                new { card = "no-such-card", leaves = new[] { "auto.nothing" } },
            },
        };

        using var refused = await JsonAsync(await PostAsync($"/api/v1/attempts/{id}/submit", answer), HttpStatusCode.BadRequest);
        var errors = refused.RootElement.GetProperty("errors");

        Assert.True(errors.TryGetProperty("picks[1].card", out _));
        Assert.True(errors.TryGetProperty("picks[1].leaves", out _));
        Assert.False(errors.TryGetProperty("picks[0].card", out _));

        // Nothing was stored: the attempt is still open.
        using var read = await GetJsonAsync($"/api/v1/attempts/{id}", HttpStatusCode.OK);
        Assert.Equal(JsonValueKind.Null, read.RootElement.GetProperty("submittedAt").ValueKind);
    }

    [Fact]
    public async Task APickedCard_WithoutALeaf_OrPickedTwice_IsAValidationError()
    {
        var id = await StartAsync(ContentTestApi.EasyTask);

        var bare = new { picks = new[] { new { card = "secrets-in-repo", leaves = Array.Empty<string>() } } };
        using var noLeaf = await JsonAsync(await PostAsync($"/api/v1/attempts/{id}/submit", bare), HttpStatusCode.BadRequest);
        Assert.True(noLeaf.RootElement.GetProperty("errors").TryGetProperty("picks[0].leaves", out _));

        var twice = new
        {
            picks = new[]
            {
                new { card = "secrets-in-repo", leaves = new[] { "auto.secrets" } },
                new { card = "secrets-in-repo", leaves = new[] { "rule.conventions" } },
            },
        };
        using var repeated = await JsonAsync(await PostAsync($"/api/v1/attempts/{id}/submit", twice), HttpStatusCode.BadRequest);
        Assert.True(repeated.RootElement.GetProperty("errors").TryGetProperty("picks", out _));

        // A leaf longer than any identifier the tree can hold is refused by its shape, before the task is read.
        var long_ = new { picks = new[] { new { card = "secrets-in-repo", leaves = new[] { new string('a', 200) } } } };
        using var tooLong = await JsonAsync(await PostAsync($"/api/v1/attempts/{id}/submit", long_), HttpStatusCode.BadRequest);
        Assert.True(tooLong.RootElement.GetProperty("errors").TryGetProperty("picks[0].leaves[0]", out _));
    }

    [Fact]
    public async Task NothingPickedOnATaskWithFindings_IsAnAnswer_ThatMissesEverything()
    {
        var id = await StartAsync(ContentTestApi.EasyTask);

        using var submitted = await JsonAsync(await PostAsync($"/api/v1/attempts/{id}/submit", new { picks = Array.Empty<object>() }), HttpStatusCode.OK);

        Assert.Equal(0, submitted.RootElement.GetProperty("result").GetProperty("total").GetInt32());
        Assert.All(
            submitted.RootElement.GetProperty("result").GetProperty("cards").EnumerateArray(),
            line => Assert.Equal("missed", line.GetProperty("outcome").GetString()));
    }

    [Fact]
    public async Task AnotherUsersAttempt_AnswersExactlyAsAMissingOne()
    {
        var theirs = Attempt.Start(Guid.CreateVersion7(), ContentTestApi.EasyTask, DateTimeOffset.UtcNow);

        await using (var scope = api.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AttemptsDbContext>();
            context.Attempts.Add(theirs);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using var missing = await GetJsonAsync($"/api/v1/attempts/{Guid.CreateVersion7()}", HttpStatusCode.NotFound);
        using var other = await GetJsonAsync($"/api/v1/attempts/{theirs.Id}", HttpStatusCode.NotFound);
        using var malformed = await GetJsonAsync("/api/v1/attempts/not-an-id", HttpStatusCode.NotFound);

        Assert.Equal("attempt_not_found", missing.RootElement.GetProperty("code").GetString());
        Assert.Equal(missing.RootElement.GetProperty("code").GetString(), other.RootElement.GetProperty("code").GetString());
        Assert.Equal(missing.RootElement.GetProperty("detail").GetString(), other.RootElement.GetProperty("detail").GetString());
        Assert.Equal("attempt_not_found", malformed.RootElement.GetProperty("code").GetString());

        Assert.Equal(HttpStatusCode.NotFound, (await PatchAsync($"/api/v1/attempts/{theirs.Id}", new { step = "treatment" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await PostAsync($"/api/v1/attempts/{theirs.Id}/submit", Answer)).StatusCode);

        using var history = await GetJsonAsync("/api/v1/attempts?pageSize=100", HttpStatusCode.OK);
        Assert.DoesNotContain(
            history.RootElement.GetProperty("items").EnumerateArray(),
            item => item.GetProperty("id").GetString() == theirs.Id.ToString());
    }

    [Fact]
    public async Task TheKey_NeverLeavesTheServer_ExceptInASubmittedResult()
    {
        var id = await StartAsync(ContentTestApi.EasyTask);

        using (var task = await GetJsonAsync($"/api/v1/tasks/{ContentTestApi.EasyTask}", HttpStatusCode.OK))
        {
            AssertNoKey(task.RootElement.GetRawText());
        }

        using (var open = await GetJsonAsync($"/api/v1/attempts/{id}", HttpStatusCode.OK))
        {
            AssertNoKey(open.RootElement.GetRawText());
            Assert.Equal(JsonValueKind.Null, open.RootElement.GetProperty("review").ValueKind);
        }

        using (var history = await GetJsonAsync("/api/v1/attempts", HttpStatusCode.OK))
        {
            AssertNoKey(history.RootElement.GetRawText());
        }

        (await PostAsync($"/api/v1/attempts/{id}/submit", Answer)).EnsureSuccessStatusCode();

        using var submitted = await GetJsonAsync($"/api/v1/attempts/{id}", HttpStatusCode.OK);
        Assert.Contains("keyLeaves", submitted.RootElement.GetRawText(), StringComparison.Ordinal);
    }

    private static void AssertNoKey(string json)
    {
        foreach (var leaked in new[] { "keyLeaves", "manual.handle-errors", "manual.representation", "weight" })
        {
            Assert.DoesNotContain(leaked, json, StringComparison.OrdinalIgnoreCase);
        }
    }

    private async Task<Guid> StartAsync(string task)
    {
        var response = await PostAsync("/api/v1/attempts", new { task });
        using var body = await JsonAsync(response, HttpStatusCode.Created);
        return body.RootElement.GetProperty("id").GetGuid();
    }

    private Task<HttpResponseMessage> PostAsync(string path, object body) =>
        api.Client.PostAsJsonAsync(new Uri(path, UriKind.Relative), body, TestContext.Current.CancellationToken);

    private Task<HttpResponseMessage> PatchAsync(string path, object body) =>
        api.Client.PatchAsJsonAsync(new Uri(path, UriKind.Relative), body, TestContext.Current.CancellationToken);

    private async Task<JsonDocument> GetJsonAsync(string path, HttpStatusCode expected) =>
        await JsonAsync(await api.Client.GetAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken), expected);

    private static async Task<JsonDocument> JsonAsync(HttpResponseMessage response, HttpStatusCode? expected = null)
    {
        var text = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        if (expected is not null)
        {
            Assert.True(expected == response.StatusCode, $"Expected {expected}, got {(int)response.StatusCode}: {text}");
        }

        return JsonDocument.Parse(text);
    }
}

/// <summary>What happens to an attempt, and to the catalogue, as content and submits change around it.</summary>
public sealed class AttemptHistoryTests(ContentTestApi api) : IClassFixture<ContentTestApi>
{
    [Fact]
    public async Task ReIngestingChangedContent_LeavesAStoredResultByteForByteUnchanged_AndTheCatalogueSaysSolved()
    {
        using (var before = await GetJsonAsync("/api/v1/tasks"))
        {
            Assert.All(before.RootElement.GetProperty("items").EnumerateArray(), task => Assert.False(task.GetProperty("solved").GetBoolean()));
        }

        var start = await api.Client.PostAsJsonAsync(new Uri("/api/v1/attempts", UriKind.Relative), new { task = ContentTestApi.EasyTask }, TestContext.Current.CancellationToken);
        var id = (await start.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetProperty("id").GetString();
        var answer = new { picks = new[] { new { card = "secrets-in-repo", leaves = new[] { "auto.secrets" } } } };
        (await api.Client.PostAsJsonAsync(new Uri($"/api/v1/attempts/{id}/submit", UriKind.Relative), answer, TestContext.Current.CancellationToken)).EnsureSuccessStatusCode();

        var stored = await GetRawAsync($"/api/v1/attempts/{id}");

        // The key, a weight and the task's text all change; the task stays published.
        api.Write(
            $"tasks/{ContentTestApi.EasyTask}/task.yaml",
            "material: invoice-mailer\ndifficulty: easy\nfindings:\n  - card: secrets-in-repo\n    leaves: [rule.conventions]\n  - card: money-in-float\n    leaves: [manual.representation]\n");
        var card = Path.Combine(api.ContentRoot, "problems", "secrets-in-repo", "card.yaml");
        await File.WriteAllTextAsync(card, "class: hygiene\nweight: 1\n", TestContext.Current.CancellationToken);
        await api.IngestAsync("second");

        Assert.Equal(stored, await GetRawAsync($"/api/v1/attempts/{id}"));

        using var after = await GetJsonAsync("/api/v1/tasks");
        var solved = after.RootElement.GetProperty("items").EnumerateArray()
            .ToDictionary(task => task.GetProperty("slug").GetString()!, task => task.GetProperty("solved").GetBoolean());
        Assert.True(solved[ContentTestApi.EasyTask]);
        Assert.False(solved[ContentTestApi.MediumTask]);
    }

    [Fact]
    public async Task TwoCountingAttempts_AtOneTask_AreRefusedByTheDatabase_UnderTheNameSubmitWatchesFor()
    {
        // Two first submits racing past the check in submit both try to count; the unique index is what
        // stops the second, and submit recognises it by this name to save it as practice instead.
        var user = Guid.CreateVersion7();
        var now = DateTimeOffset.UtcNow;

        await using var scope = api.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AttemptsDbContext>();

        foreach (var _ in new[] { 1, 2 })
        {
            var attempt = Attempt.Start(user, ContentTestApi.MediumTask, now);
            attempt.Submit(now, "first", "{\"picks\":[]}", "{}", "{}", 0, 15, countsTowardProgress: true);
            context.Attempts.Add(attempt);
        }

        var refused = await Assert.ThrowsAsync<Microsoft.EntityFrameworkCore.DbUpdateException>(
            () => context.SaveChangesAsync(TestContext.Current.CancellationToken));

        var postgres = Assert.IsType<Npgsql.PostgresException>(refused.InnerException);
        Assert.Equal(Npgsql.PostgresErrorCodes.UniqueViolation, postgres.SqlState);
        Assert.Equal("ux_attempts_first_submission", postgres.ConstraintName);
    }

    private async Task<string> GetRawAsync(string path)
    {
        var response = await api.Client.GetAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    }

    private async Task<JsonDocument> GetJsonAsync(string path) => JsonDocument.Parse(await GetRawAsync(path));
}

/// <summary>The per-user cap on submitting, set to two here.</summary>
public sealed class AttemptRateLimitTests(RateLimitedContentTestApi api) : IClassFixture<RateLimitedContentTestApi>
{
    [Fact]
    public async Task TheSubmitPastTheCap_IsRefused_AndLeavesTheAttemptOpen()
    {
        var answer = new { picks = Array.Empty<object>() };
        var ids = new List<string>();

        for (var index = 0; index < 3; index++)
        {
            var start = await api.Client.PostAsJsonAsync(new Uri("/api/v1/attempts", UriKind.Relative), new { task = ContentTestApi.EasyTask }, TestContext.Current.CancellationToken);
            ids.Add((await start.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetProperty("id").GetString()!);
        }

        foreach (var id in ids.Take(2))
        {
            (await api.Client.PostAsJsonAsync(new Uri($"/api/v1/attempts/{id}/submit", UriKind.Relative), answer, TestContext.Current.CancellationToken)).EnsureSuccessStatusCode();
        }

        var refused = await api.Client.PostAsJsonAsync(new Uri($"/api/v1/attempts/{ids[2]}/submit", UriKind.Relative), answer, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode);
        var body = await refused.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("attempt_rate_limited", body.GetProperty("code").GetString());

        var open = await api.Client.GetFromJsonAsync<JsonElement>(new Uri($"/api/v1/attempts/{ids[2]}", UriKind.Relative), TestContext.Current.CancellationToken);
        Assert.Equal(JsonValueKind.Null, open.GetProperty("submittedAt").ValueKind);
    }
}

/// <summary>Signed out, the attempt endpoints are closed, and the catalogue has no solved flags.</summary>
public sealed class AnonymousAttemptTests(AnonymousTestApi api) : IClassFixture<AnonymousTestApi>
{
    [Fact]
    public async Task EveryAttemptEndpoint_NeedsASignedInCaller()
    {
        var id = Guid.CreateVersion7();

        Assert.Equal(HttpStatusCode.Unauthorized, (await api.Client.PostAsJsonAsync(new Uri("/api/v1/attempts", UriKind.Relative), new { task = "x" }, TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.Client.GetAsync(new Uri("/api/v1/attempts", UriKind.Relative), TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.Client.GetAsync(new Uri($"/api/v1/attempts/{id}", UriKind.Relative), TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.Client.PostAsJsonAsync(new Uri($"/api/v1/attempts/{id}/submit", UriKind.Relative), new { picks = Array.Empty<object>() }, TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.Client.PatchAsJsonAsync(new Uri($"/api/v1/attempts/{id}", UriKind.Relative), new { step = "treatment" }, TestContext.Current.CancellationToken)).StatusCode);
    }
}
