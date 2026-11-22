using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ritocode.Api.Tests.Infrastructure;
using Ritocode.Modules.Errors.Alerts;
using Ritocode.Modules.Errors.Domain;
using Ritocode.Modules.Errors.Persistence;
using Ritocode.Modules.Errors.Recording;
using Ritocode.Shared.Http;

namespace Ritocode.Api.Tests.Endpoints;

/// <summary>
/// The error records (ADR 0020 decision 1, #294): a server exception and a browser report each land in their
/// group, a repeat counts rather than duplicates, a resolved group that comes back reopens, and a new or
/// reopened group raises one alert.
/// </summary>
public sealed class ErrorReportingTests(ErrorsTestApi api) : IClassFixture<ErrorsTestApi>
{
    private const string ProbeLocation = "GET /__probe/unhandled";

    [Fact]
    public async Task AnEndpointThatThrows_IsRecordedAndGrouped_AndASecondThrowCounts()
    {
        var before = await ProbeGroupAsync();
        var alertsBefore = api.Alerts.Raised.Count;

        var first = await api.Client.GetAsync(new Uri("/__probe/unhandled", UriKind.Relative), TestContext.Current.CancellationToken);
        var second = await api.Client.GetAsync(new Uri("/__probe/unhandled", UriKind.Relative), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.InternalServerError, first.StatusCode);
        Assert.Equal(HttpStatusCode.InternalServerError, second.StatusCode);

        var group = await ProbeGroupAsync();
        Assert.NotNull(group);
        Assert.Equal(ErrorSource.Server, group.Source);
        Assert.Equal("System.InvalidOperationException", group.Type);
        Assert.Equal("probe failure", group.Message);
        Assert.Equal((before?.Count ?? 0) + 2, group.Count);

        await using var scope = api.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ErrorsDbContext>();
        Assert.Equal(1, await context.Groups.CountAsync(row => row.Location == ProbeLocation, TestContext.Current.CancellationToken));

        var kept = await context.Occurrences.Where(row => row.GroupId == group.Id).Select(row => row.RequestId).ToListAsync(TestContext.Current.CancellationToken);
        Assert.Contains(first.Headers.GetValues(RequestId.HeaderName).Single(), kept);
        Assert.Contains(second.Headers.GetValues(RequestId.HeaderName).Single(), kept);

        // One alert for a new group; none for an occurrence that only counts.
        var raised = api.Alerts.Raised.Skip(alertsBefore).Where(alert => alert.Location == ProbeLocation).ToList();
        Assert.Equal(before is null ? 1 : 0, raised.Count);
        Assert.All(raised, alert => Assert.Equal(ErrorAlertKind.New, alert.Kind));
    }

    [Fact]
    public async Task AGroup_KeepsItsLatestOccurrencesOnly_AndItsWholeCount()
    {
        for (var i = 0; i < ErrorsTestApi.OccurrencesPerGroup + 2; i++)
        {
            await api.Client.GetAsync(new Uri("/__probe/unhandled", UriKind.Relative), TestContext.Current.CancellationToken);
        }

        var group = await ProbeGroupAsync();
        Assert.NotNull(group);
        Assert.True(group.Count >= ErrorsTestApi.OccurrencesPerGroup + 2);

        await using var scope = api.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ErrorsDbContext>();
        Assert.Equal(ErrorsTestApi.OccurrencesPerGroup, await context.Occurrences.CountAsync(row => row.GroupId == group.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AnOccurrenceOlderThanTheRetention_IsPruned()
    {
        var groupId = await RecordAsync($"old {Guid.NewGuid()}");

        await using (var scope = api.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ErrorsDbContext>();
            await context.Occurrences.Where(row => row.GroupId == groupId)
                .ExecuteUpdateAsync(set => set.SetProperty(row => row.OccurredAt, DateTimeOffset.UtcNow.AddDays(-31)), TestContext.Current.CancellationToken);
        }

        var pruned = await api.Services.GetRequiredService<ErrorRecorder>().PruneExpiredAsync(TestContext.Current.CancellationToken);
        Assert.True(pruned >= 1);

        await using var after = api.Services.CreateAsyncScope();
        var errors = after.ServiceProvider.GetRequiredService<ErrorsDbContext>();
        Assert.False(await errors.Occurrences.AnyAsync(row => row.GroupId == groupId, TestContext.Current.CancellationToken));

        // The group stays: only occurrences are pruned.
        Assert.True(await errors.Groups.AnyAsync(row => row.Id == groupId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ABrowserReport_IsRecordedWithThePagesPathOnly_AndItsOwnRequestId()
    {
        var message = $"Cannot read properties of undefined {Guid.NewGuid()}";
        using var response = await ReportAsync(new
        {
            type = "TypeError",
            message,
            stack = "TypeError: boom\n    at renderTask (https://ritocode.test/assets/index-abc123.js:1:2345)",
            page = "/tasks/invoice-mailer?draft=what-the-learner-typed#answer",
            commit = "0123abc",
        });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var requestId = response.Headers.GetValues(RequestId.HeaderName).Single();

        await using var scope = api.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ErrorsDbContext>();
        var group = await context.Groups.SingleAsync(row => row.Message == message, TestContext.Current.CancellationToken);
        var occurrence = await context.Occurrences.SingleAsync(row => row.GroupId == group.Id, TestContext.Current.CancellationToken);

        Assert.Equal(ErrorSource.Browser, group.Source);
        Assert.Equal("TypeError", group.Type);
        Assert.Equal("/tasks/invoice-mailer", group.Location);
        Assert.Equal(requestId, occurrence.RequestId);
        Assert.Equal("0123abc", occurrence.Commit);
        Assert.Contains("renderTask", occurrence.Stack, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AResolvedGroupThatComesBack_Reopens_AndAlertsOnce()
    {
        var message = $"reopen me {Guid.NewGuid()}";
        var groupId = await RecordAsync(message);

        using (var resolved = await api.Client.PostAsync(new Uri($"/api/v1/admin/errors/{groupId}/resolve", UriKind.Relative), null, TestContext.Current.CancellationToken))
        {
            Assert.Equal(HttpStatusCode.OK, resolved.StatusCode);
            using var body = JsonDocument.Parse(await resolved.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            Assert.Equal(JsonValueKind.String, body.RootElement.GetProperty("resolvedAt").ValueKind);
        }

        Assert.Empty(await ListedAsync("open", groupId));
        Assert.Single(await ListedAsync("resolved", groupId));

        Assert.Equal(groupId, await RecordAsync(message));

        var listed = Assert.Single(await ListedAsync("open", groupId));
        Assert.Equal(2, listed.GetProperty("count").GetInt64());
        Assert.Equal(JsonValueKind.Null, listed.GetProperty("resolvedAt").ValueKind);
        Assert.Equal(2, listed.GetProperty("latestRequestIds").GetArrayLength());

        var kinds = api.Alerts.Raised.Where(alert => alert.Type == "Error" && alert.Location == "/reopen").Select(alert => alert.Kind).ToList();
        Assert.Contains(ErrorAlertKind.New, kinds);
        Assert.Contains(ErrorAlertKind.Reopened, kinds);
    }

    [Fact]
    public async Task TheList_IsAPage_TheMostRecentlySeenFirst_AndADetailHasTheOccurrences()
    {
        var groupId = await RecordAsync($"listed {Guid.NewGuid()}");

        using var list = await GetJsonAsync("/api/v1/admin/errors?pageSize=1");
        var root = list.RootElement;
        Assert.Equal(1, root.GetProperty("pageNumber").GetInt32());
        Assert.Equal(1, root.GetProperty("pageSize").GetInt32());
        Assert.True(root.GetProperty("totalItems").GetInt64() >= 1);
        var first = Assert.Single(root.GetProperty("items").EnumerateArray());
        Assert.Equal(groupId.ToString(), first.GetProperty("id").GetString());
        Assert.Equal("browser", first.GetProperty("source").GetString());
        Assert.EndsWith("Z", first.GetProperty("lastSeenAt").GetString(), StringComparison.Ordinal);

        using var detail = await GetJsonAsync($"/api/v1/admin/errors/{groupId}");
        var occurrence = Assert.Single(detail.RootElement.GetProperty("occurrences").EnumerateArray());
        Assert.Equal("/reopen", occurrence.GetProperty("location").GetString());
        Assert.False(occurrence.TryGetProperty("userId", out _));
    }

    [Theory]
    [InlineData("/api/v1/admin/errors?status=nonsense&pageSize=0", HttpStatusCode.BadRequest, "validation_failed")]
    [InlineData("/api/v1/admin/errors/not-an-id", HttpStatusCode.NotFound, "error_group_not_found")]
    [InlineData("/api/v1/admin/errors/0192f000-0000-7000-8000-000000000000", HttpStatusCode.NotFound, "error_group_not_found")]
    public async Task ABadQueryOrAnUnknownGroup_IsRefusedInTheUnifiedBody(string path, HttpStatusCode status, string code)
    {
        using var response = await api.Client.GetAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken);
        Assert.Equal(status, response.StatusCode);

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(code, body.RootElement.GetProperty("code").GetString());
        Assert.False(string.IsNullOrEmpty(body.RootElement.GetProperty("requestId").GetString()));
    }

    [Fact]
    public async Task ABadQuery_NamesEveryBadParameter()
    {
        using var response = await api.Client.GetAsync(new Uri("/api/v1/admin/errors?status=nonsense&page=0", UriKind.Relative), TestContext.Current.CancellationToken);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        var fields = body.RootElement.GetProperty("errors");
        Assert.True(fields.TryGetProperty("status", out _));
        Assert.True(fields.TryGetProperty("page", out _));
    }

    [Fact]
    public async Task ABrowserReport_LargerThanTheCap_IsRefusedBeforeItIsRead()
    {
        using var response = await ReportAsync(new { type = "Error", message = new string('x', 20_000) });

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal("request_too_large", body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task ABrowserReport_ThatIsNotJson_IsRefused()
    {
        using var form = new StringContent("type=Error", Encoding.UTF8, "text/plain");
        using var notJson = await api.Client.PostAsync(new Uri("/api/v1/client-errors", UriKind.Relative), form, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, notJson.StatusCode);

        using var broken = new StringContent("{ not json", Encoding.UTF8, "application/json");
        using var invalid = await api.Client.PostAsync(new Uri("/api/v1/client-errors", UriKind.Relative), broken, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    private async Task<Guid> RecordAsync(string message)
    {
        using var response = await ReportAsync(new { type = "Error", message, page = "/reopen" });
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        await using var scope = api.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ErrorsDbContext>().Groups
            .Where(row => row.Message == message).Select(row => row.Id).SingleAsync(TestContext.Current.CancellationToken);
    }

    private Task<HttpResponseMessage> ReportAsync(object body) =>
        api.Client.PostAsJsonAsync(new Uri("/api/v1/client-errors", UriKind.Relative), body, TestContext.Current.CancellationToken);

    private async Task<JsonDocument> GetJsonAsync(string path)
    {
        var response = await api.Client.GetAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    private async Task<List<JsonElement>> ListedAsync(string status, Guid groupId)
    {
        using var list = await GetJsonAsync($"/api/v1/admin/errors?status={status}&pageSize=100");
        return [.. list.RootElement.GetProperty("items").EnumerateArray()
            .Where(item => item.GetProperty("id").GetString() == groupId.ToString())
            .Select(item => item.Clone())];
    }

    private async Task<ErrorGroup?> ProbeGroupAsync()
    {
        await using var scope = api.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ErrorsDbContext>().Groups.AsNoTracking()
            .SingleOrDefaultAsync(row => row.Location == ProbeLocation, TestContext.Current.CancellationToken);
    }
}

/// <summary>The browser endpoint is anonymous, so it is limited per address.</summary>
public sealed class ClientErrorRateLimitTests(RateLimitedErrorsTestApi api) : IClassFixture<RateLimitedErrorsTestApi>
{
    [Fact]
    public async Task ReportsPastTheLimit_AreRefused()
    {
        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 3; i++)
        {
            using var response = await api.Client.PostAsJsonAsync(
                new Uri("/api/v1/client-errors", UriKind.Relative), new { type = "Error", message = "flood" }, TestContext.Current.CancellationToken);
            statuses.Add(response.StatusCode);

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
                Assert.Equal("client_errors_rate_limited", body.RootElement.GetProperty("code").GetString());
            }
        }

        Assert.Equal([HttpStatusCode.Accepted, HttpStatusCode.Accepted, HttpStatusCode.TooManyRequests], statuses);
    }
}
