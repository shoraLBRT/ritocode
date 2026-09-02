namespace Ritocode.Api.Tests.Infrastructure;

/// <summary>
/// Resolves the PostgreSQL instance the API tests run against.
/// </summary>
/// <remarks>
/// Readiness now genuinely depends on the database, so these tests need one running. The default
/// matches the compose stack in <c>compose.yaml</c>, so <c>docker compose up -d</c> followed by
/// <c>dotnet test</c> works with no further setup. CI overrides the connection string through
/// <see cref="EnvironmentVariableName"/>, pointing at its service container.
/// </remarks>
public static class TestDatabase
{
    public const string EnvironmentVariableName = "RITOCODE_TEST_DATABASE";

    /// <summary>Matches the compose defaults, including the non-standard host port.</summary>
    private const string ComposeDefault =
        "Host=localhost;Port=55432;Database=ritocode;Username=ritocode;Password=ritocode";

    public static string ConnectionString =>
        Environment.GetEnvironmentVariable(EnvironmentVariableName) is { Length: > 0 } fromEnvironment
            ? fromEnvironment
            : ComposeDefault;
}
