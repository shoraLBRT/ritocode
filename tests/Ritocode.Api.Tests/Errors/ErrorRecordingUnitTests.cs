using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Ritocode.Modules.Errors;
using Ritocode.Modules.Errors.Alerts;
using Ritocode.Modules.Errors.Domain;
using Ritocode.Modules.Errors.Recording;

namespace Ritocode.Api.Tests.Errors;

/// <summary>What makes two errors one group, and what the Telegram message says (#294).</summary>
public sealed class FingerprintTests
{
    [Fact]
    public void ServerFrames_AreTheMethodsAlone_SoAnEditElsewhereInTheFileKeepsTheGroup()
    {
        const string before = "   at Ritocode.Modules.Attempts.Lifecycle.AttemptLifecycle.StartAsync(Guid id) in /src/AttemptLifecycle.cs:line 40\n"
            + "   at Ritocode.Modules.Attempts.AttemptEndpoints.StartAsync() in /src/AttemptEndpoints.cs:line 12";
        const string after = "   at Ritocode.Modules.Attempts.Lifecycle.AttemptLifecycle.StartAsync(Guid id) in /src/AttemptLifecycle.cs:line 52\n"
            + "   at Ritocode.Modules.Attempts.AttemptEndpoints.StartAsync() in /src/AttemptEndpoints.cs:line 13";

        Assert.Equal(
            Fingerprint.Of(ErrorSource.Server, "System.InvalidOperationException", "Attempt 1 missing", before),
            Fingerprint.Of(ErrorSource.Server, "System.InvalidOperationException", "Attempt 2 missing", after));

        Assert.NotEqual(
            Fingerprint.Of(ErrorSource.Server, "System.InvalidOperationException", "x", before),
            Fingerprint.Of(ErrorSource.Server, "System.ArgumentException", "x", before));
    }

    [Fact]
    public void BrowserFrames_AreTheFunctionNames_SoANewBundleKeepsTheGroup_AndTheMessageTellsErrorsApart()
    {
        const string chrome = "TypeError: x\n    at renderTask (https://ritocode.ru/assets/index-abc.js:1:200)\n    at https://ritocode.ru/assets/index-abc.js:1:9";
        const string nextRelease = "TypeError: x\n    at renderTask (https://ritocode.ru/assets/index-def.js:1:310)";
        const string firefox = "renderTask@https://ritocode.ru/assets/index-xyz.js:1:5";

        var one = Fingerprint.Of(ErrorSource.Browser, "TypeError", "Item 12 is undefined", chrome);
        Assert.Equal(one, Fingerprint.Of(ErrorSource.Browser, "TypeError", "Item 13 is undefined", nextRelease));
        Assert.Equal(one, Fingerprint.Of(ErrorSource.Browser, "TypeError", "Item 1 is undefined", firefox));
        Assert.NotEqual(one, Fingerprint.Of(ErrorSource.Browser, "TypeError", "Something else", chrome));
        Assert.Equal(["renderTask"], Fingerprint.Frames(ErrorSource.Browser, chrome));
    }

    [Theory]
    [InlineData("/tasks/a?q=secret#x", "/tasks/a")]
    [InlineData("https://evil.test/x", "unknown")]
    [InlineData("//evil.test/x", "unknown")]
    [InlineData(null, "unknown")]
    public void ThePage_IsAPathOnly(string? page, string expected) => Assert.Equal(expected, ErrorReports.Path(page));

    [Fact]
    public void TheApisCommit_IsWhatFollowsThePlus() => Assert.Equal("abc123", ErrorReports.CommitOf("0.1.0+abc123"));
}

public sealed class TelegramErrorAlertsTests
{
    private static readonly ErrorAlert Alert = new(ErrorAlertKind.Reopened, ErrorSource.Browser, "TypeError\nInjected", "/tasks/a", 7);

    [Fact]
    public void TheMessage_NamesTheGroup_NeverItsMessage()
    {
        var text = TelegramErrorAlerts.Text(Alert, "https://ritocode.test/");

        Assert.Equal("Ritocode: reopened browser error\nTypeErrorInjected\n/tasks/a\nOccurrences: 7\nhttps://ritocode.test/admin/errors", text);
    }

    [Fact]
    public async Task Configured_ItPostsToTheBotsChat()
    {
        var handler = new RecordingHandler();
        var alerts = Create(handler, new TelegramOptions { BotToken = "123:abc", ChatId = "42", ApiBaseUrl = new Uri("https://telegram.test/") });

        await alerts.SendAsync(Alert, TestContext.Current.CancellationToken);

        var (uri, body) = Assert.Single(handler.Requests);
        Assert.Equal("https://telegram.test/bot123:abc/sendMessage", uri.ToString());
        using var json = JsonDocument.Parse(body);
        Assert.Equal("42", json.RootElement.GetProperty("chat_id").GetString());
        Assert.StartsWith("Ritocode: reopened", json.RootElement.GetProperty("text").GetString(), StringComparison.Ordinal);
        Assert.False(json.RootElement.TryGetProperty("parse_mode", out _));
    }

    [Fact]
    public async Task NotConfigured_NothingIsSent()
    {
        var handler = new RecordingHandler();

        await Create(handler, new TelegramOptions()).SendAsync(Alert, TestContext.Current.CancellationToken);

        Assert.Empty(handler.Requests);
    }

    private static TelegramErrorAlerts Create(HttpMessageHandler handler, TelegramOptions telegram) => new(
        new HttpClient(handler),
        Options.Create(new ErrorsOptions { Telegram = telegram }),
        NullLogger<TelegramErrorAlerts>.Instance);

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<(Uri Uri, string Body)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add((request.RequestUri!, await request.Content!.ReadAsStringAsync(cancellationToken)));
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }
}
