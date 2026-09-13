using Ritocode.Modules.Workspaces.Domain;
using Ritocode.Shared.Storage;

namespace Ritocode.Modules.Workspaces.Tests.Domain;

public sealed class WorkspaceTests
{
    [Fact]
    public void Create_KeysTheSnapshotByTheWorkspacesOwnId()
    {
        var workspace = Workspace.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), DateTimeOffset.UtcNow);

        // Built once, here, from an id the platform generated — so a workspace cannot point at
        // another workspace's tree, and every later read uses this stored value.
        Assert.Equal(StorageKeys.WorkspaceSnapshot(workspace.Id), workspace.SnapshotReference);
        Assert.Equal(StorageRole.WorkspaceSnapshots, workspace.SnapshotReference.Role);
    }

    [Fact]
    public void Create_StoresUtc_AndStartsWithNothingWrittenSinceCreation()
    {
        var local = new DateTimeOffset(2026, 9, 13, 14, 0, 0, TimeSpan.FromHours(2));

        var workspace = Workspace.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), local);

        Assert.Equal(TimeSpan.Zero, workspace.CreatedAt.Offset);
        Assert.Equal(local, workspace.CreatedAt);
        Assert.Equal(workspace.CreatedAt, workspace.UpdatedAt);
    }

    [Fact]
    public void Create_KeepsOnlyThePrecisionTheDatabaseStores()
    {
        // 100-nanosecond ticks survive in memory and not in a timestamptz. A create response built
        // from the entity would then disagree with every later read of the same workspace.
        var precise = new DateTimeOffset(2026, 9, 13, 12, 0, 0, TimeSpan.Zero).AddTicks(1_234_567);

        var workspace = Workspace.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), precise);

        Assert.Equal(precise.AddTicks(-7), workspace.CreatedAt);
        Assert.Equal(0, workspace.CreatedAt.Ticks % TimeSpan.TicksPerMicrosecond);
    }

    [Fact]
    public void Create_RefusesAnEmptyOwnerOrVersion()
    {
        Assert.Throws<ArgumentException>(() => Workspace.Create(Guid.Empty, Guid.CreateVersion7(), DateTimeOffset.UtcNow));
        Assert.Throws<ArgumentException>(() => Workspace.Create(Guid.CreateVersion7(), Guid.Empty, DateTimeOffset.UtcNow));
    }
}
