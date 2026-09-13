namespace Ritocode.Modules.Workspaces.Files;

/// <summary>Somewhere on disk to hold an archive while it is read or written.</summary>
/// <remarks>
/// A file rather than memory, as ingest does: a put is signed over a known length, and a package's
/// limits allow a workspace of up to 100 MiB. DeleteOnClose is what leaves nothing behind when the
/// work fails half way.
/// </remarks>
internal static class ScratchFile
{
    public static FileStream Create() =>
        new(
            Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()),
            new FileStreamOptions
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.ReadWrite,
                Share = FileShare.None,
                Options = FileOptions.DeleteOnClose | FileOptions.Asynchronous,
            });
}
