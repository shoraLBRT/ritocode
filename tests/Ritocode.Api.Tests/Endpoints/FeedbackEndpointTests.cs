using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Ritocode.Api.Feedback;
using Ritocode.Api.Tests.Infrastructure;

namespace Ritocode.Api.Tests.Endpoints;

public sealed class FeedbackEndpointTests(FeedbackTestApi api) : IClassFixture<FeedbackTestApi>
{
    private static readonly Uri Feedback = new("/api/v1/feedback", UriKind.Relative);

    [Fact]
    public async Task Message_FromAnAnonymousVisitor_BecomesOneIssueWithTheLabel()
    {
        api.Issues.Next = null;
        var before = api.Issues.Created.Count;

        var response = await Send(new { message = "Кнопка «Проверить» не видна\nна телефоне", page = "/tasks/leaked-key" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var body = await Read(response);
        Assert.True(body.RootElement.GetProperty("number").GetInt32() > 0);

        Assert.Equal(before + 1, api.Issues.Created.Count);
        var issue = api.Issues.Created.Last();
        Assert.Equal("feedback", issue.Label);
        Assert.Equal("Feedback: Кнопка «Проверить» не видна", issue.Title);
        Assert.Contains("на телефоне", issue.Body, StringComparison.Ordinal);
        Assert.Contains("Page: `/tasks/leaked-key`", issue.Body, StringComparison.Ordinal);
        Assert.Contains("User: anonymous", issue.Body, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("", FeedbackEndpoints.EmptyCode)]
    [InlineData("   \n ", FeedbackEndpoints.EmptyCode)]
    public async Task EmptyMessage_IsRefused(string message, string code)
    {
        var before = api.Issues.Created.Count;

        var response = await Send(new { message });

        await AssertRefused(response, HttpStatusCode.BadRequest, code);
        Assert.Equal(before, api.Issues.Created.Count);
    }

    [Fact]
    public async Task TooLongMessage_IsRefused()
    {
        var response = await Send(new { message = new string('a', FeedbackTestApi.MaxLength + 1) });

        await AssertRefused(response, HttpStatusCode.BadRequest, FeedbackEndpoints.TooLongCode);
    }

    [Theory]
    [InlineData(FeedbackIssueOutcome.NotConfigured, HttpStatusCode.ServiceUnavailable, FeedbackEndpoints.NotConfiguredCode)]
    [InlineData(FeedbackIssueOutcome.Unreachable, HttpStatusCode.ServiceUnavailable, FeedbackEndpoints.UnreachableCode)]
    [InlineData(FeedbackIssueOutcome.Failed, HttpStatusCode.InternalServerError, FeedbackEndpoints.FailedCode)]
    public async Task TrackerFailure_IsAnsweredWithItsCode(FeedbackIssueOutcome outcome, HttpStatusCode status, string code)
    {
        api.Issues.Next = outcome;
        try
        {
            await AssertRefused(await Send(new { message = "hello" }), status, code);
        }
        finally
        {
            api.Issues.Next = null;
        }
    }

    [Fact]
    public void Body_PutsTheMessageInAFenceLongerThanItsBackticks()
    {
        var body = FeedbackEndpoints.Body("see ```code``` @someone", "javascript:alert(1)", null);

        Assert.StartsWith("````text\nsee ```code``` @someone\n````", body, StringComparison.Ordinal);
        Assert.Contains("Page: `unknown`", body, StringComparison.Ordinal);
    }

    private Task<HttpResponseMessage> Send(object request) =>
        api.Client.PostAsJsonAsync(Feedback, request, TestContext.Current.CancellationToken);

    internal static async Task<JsonDocument> Read(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

    internal static async Task AssertRefused(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        using var body = await Read(response);
        Assert.Equal(code, body.RootElement.GetProperty("code").GetString());
    }
}

public sealed class FeedbackLimitTests(FeedbackLimitTestApi api) : IClassFixture<FeedbackLimitTestApi>
{
    [Fact]
    public async Task MessagesOverTheLimit_FromOneAddress_AreRefused()
    {
        var uri = new Uri("/api/v1/feedback", UriKind.Relative);
        var cancellation = TestContext.Current.CancellationToken;

        for (var sent = 0; sent < api.MaxPerWindow; sent++)
        {
            var accepted = await api.Client.PostAsJsonAsync(uri, new { message = $"message {sent}" }, cancellation);
            Assert.Equal(HttpStatusCode.Created, accepted.StatusCode);
        }

        var refused = await api.Client.PostAsJsonAsync(uri, new { message = "one too many" }, cancellation);

        await FeedbackEndpointTests.AssertRefused(refused, HttpStatusCode.TooManyRequests, FeedbackEndpoints.RateLimitedCode);
        Assert.Equal(api.MaxPerWindow, api.Issues.Created.Count);
    }
}
