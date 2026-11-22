using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Ritocode.Api.Tests.Infrastructure;
using Ritocode.Modules.Attempts.Domain;
using Ritocode.Modules.Attempts.Persistence;
using Ritocode.Modules.Auth.Domain;
using Ritocode.Modules.Auth.Persistence;
using Ritocode.Modules.Auth.Session;
using Ritocode.Modules.Users.Domain;
using Ritocode.Modules.Users.Persistence;
using Ritocode.Shared.Identity;
using Ritocode.TestSupport;

namespace Ritocode.Api.Tests.Endpoints;

/// <summary>
/// <c>GET /api/v1/me/export</c> and <c>DELETE /api/v1/me</c> (#299), on a host with the development
/// identity off, as production runs: the export holds the caller's rows and no one else's, and deleting
/// removes every row of every module that names the caller — proved by walking every module schema's
/// columns, so a table added later is swept too — while another user's rows stay.
/// </summary>
public sealed class AccountEndpointTests(AnonymousTestApi api) : IClassFixture<AnonymousTestApi>
{
    [Fact]
    public async Task TheExport_HoldsTheCallersRows_AndNoOneElses()
    {
        var ada = await SeedAsync("export-ada");
        var other = await SeedAsync("export-other");

        using var response = await SendAsync(HttpMethod.Get, "/api/v1/me/export", ada.Session);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("attachment", response.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Matches(@"^""?ritocode-export-\d{4}-\d{2}-\d{2}\.json""?$", response.Content.Headers.ContentDisposition?.FileName);

        var text = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.DoesNotContain(other.Id.ToString(), text, StringComparison.Ordinal);
        Assert.DoesNotContain(other.Email, text, StringComparison.Ordinal);

        // What authenticates a session never travels in a file.
        Assert.DoesNotContain(ada.Session.Token, text, StringComparison.Ordinal);
        Assert.DoesNotContain(ada.Session.CsrfToken, text, StringComparison.Ordinal);
        Assert.DoesNotContain(UserSession.Hash(ada.Session.Token), text, StringComparison.Ordinal);

        using var export = JsonDocument.Parse(text);
        var root = export.RootElement;
        Assert.Equal(ada.Id.ToString(), root.GetProperty("user").GetString());

        var modules = root.GetProperty("modules");
        Assert.Equal(["users", "attempts", "auth"], modules.EnumerateObject().Select(module => module.Name));

        var account = modules.GetProperty("users").GetProperty("account");
        Assert.Equal(ada.Email, account.GetProperty("email").GetString());
        Assert.Equal("export-ada", account.GetProperty("username").GetString());

        var auth = modules.GetProperty("auth");
        Assert.Equal("github", Assert.Single(auth.GetProperty("linkedAccounts").EnumerateArray()).GetProperty("provider").GetString());
        Assert.Single(auth.GetProperty("sessions").EnumerateArray());

        var attempts = modules.GetProperty("attempts");
        var byTask = attempts.GetProperty("attempts").EnumerateArray().ToDictionary(attempt => attempt.GetProperty("task").GetString()!);
        Assert.Equal(2, byTask.Count);

        // The key is in an attempt's result: there only for the task whose answer was revealed (ADR 0017).
        Assert.Equal(JsonValueKind.Object, byTask[RevealedTask].GetProperty("result").ValueKind);
        Assert.Equal(JsonValueKind.Null, byTask[HiddenTask].GetProperty("result").ValueKind);
        Assert.Equal(JsonValueKind.Null, byTask[HiddenTask].GetProperty("review").ValueKind);
        Assert.Equal(JsonValueKind.Object, byTask[HiddenTask].GetProperty("answer").ValueKind);

        Assert.Single(attempts.GetProperty("signals").EnumerateArray());
        Assert.Equal(RevealedTask, Assert.Single(attempts.GetProperty("reveals").EnumerateArray()).GetProperty("task").GetString());
        Assert.Equal("secrets-in-repo", Assert.Single(attempts.GetProperty("studyMarks").EnumerateArray()).GetProperty("card").GetString());
    }

    [Fact]
    public async Task Deleting_RemovesEveryRowOfEveryModule_SignsOut_AndLeavesAnotherUsersRows()
    {
        var ada = await SeedAsync("delete-ada");
        var other = await SeedAsync("delete-other");

        var before = await RowsNamingAsync(ada.Id);
        var otherBefore = await RowsNamingAsync(other.Id);

        // The walk is not vacuous: the seed reaches every table that names a user, so a module that adds
        // one without erasing it fails here rather than passing over an empty table.
        Assert.All(await UserTablesAsync(), table => Assert.True(before.GetValueOrDefault(table) > 0, $"The seed wrote nothing to {table}."));

        // Typed as the learner remembers it: the username is stored lower-cased, and compared without case.
        using (var deleted = await SendAsync(HttpMethod.Delete, "/api/v1/me", ada.Session, ada.Session.CsrfToken, new { username = " Delete-Ada " }))
        {
            Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
            var cleared = deleted.Headers.GetValues("Set-Cookie").ToList();
            Assert.Contains(cleared, cookie => cookie.StartsWith(SessionCookies.SessionName + "=;", StringComparison.Ordinal));
            Assert.Contains(cleared, cookie => cookie.StartsWith(SessionCookies.CsrfName + "=;", StringComparison.Ordinal));
        }

        var after = await RowsNamingAsync(ada.Id);
        Assert.True(after.Count == 0, $"Rows still name the deleted user: {string.Join(", ", after.Keys)}");
        Assert.Equal(otherBefore, await RowsNamingAsync(other.Id));

        using (var me = await SendAsync(HttpMethod.Get, "/api/v1/me", ada.Session))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
        }

        using var otherMe = await SendAsync(HttpMethod.Get, "/api/v1/me", other.Session);
        Assert.Equal(HttpStatusCode.OK, otherMe.StatusCode);
    }

    [Fact]
    public async Task Deleting_WithoutTheCsrfToken_IsRefused_AndErasesNothing()
    {
        var ada = await SeedAsync("csrf-ada");
        var before = await RowsNamingAsync(ada.Id);

        using var missing = await SendAsync(HttpMethod.Delete, "/api/v1/me", ada.Session, csrf: null, new { username = "csrf-ada" });
        using var wrong = await SendAsync(HttpMethod.Delete, "/api/v1/me", ada.Session, csrf: "not-the-token", new { username = "csrf-ada" });

        Assert.Equal(HttpStatusCode.Forbidden, missing.StatusCode);
        Assert.Equal(CsrfProtectionMiddleware.InvalidCode, await CodeAsync(missing));
        Assert.Equal(HttpStatusCode.Forbidden, wrong.StatusCode);
        Assert.Equal(before, await RowsNamingAsync(ada.Id));
    }

    [Theory]
    [InlineData("someone-else")]
    [InlineData("")]
    [InlineData(null)]
    public async Task Deleting_WithAUsernameThatIsNotTheCallers_IsAValidationFailure_AndErasesNothing(string? username)
    {
        var ada = await SeedAsync($"confirm-{Guid.CreateVersion7():N}"[..20]);
        var before = await RowsNamingAsync(ada.Id);

        using var response = await SendAsync(HttpMethod.Delete, "/api/v1/me", ada.Session, ada.Session.CsrfToken, new { username });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal("validation_failed", body.RootElement.GetProperty("code").GetString());
        Assert.True(body.RootElement.GetProperty("errors").TryGetProperty("username", out _));
        Assert.Equal(before, await RowsNamingAsync(ada.Id));
    }

    [Fact]
    public async Task Deleting_WithNoBody_IsARequestFailure_AndErasesNothing()
    {
        var ada = await SeedAsync("nobody-ada");
        var before = await RowsNamingAsync(ada.Id);

        using var response = await SendAsync(HttpMethod.Delete, "/api/v1/me", ada.Session, ada.Session.CsrfToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(before, await RowsNamingAsync(ada.Id));
    }

    [Fact]
    public async Task SignedOut_NeitherEndpointAnswers()
    {
        using var export = await SendAsync(HttpMethod.Get, "/api/v1/me/export", session: null);
        using var delete = await SendAsync(HttpMethod.Delete, "/api/v1/me", session: null, body: new { username = "x" });

        Assert.Equal(HttpStatusCode.Unauthorized, export.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, delete.StatusCode);
    }

    private const string RevealedTask = "revealed-task";

    private const string HiddenTask = "hidden-task";

    private sealed record Seeded(Guid Id, string Email, IssuedSession Session);

    /// <summary>A user with a row in every table that can name one: linked account, session, two attempts, a signal, a reveal and a study mark.</summary>
    private async Task<Seeded> SeedAsync(string username)
    {
        var id = Guid.CreateVersion7();
        var email = $"{username}@example.test";
        var now = DateTimeOffset.UtcNow;
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var scope = api.Services.CreateAsyncScope();

        var users = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
        users.Users.Add(new User { Id = id, Email = email, Username = username, CreatedAt = now });
        await users.SaveChangesAsync(cancellationToken);

        var auth = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        auth.LinkedAccounts.Add(LinkedAccount.Create(id, IdentityProvider.GitHub, $"gh-{id:N}", username, now));
        await auth.SaveChangesAsync(cancellationToken);

        var session = await scope.ServiceProvider.GetRequiredService<ISessionIssuer>().StartAsync(id, cancellationToken);

        var revealed = Submitted(id, RevealedTask, now);
        var hidden = Submitted(id, HiddenTask, now);
        var attempts = scope.ServiceProvider.GetRequiredService<AttemptsDbContext>();
        attempts.Attempts.AddRange(revealed, hidden);
        attempts.Reveals.Add(Reveal.Of(revealed, now));
        attempts.Signals.Add(Signal.Send(revealed, "secrets-in-repo", "a comment", now));
        attempts.StudyMarks.Add(StudyMark.Of(id, "secrets-in-repo", now));
        await attempts.SaveChangesAsync(cancellationToken);

        return new Seeded(id, email, session);
    }

    private static Attempt Submitted(Guid userId, string task, DateTimeOffset now)
    {
        var attempt = Attempt.Start(userId, task, now);
        attempt.Submit(
            now,
            "first",
            """{"picks":[]}""",
            """{"total":0,"maximum":30,"isCorrect":false,"cards":[]}""",
            """{"lesson":"key"}""",
            0,
            30,
            countsTowardProgress: true);

        return attempt;
    }

    /// <summary>
    /// For every table of every module schema, how many of its rows hold <paramref name="userId"/> in any
    /// <c>uuid</c> column; tables with none are left out. Read from the catalogue, not from a list.
    /// </summary>
    private async Task<Dictionary<string, long>> RowsNamingAsync(Guid userId)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var counts = new Dictionary<string, long>(StringComparer.Ordinal);

        await using var connection = new NpgsqlConnection(api.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        foreach (var (schema, table, column) in await UuidColumnsAsync(connection))
        {
            await using var count = new NpgsqlCommand($"""SELECT count(*) FROM "{schema}"."{table}" WHERE "{column}" = @id""", connection);
            count.Parameters.AddWithValue("id", userId);
            var rows = (long)(await count.ExecuteScalarAsync(cancellationToken))!;

            if (rows > 0)
            {
                var key = $"{schema}.{table}";
                counts[key] = counts.GetValueOrDefault(key) + rows;
            }
        }

        return counts;
    }

    /// <summary>The tables that hold a user: every one with a <c>user_id</c> column, and the users table itself.</summary>
    private async Task<IReadOnlyList<string>> UserTablesAsync()
    {
        await using var connection = new NpgsqlConnection(api.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        var tables = (await UuidColumnsAsync(connection))
            .Where(column => column.Column == "user_id")
            .Select(column => $"{column.Schema}.{column.Table}")
            .Append("users.users")
            .Distinct()
            .ToList();

        // Every table SPEC names as a learner's: if the catalogue read found fewer, it read the wrong thing.
        Assert.Superset(
            new HashSet<string>(StringComparer.Ordinal) { "users.users", "auth.linked_accounts", "auth.sessions", "attempts.attempts", "attempts.signals", "attempts.reveals", "attempts.study_marks" },
            tables.ToHashSet(StringComparer.Ordinal));

        return tables;
    }

    private static async Task<List<(string Schema, string Table, string Column)>> UuidColumnsAsync(NpgsqlConnection connection)
    {
        await using var command = new NpgsqlCommand(
            """
            SELECT c.table_schema, c.table_name, c.column_name
            FROM information_schema.columns c
            JOIN information_schema.tables t ON t.table_schema = c.table_schema AND t.table_name = c.table_name
            WHERE c.table_schema = ANY(@schemas) AND c.data_type = 'uuid' AND t.table_type = 'BASE TABLE'
            ORDER BY 1, 2, 3
            """,
            connection);
        command.Parameters.AddWithValue("schemas", ModuleSchemas.All.ToArray());

        var columns = new List<(string, string, string)>();
        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);

        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            columns.Add((reader.GetString(0), reader.GetString(1), reader.GetString(2)));
        }

        Assert.NotEmpty(columns);
        return columns;
    }

    private Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, IssuedSession? session, string? csrf = null, object? body = null)
    {
        var request = new HttpRequestMessage(method, path);

        if (session is not null)
        {
            request.Headers.Add("Cookie", $"{SessionCookies.SessionName}={session.Token}");
        }

        if (csrf is not null)
        {
            request.Headers.Add(CsrfProtectionMiddleware.HeaderName, csrf);
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return api.Client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static async Task<string?> CodeAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetProperty("code").GetString();
}
