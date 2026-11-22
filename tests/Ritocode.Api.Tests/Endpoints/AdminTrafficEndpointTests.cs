using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Ritocode.Api.Admin;
using Ritocode.Api.Tests.Infrastructure;
using Ritocode.TestSupport;

namespace Ritocode.Api.Tests.Endpoints;

/// <summary>
/// The admin overview's traffic panel (#306) for an admin, against a fake Umami that answers as Umami 3 does.
/// A non-admin's and a signed-out caller's answers are in <see cref="NonAdminEndpointTests"/> and
/// <see cref="AnonymousAdminEndpointTests"/>.
/// </summary>
public sealed class AdminTrafficEndpointTests(TrafficTestApi api) : IClassFixture<TrafficTestApi>
{
    [Fact]
    public async Task TheFigures_AreUmamisForTheRange_AndTheFunnelHasEveryStepInOrder()
    {
        api.Umami.Answer = FakeUmami.Figures;

        using var report = await GetJsonAsync("/api/v1/admin/stats/traffic?from=2026-03-01&to=2026-03-07", HttpStatusCode.OK);
        var root = report.RootElement;

        Assert.Equal("ok", root.GetProperty("status").GetString());
        Assert.Equal("2026-03-01", root.GetProperty("from").GetString());
        Assert.Equal("2026-03-07", root.GetProperty("to").GetString());
        Assert.Equal($"https://stats.example.test/websites/{TrafficTestApi.WebsiteId}", root.GetProperty("dashboardUrl").GetString());

        var summary = root.GetProperty("summary");
        Assert.Equal([12L, 15L, 40L], [summary.GetProperty("visitors").GetInt64(), summary.GetProperty("visits").GetInt64(), summary.GetProperty("pageViews").GetInt64()]);

        // A referrer without a name (a direct visit) is left out.
        Assert.Equal(["news.ycombinator.com:9", "t.me:2"], Counts(root, "referrers"));
        Assert.Equal(["/:20", "/tasks/money-floats:11"], Counts(root, "pages"));
        // signal-sent is not a step; check-signed-out was never sent, so it is 0.
        Assert.Equal(
            ["task-opened:30", "step-2-reached:18", "check-signed-out:0", "sign-in-completed:4", "attempt-submitted:3"],
            Counts(root, "funnel"));

        // 1 March 00:00 UTC to the last millisecond of 7 March, with the token, for the site.
        var requests = api.Umami.Requests.Where(request => request.Contains("2026-03-01", StringComparison.Ordinal)).ToList();
        Assert.Equal(4, requests.Count);
        Assert.All(requests, request =>
        {
            Assert.Contains($"/api/websites/{TrafficTestApi.WebsiteId}/", request, StringComparison.Ordinal);
            Assert.Contains("startAt=1772323200000&endAt=1772927999999", request, StringComparison.Ordinal);
            Assert.Contains("Bearer umami-token", request, StringComparison.Ordinal);
        });
        Assert.Contains(requests, request => request.Contains("/stats?", StringComparison.Ordinal));
        Assert.Contains(requests, request => request.Contains("type=referrer", StringComparison.Ordinal));
        Assert.Contains(requests, request => request.Contains("type=path", StringComparison.Ordinal));
        Assert.Contains(requests, request => request.Contains("type=event", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Umami2sFigures_AsValueObjects_AreReadToo()
    {
        api.Umami.Answer = path => path.Contains("/stats", StringComparison.Ordinal)
            ? (HttpStatusCode.OK, """{"pageviews":{"value":8,"prev":1},"visitors":{"value":3,"prev":0},"visits":{"value":4,"prev":0}}""")
            : (HttpStatusCode.OK, "[]");

        using var report = await GetJsonAsync("/api/v1/admin/stats/traffic", HttpStatusCode.OK);
        var summary = report.RootElement.GetProperty("summary");

        Assert.Equal(3, summary.GetProperty("visitors").GetInt64());
        Assert.Equal(8, summary.GetProperty("pageViews").GetInt64());
        Assert.All(Counts(report.RootElement, "funnel"), step => Assert.EndsWith(":0", step, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable, "failing")]
    [InlineData(HttpStatusCode.Unauthorized, "refused")]
    [InlineData(HttpStatusCode.Forbidden, "refused")]
    public async Task UmamisErrors_AreThePanelsState_NotAnError(HttpStatusCode umami, string status)
    {
        api.Umami.Answer = path => path.Contains("type=path", StringComparison.Ordinal) ? (umami, "{}") : FakeUmami.Figures(path);

        using var report = await GetJsonAsync("/api/v1/admin/stats/traffic", HttpStatusCode.OK);

        AssertEmpty(report.RootElement, status);
    }

    [Fact]
    public async Task UmamiDown_IsFailing_NotAnError()
    {
        api.Umami.Answer = _ => throw new HttpRequestException("Connection refused.");

        using var report = await GetJsonAsync("/api/v1/admin/stats/traffic", HttpStatusCode.OK);

        AssertEmpty(report.RootElement, "failing");
    }

    [Fact]
    public async Task SomethingThatIsNotUmamisAnswer_IsFailing()
    {
        api.Umami.Answer = path => path.Contains("/stats", StringComparison.Ordinal) ? (HttpStatusCode.OK, "<html>a proxy's page</html>") : (HttpStatusCode.OK, "[]");

        using var report = await GetJsonAsync("/api/v1/admin/stats/traffic", HttpStatusCode.OK);

        AssertEmpty(report.RootElement, "failing");
    }

    [Theory]
    [InlineData("from=2026-03-14&to=2026-03-01", "from")]
    [InlineData("from=2024-01-01&to=2026-03-01", "from")]
    [InlineData("to=yesterday", "to")]
    public async Task ABadRange_IsAValidationError_NamingTheParameter(string query, string parameter)
    {
        using var problem = await GetJsonAsync($"/api/v1/admin/stats/traffic?{query}", HttpStatusCode.BadRequest);

        Assert.True(problem.RootElement.GetProperty("errors").TryGetProperty(parameter, out _));
    }

    private static void AssertEmpty(JsonElement root, string status)
    {
        Assert.Equal(status, root.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("summary").ValueKind);
        Assert.Empty(root.GetProperty("referrers").EnumerateArray());
        Assert.Empty(root.GetProperty("funnel").EnumerateArray());
        // The link to Umami's own pages stays: that is where to look next.
        Assert.False(string.IsNullOrEmpty(root.GetProperty("dashboardUrl").GetString()));
    }

    private static string[] Counts(JsonElement root, string list) =>
        [.. root.GetProperty(list).EnumerateArray().Select(row => $"{row.GetProperty("name").GetString()}:{row.GetProperty("count").GetInt64()}")];

    private async Task<JsonDocument> GetJsonAsync(string path, HttpStatusCode expected)
    {
        var response = await api.Client.GetAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken);
        Assert.Equal(expected, response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }
}

/// <summary>Umami not configured — no address, token or site id: the panel says so, and nothing is asked.</summary>
public sealed class AdminTrafficNotConfiguredTests(AdminContentTestApi api) : IClassFixture<AdminContentTestApi>
{
    [Fact]
    public async Task WithoutUmami_ThePanelIsNotConfigured()
    {
        var response = await api.Client.GetAsync(new Uri("/api/v1/admin/stats/traffic", UriKind.Relative), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var report = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal("notConfigured", report.RootElement.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, report.RootElement.GetProperty("summary").ValueKind);
        Assert.Equal(JsonValueKind.Null, report.RootElement.GetProperty("dashboardUrl").ValueKind);
    }
}

/// <summary>The development identity as an admin, with Umami configured and answered by <see cref="FakeUmami"/>.</summary>
public sealed class TrafficTestApi(PostgresTestServer postgres) : IAsyncLifetime
{
    public const string WebsiteId = "5f0c1d2e-0000-4000-8000-000000000306";

    private TestApiHost? _host;

    public FakeUmami Umami { get; } = new();

    public HttpClient Client => _host!.Client;

    public async ValueTask InitializeAsync()
    {
        var connectionString = await postgres.CreateDatabaseAsync(nameof(TrafficTestApi), TestContext.Current.CancellationToken);

        _host = await TestApiHost.StartAsync(
            connectionString,
            developmentIdentityEnabled: true,
            new Dictionary<string, string?>
            {
                ["Users:Admin:Emails:0"] = "developer@ritocode.local",
                ["System:UmamiUrl"] = "http://umami.test:3000",
                ["Traffic:UmamiToken"] = " umami-token ",
                ["Traffic:UmamiWebsiteId"] = WebsiteId,
                ["Traffic:UmamiDashboardUrl"] = "https://stats.example.test",
            },
            services => services.AddHttpClient(TrafficReader.UmamiClient).ConfigurePrimaryHttpMessageHandler(() => Umami));
    }

    public async ValueTask DisposeAsync()
    {
        if (_host is not null)
        {
            await _host.DisposeAsync();
        }

        Umami.Dispose();
    }
}

/// <summary>Stands in for Umami's API: answers each request with <see cref="Answer"/> and records it.</summary>
public sealed class FakeUmami : HttpMessageHandler
{
    /// <summary>The path and query asked, and the answer: a status and a body.</summary>
    public Func<string, (HttpStatusCode Status, string Body)> Answer { get; set; } = Figures;

    /// <summary>Each request as <c>path?query Authorization</c>, the range's dates added for a test to find its own.</summary>
    public ConcurrentQueue<string> Requests { get; } = new();

    /// <summary>What a site with some traffic answers, as Umami 3 shapes it.</summary>
    public static (HttpStatusCode, string) Figures(string path) => path switch
    {
        _ when path.Contains("/stats", StringComparison.Ordinal) =>
            (HttpStatusCode.OK, """{"pageviews":40,"visitors":12,"visits":15,"bounces":3,"totaltime":900,"comparison":{"pageviews":0,"visitors":0,"visits":0,"bounces":0,"totaltime":0}}"""),
        _ when path.Contains("type=referrer", StringComparison.Ordinal) =>
            (HttpStatusCode.OK, """[{"x":"news.ycombinator.com","y":9},{"x":null,"y":5},{"x":"t.me","y":2}]"""),
        _ when path.Contains("type=path", StringComparison.Ordinal) =>
            (HttpStatusCode.OK, """[{"x":"/","y":20},{"x":"/tasks/money-floats","y":11}]"""),
        _ when path.Contains("type=event", StringComparison.Ordinal) =>
            (HttpStatusCode.OK, """[{"x":"task-opened","y":30},{"x":"signal-sent","y":7},{"x":"step-2-reached","y":18},{"x":"attempt-submitted","y":3},{"x":"sign-in-completed","y":4}]"""),
        _ => (HttpStatusCode.NotFound, "{}"),
    };

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri!.PathAndQuery;
        Requests.Enqueue($"{path} {Dates(path)} {request.Headers.Authorization}");

        var (status, body) = Answer(path);
        return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
    }

    /// <summary>The UTC date of the <c>startAt</c> in a query, so a test can pick its own requests out.</summary>
    private static string Dates(string path)
    {
        var start = path.IndexOf("startAt=", StringComparison.Ordinal);
        if (start < 0)
        {
            return string.Empty;
        }

        var digits = new string([.. path[(start + "startAt=".Length)..].TakeWhile(char.IsAsciiDigit)]);
        return long.TryParse(digits, out var milliseconds)
            ? DateTimeOffset.FromUnixTimeMilliseconds(milliseconds).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)
            : string.Empty;
    }
}
