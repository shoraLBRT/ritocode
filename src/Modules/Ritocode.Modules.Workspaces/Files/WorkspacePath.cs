using System.Diagnostics.CodeAnalysis;

namespace Ritocode.Modules.Workspaces.Files;

/// <summary>
/// The one rule for what a path inside a workspace may look like, wherever that path comes from — a
/// bundle being materialised, a snapshot being read back, or a request.
/// </summary>
/// <remarks>
/// A path that fails it is refused, never normalised. Normalising decides what the caller "meant",
/// and <c>src/../problem.yaml</c> meaning <c>problem.yaml</c> is exactly the traversal ADR 0005
/// forbids. A refused path cannot be confused with a file that is missing, either: the first is
/// the caller's mistake, the second is the tree's state.
/// </remarks>
internal static class WorkspacePath
{
    /// <summary>
    /// A relative, forward-slashed path that cannot leave the directory it is resolved against: no
    /// leading slash, no backslash, no NUL, and no empty, <c>.</c> or <c>..</c> segment.
    /// </summary>
    public static bool IsConfined([NotNullWhen(true)] string? path)
    {
        if (string.IsNullOrEmpty(path)
            || path[0] == '/'
            || path.AsSpan().IndexOfAny('\\', '\0') >= 0)
        {
            return false;
        }

        foreach (var segment in path.Split('/'))
        {
            if (segment is "" or "." or "..")
            {
                return false;
            }
        }

        return true;
    }
}
