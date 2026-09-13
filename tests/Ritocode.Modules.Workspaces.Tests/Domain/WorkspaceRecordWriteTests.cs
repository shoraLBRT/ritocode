using Ritocode.Modules.Workspaces.Domain;

namespace Ritocode.Modules.Workspaces.Tests.Domain;

/// <summary>What a save does to a workspace's timestamps.</summary>
public sealed class WorkspaceRecordWriteTests
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 13, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void RecordWrite_MovesUpdatedAtToTheWrite_AtTheMicrosecondTheColumnKeeps()
    {
        var workspace = Workspace.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), Noon);

        workspace.RecordWrite(Noon.AddTicks(TimeSpan.TicksPerHour + 7));

        Assert.Equal(Noon.AddHours(1), workspace.UpdatedAt);
        Assert.Equal(Noon, workspace.CreatedAt);
    }

    [Fact]
    public void RecordWrite_FromAClockBehindTheLastWrite_LeavesUpdatedAtWhereItWas()
    {
        // Moving it back would reorder "continue where you left off", and before created_at the
        // database refuses the row outright.
        var workspace = Workspace.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), Noon);

        workspace.RecordWrite(Noon.AddHours(2));
        workspace.RecordWrite(Noon.AddHours(1));
        workspace.RecordWrite(Noon.AddHours(-1));

        Assert.Equal(Noon.AddHours(2), workspace.UpdatedAt);
    }
}
