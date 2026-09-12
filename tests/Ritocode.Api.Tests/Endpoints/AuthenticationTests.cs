using System.Net;
using System.Text.Json;
using Npgsql;
using Ritocode.Api.Tests.Infrastructure;
using Ritocode.Shared.Http;
using Ritocode.Shared.Identity;

namespace Ritocode.Api.Tests.Endpoints;

/// <summary>
/// The identity seam as the host actually composes it: the development identity from ADR 0005
/// authenticating requests, and the row behind it.
/// </summary>
public sealed class AuthenticationTests(TestApi api) : IClassFixture<TestApi>
{
    private static readonly Guid SeededUserId = new DevelopmentIdentityOptions().UserId;

    [Fact]
    public async Task ProtectedEndpoint_ResolvesTheSeededIdentity()
    {
        var response = await api.Client.GetAsync(
            new Uri("/__probe/current-user", UriKind.Relative),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var probe = await response.Content.ReadFromJsonAsync<CurrentUserProbe>(
            TestContext.Current.CancellationToken);

        Assert.NotNull(probe);
        Assert.Equal(SeededUserId, probe.UserId);
    }

    [Fact]
    public async Task TheSeededIdentity_HasARowInTheUsersSchema()
    {
        // The claim naming a user no row can match would pass authentication and then fail at the
        // first workspace, several layers from the cause — workspaces.user_id has no foreign key to
        // catch it. This is the assertion that the two halves agree.
        await using var connection = new NpgsqlConnection(api.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        await using var command = new NpgsqlCommand(
            "SELECT username, email FROM users.users WHERE id = @id",
            connection);
        command.Parameters.AddWithValue("id", SeededUserId);

        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);

        Assert.True(await reader.ReadAsync(TestContext.Current.CancellationToken), "The development identity has no user row.");
        Assert.Equal("developer", reader.GetString(0));
        Assert.Equal("developer@ritocode.local", reader.GetString(1));
    }

    [Fact]
    public async Task SeedingIsIdempotent_AcrossRestarts()
    {
        // The host has started once; the identifier is configuration rather than generated, so a
        // second start must find the row rather than add a second one. A generated identifier would
        // pass every other test here and orphan every workspace on restart.
        await using var connection = new NpgsqlConnection(api.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        await using var command = new NpgsqlCommand("SELECT count(*) FROM users.users", connection);
        var count = (long)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;

        Assert.Equal(1, count);
    }
}

/// <summary>
/// The same host with no identity, which is what a deployment looks like before a real session
/// provider exists and what it looks like the moment the seeded one is removed.
/// </summary>
public sealed class AnonymousRequestTests(AnonymousTestApi api) : IClassFixture<AnonymousTestApi>
{
    [Fact]
    public async Task ProtectedEndpoint_AnswersUnauthenticatedInTheUnifiedErrorBody()
    {
        var response = await api.Client.GetAsync(
            new Uri("/__probe/current-user", UriKind.Relative),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        Assert.Equal("unauthenticated", document.RootElement.GetProperty("code").GetString());
        Assert.Equal(401, document.RootElement.GetProperty("status").GetInt32());
        Assert.Equal(
            ApiProblem.TypeUriPrefix + "unauthenticated",
            document.RootElement.GetProperty("type").GetString());
        Assert.False(string.IsNullOrWhiteSpace(document.RootElement.GetProperty("requestId").GetString()));
    }

    [Fact]
    public async Task AChallenge_StillCarriesTheCorrelationHeader()
    {
        var response = await api.Client.GetAsync(
            new Uri("/__probe/current-user", UriKind.Relative),
            TestContext.Current.CancellationToken);

        Assert.True(response.Headers.Contains(RequestId.HeaderName));
    }

    [Fact]
    public async Task NoWwwAuthenticateHeader_IsOffered()
    {
        // There is no credential a client could be told to present, and a scheme a browser
        // understands would put a native credential prompt in front of the frontend.
        var response = await api.Client.GetAsync(
            new Uri("/__probe/current-user", UriKind.Relative),
            TestContext.Current.CancellationToken);

        Assert.Empty(response.Headers.WwwAuthenticate);
    }

    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    [InlineData("/api/v1/meta/modules")]
    [InlineData("/api/v1/problems")]
    public async Task EndpointsThatSayAllowAnonymous_SurviveTheFallbackPolicy(string path)
    {
        // The fallback policy protects anything that states no requirement, which is the point of
        // having one. These four state one, and a change that quietly loses an AllowAnonymous would
        // otherwise only be noticed by whoever opened the catalog while signed out.
        var response = await api.Client.GetAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task NoUserRow_IsSeededWhenTheDevelopmentIdentityIsOff()
    {
        await using var connection = new NpgsqlConnection(api.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        await using var command = new NpgsqlCommand("SELECT count(*) FROM users.users", connection);
        var count = (long)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;

        Assert.Equal(0, count);
    }
}
