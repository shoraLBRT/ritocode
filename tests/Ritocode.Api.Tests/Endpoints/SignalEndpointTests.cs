using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ritocode.Api.Tests.Infrastructure;
using Ritocode.Modules.Attempts.Domain;
using Ritocode.Modules.Attempts.Persistence;

namespace Ritocode.Api.Tests.Endpoints;

/// <summary>
/// <c>POST /api/v1/signals</c> (docs/SPEC.md §4.8): from an extra pick of the caller's own submitted
/// attempt only, once its answer is revealed (ADR 0017), once per pick, and never changing the attempt. The reference easy task's key lists
/// secrets, money in a float and a swallowed error; <c>hardcoded-config</c> is the extra pick here.
/// </summary>
public sealed class SignalEndpointTests(ContentTestApi api) : IClassFixture<ContentTestApi>
{
    private static readonly object Answer = new
    {
        picks = new object[]
        {
            new { card = "secrets-in-repo", leaves = new[] { "auto.secrets" } },
            new { card = "hardcoded-config", leaves = new[] { "manual.extract-config" } },
        },
    };

    [Fact]
    public async Task ASignal_FromAnExtraPick_IsKept_AndTheAttemptIsUntouched()
    {
        var id = await SubmitAsync(ContentTestApi.EasyTask, Answer);
        var before = await AttemptJsonAsync(id);

        using var sent = await JsonAsync(await SendAsync(new { attempt = id, card = "hardcoded-config", comment = "  Порт зашит в код, строка 12.  " }), HttpStatusCode.Created);
        var signal = sent.RootElement;

        Assert.Equal(id.ToString(), signal.GetProperty("attempt").GetString());
        Assert.Equal(ContentTestApi.EasyTask, signal.GetProperty("task").GetString());
        Assert.Equal("hardcoded-config", signal.GetProperty("card").GetString());
        Assert.Equal("Порт зашит в код, строка 12.", signal.GetProperty("comment").GetString());

        // The attempt reads the same, score and all, except that it now names the signalled card.
        using var after = JsonDocument.Parse(await AttemptJsonAsync(id));
        Assert.Equal(["hardcoded-config"], after.RootElement.GetProperty("signalledCards").EnumerateArray().Select(card => card.GetString()));

        using var beforeDocument = JsonDocument.Parse(before);
        Assert.Empty(beforeDocument.RootElement.GetProperty("signalledCards").EnumerateArray());
        Assert.Equal(Without(beforeDocument.RootElement, "signalledCards"), Without(after.RootElement, "signalledCards"));

        await using var scope = api.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AttemptsDbContext>();
        var stored = await context.Signals.AsNoTracking().SingleAsync(row => row.AttemptId == id, TestContext.Current.CancellationToken);
        Assert.Null(stored.ResolvedAt);
    }

    [Fact]
    public async Task OnlyAnExtraPick_CanBeSignalled_AndOnlyOnce()
    {
        var id = await SubmitAsync(ContentTestApi.EasyTask, Answer);

        // Found, missed (never picked), and a card nobody has heard of: none is an extra pick.
        foreach (var card in new[] { "secrets-in-repo", "money-in-float", "no-such-card" })
        {
            using var refused = await JsonAsync(await SendAsync(new { attempt = id, card }), HttpStatusCode.BadRequest);
            Assert.Equal("validation_failed", refused.RootElement.GetProperty("code").GetString());
            Assert.True(refused.RootElement.GetProperty("errors").TryGetProperty("card", out _));
        }

        using (await JsonAsync(await SendAsync(new { attempt = id, card = "hardcoded-config" }), HttpStatusCode.Created))
        {
        }

        using var again = await JsonAsync(await SendAsync(new { attempt = id, card = "hardcoded-config", comment = "ещё раз" }), HttpStatusCode.Conflict);
        Assert.Equal("signal_already_sent", again.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task AnAttemptNotYetSubmitted_HasNothingToSignal_AndSaysNothingAboutItsKey()
    {
        var id = await StartAsync(ContentTestApi.EasyTask);

        // A card in the key and one outside it answer alike: the key is not revealed before submitting.
        using var inKey = await JsonAsync(await SendAsync(new { attempt = id, card = "secrets-in-repo" }), HttpStatusCode.Conflict);
        using var outside = await JsonAsync(await SendAsync(new { attempt = id, card = "hardcoded-config" }), HttpStatusCode.Conflict);

        Assert.Equal("attempt_not_submitted", inKey.RootElement.GetProperty("code").GetString());
        Assert.Equal(inKey.RootElement.GetProperty("detail").GetString(), outside.RootElement.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task BeforeTheAnswerIsRevealed_NoCardCanBeSignalled_AndTheRefusalSaysNothingAboutTheKey()
    {
        // The medium task, which no other test here reveals: its key lists hardcoded configuration only.
        var answer = new { picks = new[] { new { card = "hardcoded-config", leaves = new[] { "manual.extract-config" } }, new { card = "secrets-in-repo", leaves = new[] { "auto.secrets" } } } };
        var id = await SubmitAsync(ContentTestApi.MediumTask, answer, reveal: false);

        using var found = await JsonAsync(await SendAsync(new { attempt = id, card = "hardcoded-config" }), HttpStatusCode.Conflict);
        using var extra = await JsonAsync(await SendAsync(new { attempt = id, card = "secrets-in-repo" }), HttpStatusCode.Conflict);
        using var unknown = await JsonAsync(await SendAsync(new { attempt = id, card = "no-such-card" }), HttpStatusCode.Conflict);

        Assert.Equal("answer_not_revealed", found.RootElement.GetProperty("code").GetString());
        Assert.Equal(found.RootElement.GetProperty("detail").GetString(), extra.RootElement.GetProperty("detail").GetString());
        Assert.Equal("answer_not_revealed", extra.RootElement.GetProperty("code").GetString());
        Assert.Equal(found.RootElement.GetProperty("detail").GetString(), unknown.RootElement.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task AnotherUsersAttempt_AnswersExactlyAsAMissingOne()
    {
        var theirs = Attempt.Start(Guid.CreateVersion7(), ContentTestApi.EasyTask, DateTimeOffset.UtcNow);
        theirs.Submit(
            DateTimeOffset.UtcNow,
            "first",
            "{\"picks\":[]}",
            """{"total":0,"maximum":0,"isCorrect":false,"cards":[{"card":"hardcoded-config","outcome":"extra","points":-3,"keyLeaves":null,"treatment":null}]}""",
            "{}",
            0,
            0,
            countsTowardProgress: true);

        await using (var scope = api.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AttemptsDbContext>();
            context.Attempts.Add(theirs);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using var other = await JsonAsync(await SendAsync(new { attempt = theirs.Id, card = "hardcoded-config" }), HttpStatusCode.NotFound);
        using var missing = await JsonAsync(await SendAsync(new { attempt = Guid.CreateVersion7(), card = "hardcoded-config" }), HttpStatusCode.NotFound);
        using var malformed = await JsonAsync(await SendAsync(new { attempt = "not-an-id", card = "hardcoded-config" }), HttpStatusCode.NotFound);

        Assert.Equal("attempt_not_found", other.RootElement.GetProperty("code").GetString());
        Assert.Equal(missing.RootElement.GetProperty("detail").GetString(), other.RootElement.GetProperty("detail").GetString());
        Assert.Equal("attempt_not_found", malformed.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task AMissingCard_OrACommentOverFiveHundredCharacters_IsAValidationError_AndABlankCommentIsNone()
    {
        var id = await SubmitAsync(ContentTestApi.EasyTask, Answer);

        using var noCard = await JsonAsync(await SendAsync(new { attempt = id }), HttpStatusCode.BadRequest);
        Assert.True(noCard.RootElement.GetProperty("errors").TryGetProperty("card", out _));

        using var tooLong = await JsonAsync(await SendAsync(new { attempt = id, card = "hardcoded-config", comment = new string('а', 501) }), HttpStatusCode.BadRequest);
        Assert.True(tooLong.RootElement.GetProperty("errors").TryGetProperty("comment", out _));

        using var blank = await JsonAsync(await SendAsync(new { attempt = id, card = "hardcoded-config", comment = "   " }), HttpStatusCode.Created);
        Assert.Equal(JsonValueKind.Null, blank.RootElement.GetProperty("comment").ValueKind);
    }

    private async Task<Guid> StartAsync(string task)
    {
        var response = await api.Client.PostAsJsonAsync(new Uri("/api/v1/attempts", UriKind.Relative), new { task }, TestContext.Current.CancellationToken);
        using var body = await JsonAsync(response, HttpStatusCode.Created);
        return body.RootElement.GetProperty("id").GetGuid();
    }

    /// <summary>A submitted attempt, its answer revealed unless <paramref name="reveal"/> says otherwise.</summary>
    private async Task<Guid> SubmitAsync(string task, object answer, bool reveal = true)
    {
        var id = await StartAsync(task);
        var response = await api.Client.PostAsJsonAsync(new Uri($"/api/v1/attempts/{id}/submit", UriKind.Relative), answer, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        if (reveal)
        {
            var revealed = await api.Client.PostAsync(new Uri($"/api/v1/attempts/{id}/reveal", UriKind.Relative), null, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, revealed.StatusCode);
        }

        return id;
    }

    private Task<HttpResponseMessage> SendAsync(object body) =>
        api.Client.PostAsJsonAsync(new Uri("/api/v1/signals", UriKind.Relative), body, TestContext.Current.CancellationToken);

    private Task<string> AttemptJsonAsync(Guid id) =>
        api.Client.GetStringAsync(new Uri($"/api/v1/attempts/{id}", UriKind.Relative), TestContext.Current.CancellationToken);

    private static string Without(JsonElement element, string property) =>
        JsonSerializer.Serialize(element.EnumerateObject().Where(item => item.Name != property).ToDictionary(item => item.Name, item => item.Value));

    private static async Task<JsonDocument> JsonAsync(HttpResponseMessage response, HttpStatusCode expected)
    {
        var text = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.True(expected == response.StatusCode, $"Expected {expected}, got {(int)response.StatusCode}: {text}");
        return JsonDocument.Parse(text);
    }
}

/// <summary>The per-user cap on signals, set to two here.</summary>
public sealed class SignalRateLimitTests(SignalRateLimitedContentTestApi api) : IClassFixture<SignalRateLimitedContentTestApi>
{
    private static readonly string[] Leaves = ["manual.split"];

    [Fact]
    public async Task TheSignalPastTheCap_IsRefused()
    {
        // The medium task's key lists only hardcoded configuration: the other three picks are extra.
        var start = await api.Client.PostAsJsonAsync(new Uri("/api/v1/attempts", UriKind.Relative), new { task = ContentTestApi.MediumTask }, TestContext.Current.CancellationToken);
        var id = (await start.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetProperty("id").GetString();
        string[] extras = ["money-in-float", "secrets-in-repo", "swallowed-error"];
        var answer = new { picks = extras.Select(card => new { card, leaves = Leaves }) };
        (await api.Client.PostAsJsonAsync(new Uri($"/api/v1/attempts/{id}/submit", UriKind.Relative), answer, TestContext.Current.CancellationToken)).EnsureSuccessStatusCode();
        (await api.Client.PostAsync(new Uri($"/api/v1/attempts/{id}/reveal", UriKind.Relative), null, TestContext.Current.CancellationToken)).EnsureSuccessStatusCode();

        foreach (var card in extras.Take(2))
        {
            var sent = await api.Client.PostAsJsonAsync(new Uri("/api/v1/signals", UriKind.Relative), new { attempt = id, card }, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Created, sent.StatusCode);
        }

        var refused = await api.Client.PostAsJsonAsync(new Uri("/api/v1/signals", UriKind.Relative), new { attempt = id, card = extras[2] }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode);
        var body = await refused.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("signal_rate_limited", body.GetProperty("code").GetString());
    }
}

/// <summary>Signed out, no signal can be sent.</summary>
public sealed class AnonymousSignalTests(AnonymousTestApi api) : IClassFixture<AnonymousTestApi>
{
    [Fact]
    public async Task ASignedOutCaller_IsUnauthenticated()
    {
        var response = await api.Client.PostAsJsonAsync(
            new Uri("/api/v1/signals", UriKind.Relative),
            new { attempt = Guid.CreateVersion7(), card = "hardcoded-config" },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
