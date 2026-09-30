using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Ritocode.Api.Tests.Infrastructure;
using Ritocode.Shared.Http;
using Ritocode.Shared.Identity;

namespace Ritocode.Api.Tests.Endpoints;

/// <summary>
/// A request's log lines are found by its request id, and name the caller by user id and nothing
/// more (#33).
/// </summary>
public sealed class LoggingTests(TestApi api) : IClassFixture<TestApi>
{
    private const string HttpLoggingCategory = "Microsoft.AspNetCore.HttpLogging.HttpLoggingMiddleware";

    private static readonly Guid DevelopmentUser = Guid.Parse("0199aa00-0000-7000-8000-000000000001");

    [Fact]
    public async Task ARequestsLines_AreFoundByItsRequestId_AndNameTheUserById()
    {
        await GetAsync("/api/v1/me", "find-me-by-this-id");

        var lines = LinesOf("find-me-by-this-id");

        var summary = Assert.Single(lines, line => line.Category == HttpLoggingCategory);
        Assert.Equal("GET", summary.Fields["Method"]?.ToString());
        Assert.Equal("/api/v1/me", summary.Fields["Path"]?.ToString());
        Assert.Equal(200, Convert.ToInt32(summary.Fields["StatusCode"], System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(DevelopmentUser, summary.Scope[UserLogScopeMiddleware.LogPropertyName]);
    }

    [Fact]
    public async Task NoPersonalDataIsLogged_OnlyTheUserId()
    {
        await GetAsync("/api/v1/me", "personal-data-probe");

        // The response carries the username and the user row the e-mail; no line of the request may,
        // nor anything from its headers. SQL is logged in development, with its parameters masked.
        var text = string.Join('\n', LinesOf("personal-data-probe").Select(Describe));
        Assert.DoesNotContain("developer", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ritocode.local", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Cookie", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AFailingRequest_IsLoggedWithItsStatus()
    {
        await GetAsync("/api/v1/tasks/no-such-task", "a-failing-request");

        var summary = Assert.Single(LinesOf("a-failing-request"), line => line.Category == HttpLoggingCategory);
        Assert.Equal(404, Convert.ToInt32(summary.Fields["StatusCode"], System.Globalization.CultureInfo.InvariantCulture));
    }

    private async Task GetAsync(string path, string requestId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add(RequestId.HeaderName, requestId);
        using var response = await api.Client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.NotEqual(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    private List<LogLine> LinesOf(string requestId) =>
        [.. api.Services.GetRequiredService<CapturedLogs>().Lines
            .Where(line => line.Scope.TryGetValue(RequestId.LogPropertyName, out var id) && Equals(id, requestId))];

    private static string Describe(LogLine line) =>
        $"{line.Message} {JsonSerializer.Serialize(line.Fields.ToDictionary(pair => pair.Key, pair => pair.Value?.ToString()))}";
}

/// <summary>A signed-out request is logged with its request id and no user.</summary>
public sealed class AnonymousLoggingTests(AnonymousTestApi api) : IClassFixture<AnonymousTestApi>
{
    [Fact]
    public async Task ASignedOutRequest_HasNoUserId_AndIsLoggedThoughRefused()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/me");
        request.Headers.Add(RequestId.HeaderName, "anonymous-probe");
        using var response = await api.Client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var lines = api.Services.GetRequiredService<CapturedLogs>().Lines
            .Where(line => line.Scope.TryGetValue(RequestId.LogPropertyName, out var id) && Equals(id, "anonymous-probe"))
            .ToList();

        var summary = Assert.Single(lines, line => line.Category == "Microsoft.AspNetCore.HttpLogging.HttpLoggingMiddleware");
        Assert.Equal(401, Convert.ToInt32(summary.Fields["StatusCode"], System.Globalization.CultureInfo.InvariantCulture));
        Assert.All(lines, line => Assert.False(line.Scope.ContainsKey(UserLogScopeMiddleware.LogPropertyName)));
    }
}

/// <summary>What the production settings ask of the console: JSON, with the scopes, and the request summary on.</summary>
public sealed class ProductionLoggingSettingsTests
{
    [Fact]
    public void TheApiLogsJson_WithScopes_AndItsRequestSummaries()
    {
        // From the source tree, not the test output: the migrator's appsettings.json also reaches the
        // output through Ritocode.TestSupport, and which of the two lands there depends on build order.
        using var settings = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "Ritocode.Api", "appsettings.json")),
            new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip });
        var logging = settings.RootElement.GetProperty("Logging");

        Assert.Equal("json", logging.GetProperty("Console").GetProperty("FormatterName").GetString());
        Assert.True(logging.GetProperty("Console").GetProperty("FormatterOptions").GetProperty("IncludeScopes").GetBoolean());
        Assert.Equal("Information", logging.GetProperty("LogLevel").GetProperty("Microsoft.AspNetCore.HttpLogging").GetString());
    }

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Ritocode.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException($"No Ritocode.slnx above {AppContext.BaseDirectory}.");
    }
}
