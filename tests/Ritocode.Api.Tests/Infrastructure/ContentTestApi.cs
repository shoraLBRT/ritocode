using Microsoft.Extensions.DependencyInjection;
using Ritocode.Modules.Content.Ingest;
using Ritocode.TestSupport;

namespace Ritocode.Api.Tests.Infrastructure;

/// <summary>
/// The host under the development identity, over a database holding ingested content: the Content
/// module's reference fixture, plus a medium task over the same material. The tree is a copy of its
/// own, so a test can change it and ingest again.
/// </summary>
public class ContentTestApi(PostgresTestServer postgres) : IAsyncLifetime
{
    public const string EasyTask = "invoice-mailer-monthly";

    public const string MediumTask = "invoice-mailer-hosted";

    private static readonly string Fixture = Path.Combine(AppContext.BaseDirectory, "ContentFixtures", "reference");

    private TestApiHost? _host;

    public HttpClient Client => _host!.Client;

    public IServiceProvider Services => _host!.Services;

    /// <summary>The content tree this fixture ingested, for a test that edits it.</summary>
    public string ContentRoot { get; } = Path.Combine(Path.GetTempPath(), "ritocode-api-content-" + Guid.NewGuid().ToString("N"));

    /// <summary>Configuration a derived fixture varies, such as a smaller rate limit.</summary>
    protected virtual IReadOnlyDictionary<string, string?>? Settings => null;

    public async ValueTask InitializeAsync()
    {
        var connectionString = await postgres.CreateDatabaseAsync(GetType().Name, TestContext.Current.CancellationToken);
        _host = await TestApiHost.StartAsync(connectionString, developmentIdentityEnabled: true, Settings);

        Copy(Fixture, ContentRoot);
        Write(
            $"tasks/{MediumTask}/task.yaml",
            "material: invoice-mailer\ndifficulty: medium\nfindings:\n  - card: hardcoded-config\n    leaves: [manual.extract-config]\n");
        Write(
            $"tasks/{MediumTask}/ru.md",
            "---\ntitle: Счета для сотни студий\n---\n\n## Context\n\nСервис для сотни студий.\n\n## Brief\n\n«То же самое.»\n");

        await IngestAsync("first");
    }

    public async ValueTask DisposeAsync()
    {
        if (_host is not null)
        {
            await _host.DisposeAsync();
        }

        if (Directory.Exists(ContentRoot))
        {
            Directory.Delete(ContentRoot, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    public void Write(string relativePath, string text)
    {
        var path = Path.Combine(ContentRoot, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    public async Task IngestAsync(string revision)
    {
        await using var scope = Services.CreateAsyncScope();
        var ingest = scope.ServiceProvider.GetRequiredService<IContentIngest>();

        var result = await ingest.IngestAsync(ContentRoot, revision, TestContext.Current.CancellationToken);

        Assert.True(result.Ingested, string.Join("\n", result.Report.Issues));
    }

    private static void Copy(string from, string to)
    {
        Directory.CreateDirectory(to);

        foreach (var file in Directory.GetFiles(from))
        {
            File.Copy(file, Path.Combine(to, Path.GetFileName(file)));
        }

        foreach (var directory in Directory.GetDirectories(from))
        {
            Copy(directory, Path.Combine(to, Path.GetFileName(directory)));
        }
    }
}

/// <summary>As <see cref="ContentTestApi"/>, with a submit limit of two, so the third is refused.</summary>
public sealed class RateLimitedContentTestApi(PostgresTestServer postgres) : ContentTestApi(postgres)
{
    protected override IReadOnlyDictionary<string, string?>? Settings { get; } = new Dictionary<string, string?>
    {
        ["Attempts:RateLimit:MaxSubmissions"] = "2",
    };
}

/// <summary>As <see cref="ContentTestApi"/>, with a signal limit of two, so the third is refused.</summary>
public sealed class SignalRateLimitedContentTestApi(PostgresTestServer postgres) : ContentTestApi(postgres)
{
    protected override IReadOnlyDictionary<string, string?>? Settings { get; } = new Dictionary<string, string?>
    {
        ["Attempts:SignalRateLimit:MaxSignals"] = "2",
    };
}

/// <summary>As <see cref="ContentTestApi"/>, with the development identity's address named an admin.</summary>
public sealed class AdminContentTestApi(PostgresTestServer postgres) : ContentTestApi(postgres)
{
    protected override IReadOnlyDictionary<string, string?>? Settings { get; } = new Dictionary<string, string?>
    {
        ["Users:Admin:Emails:0"] = "  Developer@Ritocode.Local ",
    };
}

/// <summary>As <see cref="ContentTestApi"/>, with an admin who is not the development identity, so the caller is not one.</summary>
public sealed class NonAdminContentTestApi(PostgresTestServer postgres) : ContentTestApi(postgres)
{
    protected override IReadOnlyDictionary<string, string?>? Settings { get; } = new Dictionary<string, string?>
    {
        ["Users:Admin:Emails:0"] = "admin@example.test",
    };
}
