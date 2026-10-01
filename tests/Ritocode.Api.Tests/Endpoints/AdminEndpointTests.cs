using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Ritocode.Api.Tests.Infrastructure;
using Ritocode.Modules.Attempts.Domain;
using Ritocode.Modules.Attempts.Persistence;
using Ritocode.Modules.Auth.Domain;
using Ritocode.Modules.Auth.Persistence;
using Ritocode.Modules.Users.Domain;
using Ritocode.Modules.Users.Persistence;
using Ritocode.Shared.Identity;

namespace Ritocode.Api.Tests.Endpoints;

/// <summary>
/// The admin area (docs/SPEC.md §6.2) for an admin: the development identity, whose address the fixture
/// names. Its signals, users and attempts lists read every user's rows; another user's are written
/// directly.
/// </summary>
public sealed class AdminEndpointTests(AdminContentTestApi api) : IClassFixture<AdminContentTestApi>
{
    private static readonly object AnswerWithAnExtraPick = new
    {
        picks = new object[]
        {
            new { card = "secrets-in-repo", leaves = new[] { "auto.secrets" } },
            new { card = "hardcoded-config", leaves = new[] { "manual.extract-config" } },
        },
    };

    private static readonly Guid Me = new DevelopmentIdentityOptions().UserId;

    [Fact]
    public async Task AnAdmin_IsToldSoByMe()
    {
        using var me = await GetJsonAsync("/api/v1/me", HttpStatusCode.OK);

        Assert.True(me.RootElement.GetProperty("admin").GetBoolean());
    }

    [Fact]
    public async Task ASignal_IsListedOpen_ThenResolved_AndResolvingAgainKeepsTheFirstTime()
    {
        var attempt = await SubmitAsync(ContentTestApi.MediumTask, AnswerWithAnExtraPick);
        using (await JsonAsync(await PostAsync("/api/v1/signals", new { attempt, card = "secrets-in-repo", comment = "Ключ в settings.py." }), HttpStatusCode.Created))
        {
        }

        var other = await AnotherUserAsync("signaller@example.test", IdentityProvider.Google);
        var theirs = await AnotherUsersSignalAsync(other);

        using var open = await GetJsonAsync("/api/v1/admin/signals", HttpStatusCode.OK);
        var rows = open.RootElement.GetProperty("items").EnumerateArray().ToList();
        var mine = rows.Single(row => row.GetProperty("attempt").GetGuid() == attempt);
        var theirsRow = rows.Single(row => row.GetProperty("id").GetGuid() == theirs);

        // Newest first: the other user's was written last.
        Assert.True(rows.IndexOf(theirsRow) < rows.IndexOf(mine));

        Assert.Equal(ContentTestApi.MediumTask, mine.GetProperty("task").GetString());
        Assert.Equal("secrets-in-repo", mine.GetProperty("card").GetString());
        Assert.NotEqual("secrets-in-repo", mine.GetProperty("cardName").GetString());
        Assert.Equal("Ключ в settings.py.", mine.GetProperty("comment").GetString());
        Assert.Equal(Me, mine.GetProperty("learner").GetProperty("id").GetGuid());
        Assert.Equal("developer@ritocode.local", mine.GetProperty("learner").GetProperty("email").GetString());
        Assert.Equal(JsonValueKind.Null, mine.GetProperty("resolvedAt").ValueKind);
        Assert.Equal("signaller@example.test", theirsRow.GetProperty("learner").GetProperty("email").GetString());
        Assert.Equal("Хардкод конфигурации", theirsRow.GetProperty("cardName").GetString());

        using var resolved = await JsonAsync(await PostAsync($"/api/v1/admin/signals/{theirs}/resolve", null), HttpStatusCode.OK);
        var resolvedAt = resolved.RootElement.GetProperty("resolvedAt").GetDateTimeOffset();

        using var again = await JsonAsync(await PostAsync($"/api/v1/admin/signals/{theirs}/resolve", null), HttpStatusCode.OK);
        Assert.Equal(resolvedAt, again.RootElement.GetProperty("resolvedAt").GetDateTimeOffset());

        using var openAfter = await GetJsonAsync("/api/v1/admin/signals?status=open", HttpStatusCode.OK);
        Assert.DoesNotContain(openAfter.RootElement.GetProperty("items").EnumerateArray(), row => row.GetProperty("id").GetGuid() == theirs);

        using var resolvedList = await GetJsonAsync("/api/v1/admin/signals?status=Resolved", HttpStatusCode.OK);
        var listed = Assert.Single(resolvedList.RootElement.GetProperty("items").EnumerateArray(), row => row.GetProperty("id").GetGuid() == theirs);
        Assert.Equal(resolvedAt, listed.GetProperty("resolvedAt").GetDateTimeOffset());
    }

    [Fact]
    public async Task ResolvingASignalThatDoesNotExist_IsANotFound()
    {
        using var missing = await JsonAsync(await PostAsync($"/api/v1/admin/signals/{Guid.CreateVersion7()}/resolve", null), HttpStatusCode.NotFound);
        using var malformed = await JsonAsync(await PostAsync("/api/v1/admin/signals/not-an-id/resolve", null), HttpStatusCode.NotFound);

        Assert.Equal("signal_not_found", missing.RootElement.GetProperty("code").GetString());
        Assert.Equal("signal_not_found", malformed.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task AnAbandonedAttempt_ShowsTheStepItStoppedAt_AndASubmittedOneItsScoreAndTime()
    {
        // Abandoned at step 2.
        var abandoned = await StartAsync(ContentTestApi.EasyTask);
        var step = await api.Client.PatchAsJsonAsync(new Uri($"/api/v1/attempts/{abandoned}", UriKind.Relative), new { step = "treatment" }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, step.StatusCode);

        var first = await SubmitAsync(ContentTestApi.EasyTask, AnswerWithAnExtraPick);
        var practice = await SubmitAsync(ContentTestApi.EasyTask, AnswerWithAnExtraPick);

        using var open = await GetJsonAsync($"/api/v1/admin/attempts?status=open&user={Me}&pageSize=100", HttpStatusCode.OK);
        var stopped = Assert.Single(open.RootElement.GetProperty("items").EnumerateArray(), row => row.GetProperty("id").GetGuid() == abandoned);
        Assert.Equal("treatment", stopped.GetProperty("step").GetString());
        Assert.Equal(JsonValueKind.Null, stopped.GetProperty("submittedAt").ValueKind);
        Assert.Equal(JsonValueKind.Null, stopped.GetProperty("timeTakenSeconds").ValueKind);
        Assert.Equal(JsonValueKind.Null, stopped.GetProperty("score").ValueKind);
        Assert.All(open.RootElement.GetProperty("items").EnumerateArray(), row => Assert.Equal(JsonValueKind.Null, row.GetProperty("submittedAt").ValueKind));

        using var submitted = await GetJsonAsync($"/api/v1/admin/attempts?status=submitted&user={Me}&pageSize=100", HttpStatusCode.OK);
        var rows = submitted.RootElement.GetProperty("items").EnumerateArray().ToList();
        var firstRow = rows.Single(row => row.GetProperty("id").GetGuid() == first);
        var practiceRow = rows.Single(row => row.GetProperty("id").GetGuid() == practice);

        Assert.True(rows.IndexOf(practiceRow) < rows.IndexOf(firstRow));
        Assert.False(firstRow.GetProperty("practice").GetBoolean());
        Assert.True(practiceRow.GetProperty("practice").GetBoolean());
        Assert.Equal(ContentTestApi.EasyTask, firstRow.GetProperty("task").GetString());
        Assert.True(firstRow.GetProperty("timeTakenSeconds").GetInt64() >= 0);
        Assert.True(firstRow.GetProperty("maxScore").GetInt32() > 0);
        Assert.Equal(JsonValueKind.Number, firstRow.GetProperty("score").ValueKind);
        Assert.Equal("developer", firstRow.GetProperty("learner").GetProperty("username").GetString());
        Assert.DoesNotContain(rows, row => row.GetProperty("id").GetGuid() == abandoned);
    }

    [Fact]
    public async Task TheAttemptsList_FiltersByLearner()
    {
        var other = await AnotherUserAsync("filtered@example.test", IdentityProvider.GitHub);
        await AnotherUsersSignalAsync(other);
        await StartAsync(ContentTestApi.EasyTask);

        using var theirs = await GetJsonAsync($"/api/v1/admin/attempts?user={other}", HttpStatusCode.OK);
        var row = Assert.Single(theirs.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal(other, row.GetProperty("learner").GetProperty("id").GetGuid());
        Assert.Equal(1, theirs.RootElement.GetProperty("totalItems").GetInt64());

        using var everyone = await GetJsonAsync("/api/v1/admin/attempts?pageSize=100", HttpStatusCode.OK);
        var learners = everyone.RootElement.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("learner").GetProperty("id").GetGuid()).ToHashSet();
        Assert.Contains(other, learners);
        Assert.Contains(Me, learners);
    }

    [Fact]
    public async Task TheUsersList_NamesEachUsersProviders_AttemptsAndTasksSolved()
    {
        var other = await AnotherUserAsync("counted@example.test", IdentityProvider.GitHub, IdentityProvider.Google);
        await AnotherUsersSignalAsync(other);
        await StartAsync(ContentTestApi.EasyTask);

        using var page = await GetJsonAsync("/api/v1/admin/users?pageSize=100", HttpStatusCode.OK);
        var rows = page.RootElement.GetProperty("items").EnumerateArray().ToList();

        var theirs = rows.Single(row => row.GetProperty("id").GetGuid() == other);
        Assert.Equal("counted@example.test", theirs.GetProperty("email").GetString());
        Assert.Equal(["github", "google"], theirs.GetProperty("providers").EnumerateArray().Select(provider => provider.GetString()));
        Assert.Equal(1, theirs.GetProperty("attempts").GetInt32());
        Assert.Equal(1, theirs.GetProperty("tasksSolved").GetInt32());

        // The development identity is linked to no provider, and has at least the attempt started above.
        var mine = rows.Single(row => row.GetProperty("id").GetGuid() == Me);
        Assert.Empty(mine.GetProperty("providers").EnumerateArray());
        Assert.True(mine.GetProperty("attempts").GetInt32() >= 1);
        Assert.Equal(JsonValueKind.String, mine.GetProperty("registeredAt").ValueKind);

        // Newest first: the user made above was registered after the seeded one.
        Assert.True(rows.IndexOf(theirs) < rows.IndexOf(mine));
    }

    [Fact]
    public async Task ABadFilterOrPage_IsAValidationError_NamingEveryBadParameter()
    {
        using var signals = await GetJsonAsync("/api/v1/admin/signals?status=closed&pageSize=1000", HttpStatusCode.BadRequest);
        var errors = signals.RootElement.GetProperty("errors");
        Assert.True(errors.TryGetProperty("status", out _));
        Assert.True(errors.TryGetProperty("pageSize", out _));

        using var attempts = await GetJsonAsync("/api/v1/admin/attempts?status=1&user=someone", HttpStatusCode.BadRequest);
        Assert.True(attempts.RootElement.GetProperty("errors").TryGetProperty("status", out _));
        Assert.True(attempts.RootElement.GetProperty("errors").TryGetProperty("user", out _));

        using var users = await GetJsonAsync("/api/v1/admin/users?page=0", HttpStatusCode.BadRequest);
        Assert.True(users.RootElement.GetProperty("errors").TryGetProperty("page", out _));
    }

    /// <summary>A user other than the caller, linked to <paramref name="providers"/>.</summary>
    private async Task<Guid> AnotherUserAsync(string email, params IdentityProvider[] providers)
    {
        await using var scope = api.Services.CreateAsyncScope();

        var user = User.Create(email, email.Split('@')[0], DateTimeOffset.UtcNow);
        var users = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
        users.Users.Add(user);
        await users.SaveChangesAsync(TestContext.Current.CancellationToken);

        var auth = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        foreach (var provider in providers)
        {
            auth.LinkedAccounts.Add(LinkedAccount.Create(user.Id, provider, $"{provider}-{user.Id:N}", user.Username, DateTimeOffset.UtcNow));
        }

        await auth.SaveChangesAsync(TestContext.Current.CancellationToken);

        return user.Id;
    }

    /// <summary>A submitted attempt of <paramref name="userId"/>'s with <c>hardcoded-config</c> as an extra pick, and a signal from it.</summary>
    private async Task<Guid> AnotherUsersSignalAsync(Guid userId)
    {
        var started = DateTimeOffset.UtcNow.AddMinutes(-5);
        var attempt = Attempt.Start(userId, ContentTestApi.EasyTask, started);
        attempt.Submit(
            started.AddMinutes(4),
            "first",
            "{\"picks\":[]}",
            """{"total":0,"maximum":30,"isCorrect":false,"cards":[{"card":"hardcoded-config","outcome":"extra","points":-3,"keyLeaves":null,"treatment":null}]}""",
            "{}",
            0,
            30,
            countsTowardProgress: true);

        var signal = Signal.Send(attempt, "hardcoded-config", null, DateTimeOffset.UtcNow);

        await using var scope = api.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AttemptsDbContext>();
        context.Attempts.Add(attempt);
        context.Signals.Add(signal);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return signal.Id;
    }

    private async Task<Guid> StartAsync(string task)
    {
        using var body = await JsonAsync(await PostAsync("/api/v1/attempts", new { task }), HttpStatusCode.Created);
        return body.RootElement.GetProperty("id").GetGuid();
    }

    private async Task<Guid> SubmitAsync(string task, object answer)
    {
        var id = await StartAsync(task);
        using (await JsonAsync(await PostAsync($"/api/v1/attempts/{id}/submit", answer), HttpStatusCode.OK))
        {
        }

        return id;
    }

    private Task<HttpResponseMessage> PostAsync(string path, object? body) =>
        api.Client.PostAsJsonAsync(new Uri(path, UriKind.Relative), body, TestContext.Current.CancellationToken);

    private async Task<JsonDocument> GetJsonAsync(string path, HttpStatusCode expected) =>
        await JsonAsync(await api.Client.GetAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken), expected);

    private static async Task<JsonDocument> JsonAsync(HttpResponseMessage response, HttpStatusCode expected)
    {
        var text = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.True(expected == response.StatusCode, $"Expected {expected}, got {(int)response.StatusCode}: {text}");
        return JsonDocument.Parse(text);
    }
}

/// <summary>
/// The admin area for a signed-in caller who is not an admin: every address answers as one that serves
/// nothing, before its parameters are even read, so the area does not confirm it exists.
/// </summary>
public sealed class NonAdminEndpointTests(NonAdminContentTestApi api) : IClassFixture<NonAdminContentTestApi>
{
    public static TheoryData<string, string> AdminAddresses { get; } = new()
    {
        { "GET", "/api/v1/admin/signals" },
        { "GET", "/api/v1/admin/signals?status=nonsense&pageSize=abc" },
        { "POST", $"/api/v1/admin/signals/{Guid.CreateVersion7()}/resolve" },
        { "GET", "/api/v1/admin/users" },
        { "GET", "/api/v1/admin/attempts?user=someone" },
    };

    [Theory]
    [MemberData(nameof(AdminAddresses))]
    public async Task EveryAdminAddress_AnswersAsAnUnknownOne(string method, string path)
    {
        using var unknown = await SendAsync("GET", "/api/v1/admin/no-such-list");
        using var answer = await SendAsync(method, path);

        Assert.Equal(HttpStatusCode.NotFound, unknown.Status);
        Assert.Equal(HttpStatusCode.NotFound, answer.Status);
        Assert.Equal("not_found", answer.Body.RootElement.GetProperty("code").GetString());

        // Everything but the address and the ids of the request is the same.
        Assert.Equal(Without(unknown.Body.RootElement), Without(answer.Body.RootElement));
    }

    [Fact]
    public async Task ANonAdmin_IsToldSoByMe()
    {
        using var me = await SendAsync("GET", "/api/v1/me");

        Assert.Equal(HttpStatusCode.OK, me.Status);
        Assert.False(me.Body.RootElement.GetProperty("admin").GetBoolean());
    }

    private async Task<Answer> SendAsync(string method, string path)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), new Uri(path, UriKind.Relative));
        var response = await api.Client.SendAsync(request, TestContext.Current.CancellationToken);
        var text = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        return new Answer(response.StatusCode, JsonDocument.Parse(text));
    }

    private static string Without(JsonElement body) =>
        JsonSerializer.Serialize(body.EnumerateObject()
            .Where(item => item.Name is not ("instance" or "requestId" or "traceId"))
            .ToDictionary(item => item.Name, item => item.Value));

    private sealed record Answer(HttpStatusCode Status, JsonDocument Body) : IDisposable
    {
        public void Dispose() => Body.Dispose();
    }
}

/// <summary>Signed out, the admin area is a 401, as every address under the API is.</summary>
public sealed class AnonymousAdminEndpointTests(AnonymousTestApi api) : IClassFixture<AnonymousTestApi>
{
    [Theory]
    [InlineData("/api/v1/admin/signals")]
    [InlineData("/api/v1/admin/users")]
    [InlineData("/api/v1/admin/attempts")]
    [InlineData("/api/v1/admin/no-such-list")]
    public async Task ASignedOutCaller_IsUnauthenticated(string path)
    {
        var response = await api.Client.GetAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal("unauthenticated", body.RootElement.GetProperty("code").GetString());
    }
}

/// <summary>The list of admins as configuration hands it over.</summary>
public sealed class AdminOptionsTests
{
    [Fact]
    public void ABlankEntry_NamesNobody_AndDoesNotStopTheHost()
    {
        // What the production Compose file passes when ADMIN_EMAIL is left empty.
        var options = new Ritocode.Modules.Users.Admin.AdminOptions { Emails = ["", "  "] };

        Assert.True(options.IsValid());
        Assert.False(options.Names(""));
        Assert.False(options.Names("anyone@example.test"));
    }

    [Fact]
    public void AnEntryThatIsNotAnAddress_IsRefused()
    {
        Assert.False(new Ritocode.Modules.Users.Admin.AdminOptions { Emails = ["admin"] }.IsValid());
        Assert.True(new Ritocode.Modules.Users.Admin.AdminOptions { Emails = [" Admin@Example.Test "] }.Names("admin@example.test"));
    }
}
