using Microsoft.EntityFrameworkCore;
using Ritocode.Modules.Submissions.Persistence;
using Ritocode.Shared.Contracts.Users;
using Ritocode.Shared.Contracts.Workspaces;
using Ritocode.TestSupport;

namespace Ritocode.Modules.Submissions.Tests;

/// <summary>
/// A migrated database, and contexts over it configured the way <c>AddModuleDbContext</c> configures
/// the host's — including the naming convention, without which every query names columns that do not
/// exist.
/// </summary>
internal sealed class SubmissionsDatabase
{
    private readonly string _connectionString;

    private SubmissionsDatabase(string connectionString) => _connectionString = connectionString;

    public static async Task<SubmissionsDatabase> CreateAsync(PostgresTestServer postgres, string label) =>
        new(await postgres.CreateDatabaseAsync(label, TestContext.Current.CancellationToken));

    /// <summary>A new context each call, so an assertion reads the database rather than a change tracker.</summary>
    public SubmissionsDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<SubmissionsDbContext>()
            .UseNpgsql(_connectionString)
            .UseSnakeCaseNamingConvention()
            .Options);
}

/// <summary>
/// The Users contract answered from a fixed list. The real implementation is tested in its own module
/// and resolved from the composed host in Ritocode.Api.Tests; here the question is what Submissions does
/// with each answer.
/// </summary>
internal sealed class StubUserLookup(params UserSummary[] users) : IUserLookup
{
    public Task<UserSummary?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(users.FirstOrDefault(user => user.Id == id));
}

/// <summary>
/// The Workspaces contract answered from a list of owned workspaces, applying the owner the way the real
/// one does: a workspace is found only by the user it was added for.
/// </summary>
internal sealed class StubOwnedWorkspaceLookup : IOwnedWorkspaceLookup
{
    private readonly List<(Guid UserId, WorkspaceSummary Workspace)> _workspaces = [];

    public void Add(Guid userId, WorkspaceSummary workspace) => _workspaces.Add((userId, workspace));

    public Task<WorkspaceSummary?> FindAsync(Guid userId, Guid workspaceId, CancellationToken cancellationToken) =>
        Task.FromResult(_workspaces
            .Where(owned => owned.UserId == userId && owned.Workspace.Id == workspaceId)
            .Select(owned => owned.Workspace)
            .FirstOrDefault());
}

/// <summary>A clock that does not move, so a stored timestamp is an assertion and not a range.</summary>
internal sealed class FixedClock(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
