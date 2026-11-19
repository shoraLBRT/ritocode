using System.Buffers.Text;
using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Ritocode.Api.Admin;
using Ritocode.Api.Feedback;

namespace Ritocode.Api.Tests.Feedback;

/// <summary>
/// <see cref="GitHubFeedbackIssues"/> against <see cref="FakeGitHub"/>: the GitHub App's JWT, installation and
/// cached installation token (ADR 0019), and the personal token as before.
/// </summary>
public sealed class GitHubFeedbackIssuesTests : IDisposable
{
    private const string Repository = "owner/name";
    private const string AppId = "12345";

    private static readonly RSA AppKey = RSA.Create(2048);
    private static readonly FeedbackIssue Issue = new("Feedback: a title", "a body", "feedback");

    private readonly TestClock _clock = new(new DateTimeOffset(2026, 11, 18, 12, 0, 0, TimeSpan.Zero));
    private readonly FakeGitHub _github;
    private readonly HttpClient _http;
    private readonly GitHubAppCredential _credential;

    public GitHubFeedbackIssuesTests()
    {
        _github = new FakeGitHub(_clock);
        _http = new HttpClient(_github) { BaseAddress = new Uri("https://api.github.com/") };
        _credential = new GitHubAppCredential(_clock, NullLogger<GitHubAppCredential>.Instance);
    }

    [Fact]
    public async Task App_SignsAJwtTheAppsPublicKeyVerifies_AndOpensTheIssueWithTheInstallationToken()
    {
        var result = await Issues(App(AppKey.ExportRSAPrivateKeyPem())).CreateAsync(Issue, TestContext.Current.CancellationToken);

        Assert.Equal(new FeedbackIssueResult(FeedbackIssueOutcome.Created, 1), result);
        Assert.Equal(
            [
                "GET /repos/owner/name/installation",
                "POST /app/installations/42/access_tokens",
                "POST /repos/owner/name/issues",
            ],
            _github.Requests.Select(request => request.Line));

        var jwt = _github.Requests[0].Bearer;
        Assert.Equal(jwt, _github.Requests[1].Bearer);
        Assert.Equal("installation-1", _github.Requests[2].Bearer);

        var parts = jwt.Split('.');
        Assert.Equal(3, parts.Length);
        Assert.True(AppKey.VerifyData(
            Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}"),
            Base64Url.DecodeFromChars(parts[2]),
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1));

        using var header = JsonDocument.Parse(Base64Url.DecodeFromChars(parts[0]));
        Assert.Equal("RS256", header.RootElement.GetProperty("alg").GetString());
        Assert.Equal("JWT", header.RootElement.GetProperty("typ").GetString());

        using var payload = JsonDocument.Parse(Base64Url.DecodeFromChars(parts[1]));
        var now = _clock.GetUtcNow().ToUnixTimeSeconds();
        Assert.Equal(now - 60, payload.RootElement.GetProperty("iat").GetInt64());
        Assert.Equal(now + (9 * 60), payload.RootElement.GetProperty("exp").GetInt64());
        Assert.Equal(AppId, payload.RootElement.GetProperty("iss").GetString());
    }

    [Fact]
    public async Task InstallationToken_IsReusedForTheNextMessage_AndRenewedShortlyBeforeItExpires()
    {
        // An environment variable carries the key's line breaks as literal \n.
        var issues = Issues(App(AppKey.ExportRSAPrivateKeyPem().Replace("\n", "\\n", StringComparison.Ordinal)));
        var cancellationToken = TestContext.Current.CancellationToken;

        await issues.CreateAsync(Issue, cancellationToken);
        _clock.Advance(TimeSpan.FromMinutes(54));
        await issues.CreateAsync(Issue, cancellationToken);

        Assert.Equal(1, _github.Count("POST /app/installations/42/access_tokens"));
        Assert.All(_github.Issues, request => Assert.Equal("installation-1", request.Bearer));

        // The token lives an hour; five minutes before that it is replaced.
        _clock.Advance(TimeSpan.FromMinutes(2));
        var result = await issues.CreateAsync(Issue, cancellationToken);

        Assert.Equal(FeedbackIssueOutcome.Created, result.Outcome);
        Assert.Equal(2, _github.Count("GET /repos/owner/name/installation"));
        Assert.Equal(2, _github.Count("POST /app/installations/42/access_tokens"));
        Assert.Equal("installation-2", _github.Issues.Last().Bearer);
    }

    [Fact]
    public async Task InstallationToken_GitHubRejects_IsAskedForAgainByTheNextMessage()
    {
        var issues = Issues(App(AppKey.ExportRSAPrivateKeyPem()));
        var cancellationToken = TestContext.Current.CancellationToken;

        _github.Fail("POST /repos/owner/name/issues", HttpStatusCode.Unauthorized);
        Assert.Equal(FeedbackIssueOutcome.Failed, (await issues.CreateAsync(Issue, cancellationToken)).Outcome);

        _github.Fail("POST /repos/owner/name/issues", null);
        Assert.Equal(FeedbackIssueOutcome.Created, (await issues.CreateAsync(Issue, cancellationToken)).Outcome);
        Assert.Equal("installation-2", _github.Issues.Last().Bearer);
    }

    [Fact]
    public async Task PersonalToken_WithNoApp_IsSentAsBefore()
    {
        var result = await Issues(new FeedbackOptions { Repository = Repository, GitHubToken = "personal" })
            .CreateAsync(Issue, TestContext.Current.CancellationToken);

        Assert.Equal(FeedbackIssueOutcome.Created, result.Outcome);
        var request = Assert.Single(_github.Requests);
        Assert.Equal("POST /repos/owner/name/issues", request.Line);
        Assert.Equal("personal", request.Bearer);
    }

    [Fact]
    public async Task App_BothSet_IsPreferredToThePersonalToken()
    {
        var options = App(AppKey.ExportRSAPrivateKeyPem());
        var result = await Issues(new FeedbackOptions { Repository = Repository, GitHubApp = options.GitHubApp, GitHubToken = "personal" })
            .CreateAsync(Issue, TestContext.Current.CancellationToken);

        Assert.Equal(FeedbackIssueOutcome.Created, result.Outcome);
        Assert.Equal("installation-1", _github.Issues.Single().Bearer);
    }

    [Fact]
    public async Task Credential_HalfAnAppAndNoToken_IsNotConfigured()
    {
        var result = await Issues(new FeedbackOptions { Repository = Repository, GitHubApp = new FeedbackGitHubAppOptions { AppId = AppId } })
            .CreateAsync(Issue, TestContext.Current.CancellationToken);

        Assert.Equal(FeedbackIssueOutcome.NotConfigured, result.Outcome);
        Assert.Empty(_github.Requests);
    }

    [Fact]
    public async Task PrivateKey_Unreadable_FailsWithoutCallingGitHub()
    {
        var result = await Issues(App("-----BEGIN RSA PRIVATE KEY-----\nnot a key\n-----END RSA PRIVATE KEY-----"))
            .CreateAsync(Issue, TestContext.Current.CancellationToken);

        Assert.Equal(FeedbackIssueOutcome.Failed, result.Outcome);
        Assert.Empty(_github.Requests);
    }

    [Theory]
    [InlineData("GET /repos/owner/name/installation", HttpStatusCode.NotFound, FeedbackIssueOutcome.Failed)]
    [InlineData("POST /app/installations/42/access_tokens", HttpStatusCode.Forbidden, FeedbackIssueOutcome.Failed)]
    [InlineData("GET /repos/owner/name/installation", null, FeedbackIssueOutcome.Unreachable)]
    [InlineData("POST /app/installations/42/access_tokens", null, FeedbackIssueOutcome.Unreachable)]
    public async Task AppCall_GitHubRefusesOrIsUnreachable_IsTheExistingOutcome(string line, HttpStatusCode? status, FeedbackIssueOutcome expected)
    {
        _github.Fail(line, status ?? FakeGitHub.Unreachable);

        var result = await Issues(App(AppKey.ExportRSAPrivateKeyPem())).CreateAsync(Issue, TestContext.Current.CancellationToken);

        Assert.Equal(expected, result.Outcome);
        Assert.Empty(_github.Issues);
    }

    [Fact]
    public async Task CredentialCheck_AnAppGitHubSignsIn_IsOk()
    {
        var status = await Check(App(AppKey.ExportRSAPrivateKeyPem())).CheckAsync(TestContext.Current.CancellationToken);

        Assert.Equal(DependencyStatus.Ok, status);
        Assert.Equal(1, _github.Count("POST /app/installations/42/access_tokens"));
        Assert.Empty(_github.Issues);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(null)]
    public async Task CredentialCheck_GitHubRefusesOrIsUnreachable_IsFailing(HttpStatusCode? status)
    {
        _github.Fail("POST /app/installations/42/access_tokens", status ?? FakeGitHub.Unreachable);

        Assert.Equal(DependencyStatus.Failing, await Check(App(AppKey.ExportRSAPrivateKeyPem())).CheckAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CredentialCheck_NoApp_OrMessagesToTheLog_IsNotConfigured()
    {
        var tokenOnly = new FeedbackOptions { Repository = Repository, GitHubToken = "a-token" };
        var toTheLog = new FeedbackOptions { Sink = FeedbackSink.Log, Repository = Repository, GitHubApp = App(AppKey.ExportRSAPrivateKeyPem()).GitHubApp };

        Assert.Equal(DependencyStatus.NotConfigured, await Check(tokenOnly).CheckAsync(TestContext.Current.CancellationToken));
        Assert.Equal(DependencyStatus.NotConfigured, await Check(toTheLog).CheckAsync(TestContext.Current.CancellationToken));
        Assert.Equal(
            DependencyStatus.NotConfigured,
            await new FeedbackCredentialCheck(_http, Options.Create(App(AppKey.ExportRSAPrivateKeyPem()))).CheckAsync(TestContext.Current.CancellationToken));
        Assert.Empty(_github.Requests);
    }

    public void Dispose()
    {
        _http.Dispose();
        _github.Dispose();
        _credential.Dispose();
    }

    private GitHubFeedbackIssues Issues(FeedbackOptions options) =>
        new(_http, Options.Create(options), _credential, NullLogger<GitHubFeedbackIssues>.Instance);

    private FeedbackCredentialCheck Check(FeedbackOptions options) => new(_http, Options.Create(options), _credential);

    private static FeedbackOptions App(string privateKey) => new()
    {
        Repository = Repository,
        GitHubApp = new FeedbackGitHubAppOptions { AppId = AppId, PrivateKey = privateKey },
    };

    private sealed class TestClock(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }

    /// <summary>
    /// The three calls of a GitHub App: the installation of <c>owner/name</c> (42), an installation token that lives
    /// an hour (<c>installation-1</c>, <c>-2</c>, …), and the issue. A call can be made to fail.
    /// </summary>
    private sealed class FakeGitHub(TimeProvider clock) : HttpMessageHandler
    {
        /// <summary>Not a status: the call throws as if GitHub could not be reached.</summary>
        public const HttpStatusCode Unreachable = (HttpStatusCode)0;

        private readonly ConcurrentDictionary<string, HttpStatusCode> _failures = new();
        private int _tokens;
        private int _issues;

        public List<Recorded> Requests { get; } = [];

        public IEnumerable<Recorded> Issues => Requests.Where(request => request.Line == "POST /repos/owner/name/issues");

        public int Count(string line) => Requests.Count(request => request.Line == line);

        public void Fail(string line, HttpStatusCode? status)
        {
            if (status is { } failure)
            {
                _failures[line] = failure;
            }
            else
            {
                _failures.TryRemove(line, out _);
            }
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var line = $"{request.Method} {request.RequestUri!.AbsolutePath}";
            Assert.Equal("api.github.com", request.RequestUri.Host);
            Requests.Add(new Recorded(line, request.Headers.Authorization?.Parameter ?? string.Empty));

            if (_failures.TryGetValue(line, out var failure))
            {
                return failure == Unreachable
                    ? throw new HttpRequestException("GitHub is unreachable.")
                    : Task.FromResult(new HttpResponseMessage(failure));
            }

            return Task.FromResult(line switch
            {
                "GET /repos/owner/name/installation" => Json(HttpStatusCode.OK, new { id = 42 }),
                "POST /app/installations/42/access_tokens" => Json(HttpStatusCode.Created, new
                {
                    token = $"installation-{Interlocked.Increment(ref _tokens)}",
                    expires_at = clock.GetUtcNow().AddHours(1),
                }),
                "POST /repos/owner/name/issues" => Json(HttpStatusCode.Created, new { number = Interlocked.Increment(ref _issues) }),
                _ => new HttpResponseMessage(HttpStatusCode.NotFound),
            });
        }

        private static HttpResponseMessage Json(HttpStatusCode status, object body) =>
            new(status) { Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json") };
    }

    private sealed record Recorded(string Line, string Bearer);
}
