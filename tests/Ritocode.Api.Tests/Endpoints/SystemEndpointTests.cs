using System.Net;
using System.Text.Json;
using Ritocode.Api.Admin;
using Ritocode.Api.Tests.Infrastructure;

namespace Ritocode.Api.Tests.Endpoints;

/// <summary>
/// The admin system page (#297) for an admin: what is deployed and how it is. A non-admin's and a signed-out
/// caller's answers are in <see cref="NonAdminEndpointTests"/> and <see cref="AnonymousAdminEndpointTests"/>.
/// </summary>
public sealed class SystemEndpointTests(AdminContentTestApi api) : IClassFixture<AdminContentTestApi>
{
    [Fact]
    public async Task TheReport_NamesTheBuild_TheDatabase_EveryModulesMigrations_AndTheDependencies()
    {
        using var body = await GetAsync(api.Client);
        var root = body.RootElement;

        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("api").GetProperty("version").GetString()));
        Assert.True(root.GetProperty("api").TryGetProperty("commit", out _));

        var database = root.GetProperty("database");
        Assert.Equal("ok", database.GetProperty("status").GetString());
        Assert.True(database.GetProperty("sizeBytes").GetInt64() > 0);

        var migrations = root.GetProperty("migrations").EnumerateArray().ToList();
        Assert.Contains(migrations, module => module.GetProperty("module").GetString() == "content");
        Assert.Contains(migrations, module => module.GetProperty("module").GetString() == "attempts");
        Assert.All(migrations, module =>
        {
            Assert.Equal("upToDate", module.GetProperty("status").GetString());
            Assert.True(module.GetProperty("applied").GetInt32() > 0);
            Assert.False(string.IsNullOrEmpty(module.GetProperty("last").GetString()));
            Assert.Empty(module.GetProperty("pending").EnumerateArray());
        });

        Assert.Equal(
            ["postgresql:ok", "umami:notConfigured", "feedbackGitHubApp:notConfigured"],
            root.GetProperty("dependencies").EnumerateArray()
                .Select(dependency => $"{dependency.GetProperty("name").GetString()}:{dependency.GetProperty("status").GetString()}"));

        // No heartbeat configured here (#296; BackupHeartbeatTests reads one): absent, not an error.
        Assert.Equal(JsonValueKind.Null, root.GetProperty("backup").ValueKind);
        Assert.EndsWith("Z", root.GetProperty("checkedAt").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheContentRevision_IsTheLastIngests()
    {
        using (var first = await GetAsync(api.Client))
        {
            Assert.Equal("first", first.RootElement.GetProperty("content").GetProperty("revision").GetString());
        }

        var before = DateTimeOffset.UtcNow;
        await api.IngestAsync("second");

        using var second = await GetAsync(api.Client);
        var content = second.RootElement.GetProperty("content");
        Assert.Equal("second", content.GetProperty("revision").GetString());
        Assert.True(content.GetProperty("ingestedAt").GetDateTimeOffset() >= before.AddSeconds(-5));
    }

    internal static async Task<JsonDocument> GetAsync(HttpClient client)
    {
        var response = await client.GetAsync(new Uri("/api/v1/admin/system", UriKind.Relative), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }
}

/// <summary>Umami configured but not answering is failing — and the rest of the report is still made.</summary>
public sealed class SystemUnreachableUmamiTests(AdminUnreachableUmamiTestApi api) : IClassFixture<AdminUnreachableUmamiTestApi>
{
    [Fact]
    public async Task UmamiNotAnswering_IsFailing()
    {
        using var body = await SystemEndpointTests.GetAsync(api.Client);

        var umami = body.RootElement.GetProperty("dependencies").EnumerateArray().Single(dependency => dependency.GetProperty("name").GetString() == "umami");
        Assert.Equal("failing", umami.GetProperty("status").GetString());
        Assert.Equal("ok", body.RootElement.GetProperty("database").GetProperty("status").GetString());
    }
}

/// <summary>The API's version and commit, from the informational version the SDK writes.</summary>
public sealed class BuildInfoTests
{
    [Theory]
    [InlineData("0.1.0+ac1caf1b2c3d", "0.1.0", "ac1caf1b2c3d")]
    [InlineData("0.1.0", "0.1.0", null)]
    [InlineData("0.1.0+", "0.1.0", null)]
    [InlineData(null, "unknown", null)]
    [InlineData("  ", "unknown", null)]
    public void Parse_SplitsTheVersionFromTheCommit(string? informational, string version, string? commit)
    {
        Assert.Equal(new BuildInfo(version, commit), BuildInfo.Parse(informational));
    }
}
