using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;

namespace Ritocode.Modules.Workspaces.Files;

/// <summary>A file's revision: the SHA-256 of its bytes, as 64 lower-case hexadecimal digits.</summary>
/// <remarks>
/// Derived from the content rather than counted, for three reasons. It needs no column, so the
/// snapshot stays the only record of what a file holds. It is per file, so saving one file does not
/// make an editor's copy of another one stale. And it cannot disagree with the tree: if a save's
/// upload lands and its commit does not, the next read reports the revision of what is actually
/// stored, where a counter kept in the row would still name the old one.
/// </remarks>
internal static class FileRevision
{
    public const int Length = 64;

    public static string Of(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    /// <summary>The shape <see cref="Of"/> produces. Says nothing about whether any file has it.</summary>
    public static bool IsWellFormed([NotNullWhen(true)] string? value) =>
        value is { Length: Length } && value.All(char.IsAsciiHexDigitLower);
}
