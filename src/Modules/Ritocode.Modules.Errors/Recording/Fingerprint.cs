using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Ritocode.Modules.Errors.Domain;

namespace Ritocode.Modules.Errors.Recording;

/// <summary>
/// What makes two errors the same (ADR 0020 decision 1): their source, their type and their top frames.
/// </summary>
/// <remarks>
/// <para>
/// <b>Server</b>: the exception's type and the first <see cref="TopFrames"/> frames of its stack, each the
/// method alone — no file and no line, so an edit elsewhere in the file does not split a group. The message
/// is left out: it often carries a value (an id, a key), and the same failure with another value is the same
/// error.
/// </para>
/// <para>
/// <b>Browser</b>: the type, the message with its numbers taken out, and the top frames' function names. A
/// browser's type is nearly always <c>Error</c> or <c>TypeError</c>, so the message is what tells two apart;
/// a frame's address is left out, because the bundle's file name changes with every release. A release that
/// renames a minified function still splits a group — the price of grouping without source maps.
/// </para>
/// </remarks>
internal static partial class Fingerprint
{
    public const int TopFrames = 3;

    public static string Of(ErrorSource source, string type, string message, string? stack)
    {
        string[] head = source == ErrorSource.Server
            ? [source.ToString(), type]
            : [source.ToString(), type, Digits().Replace(message, "#")];
        var parts = head.Concat(Frames(source, stack).Take(TopFrames));

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', parts)));
        return Convert.ToHexStringLower(hash);
    }

    /// <summary>The stack's frames as the fingerprint reads them, outermost last.</summary>
    internal static IEnumerable<string> Frames(ErrorSource source, string? stack)
    {
        if (string.IsNullOrWhiteSpace(stack))
        {
            yield break;
        }

        foreach (var raw in stack.Split('\n'))
        {
            var line = raw.Trim();
            var frame = source == ErrorSource.Server ? ServerFrame(line) : BrowserFrame(line);

            if (frame is { Length: > 0 })
            {
                yield return frame;
            }
        }
    }

    /// <summary><c>at Ns.Type.Method(Args) in /src/File.cs:line 12</c> → <c>Ns.Type.Method(Args)</c>.</summary>
    private static string? ServerFrame(string line)
    {
        if (!line.StartsWith("at ", StringComparison.Ordinal))
        {
            return null;
        }

        var frame = line[3..];
        var file = frame.IndexOf(" in ", StringComparison.Ordinal);
        return (file >= 0 ? frame[..file] : frame).Trim();
    }

    /// <summary>
    /// Chrome's <c>at fn (https://…/index-abc.js:1:2)</c> and Firefox's <c>fn@https://…:1:2</c> → <c>fn</c>.
    /// An anonymous frame says nothing that survives a release and is skipped.
    /// </summary>
    private static string? BrowserFrame(string line)
    {
        if (line.StartsWith("at ", StringComparison.Ordinal))
        {
            var frame = line[3..];
            var address = frame.IndexOf(" (", StringComparison.Ordinal);
            return address >= 0 ? frame[..address].Trim() : null;
        }

        var at = line.IndexOf('@', StringComparison.Ordinal);
        return at > 0 ? line[..at].Trim() : null;
    }

    [GeneratedRegex(@"\d+")]
    private static partial Regex Digits();
}
