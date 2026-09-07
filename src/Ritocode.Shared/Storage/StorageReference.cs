using System.Diagnostics.CodeAnalysis;

namespace Ritocode.Shared.Storage;

/// <summary>
/// A pointer to an object, or to a set of objects, in the form <c>role/key</c> — the exact content
/// of the <c>*_reference</c> columns, specified in docs/STORAGE_LAYOUT.md.
/// </summary>
/// <remarks>
/// <para>
/// It is deliberately not a URL: a stored endpoint stops resolving the moment the port, the host or
/// the provider changes, and writes a deployment detail into data that outlives the deployment. It
/// is not a bare key either — the role is what lets a reference be read on its own, in psql or in a
/// log line, without knowing which column it came from.
/// </para>
/// <para>
/// The trailing slash is the whole of the object-versus-prefix distinction, so a reader needs no
/// schema to know which form a string holds.
/// </para>
/// </remarks>
public sealed record StorageReference
{
    /// <summary>
    /// The width of the <c>varchar(512)</c> columns that hold a reference. Checked here rather than
    /// discovered as a truncation at insert time.
    /// </summary>
    public const int MaxLength = 512;

    private readonly string _text;

    private StorageReference(StorageRole role, string key, string text)
    {
        Role = role;
        Key = key;
        _text = text;
    }

    /// <summary>Which of the three buckets, as a role rather than a physical name.</summary>
    public StorageRole Role { get; }

    /// <summary>The key within that role, with no leading slash.</summary>
    public string Key { get; }

    /// <summary>True when this names every object beneath a prefix rather than exactly one object.</summary>
    public bool IsPrefix => Key.EndsWith('/');

    /// <summary>
    /// Builds a reference from a role and a key. Throws, rather than returning a failure, because
    /// keys are assembled by <see cref="StorageKeys"/> from platform-generated identifiers: an
    /// invalid one here is a defect in this codebase, not bad input from a user.
    /// </summary>
    public static StorageReference Create(StorageRole role, string key)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);

        var text = $"{role.Name()}/{key}";

        if (!IsValidKey(key))
        {
            throw new ArgumentException($"'{key}' is not a valid storage key.", nameof(key));
        }

        if (text.Length > MaxLength)
        {
            throw new ArgumentException(
                $"Reference is {text.Length} characters; the reference columns hold {MaxLength}.",
                nameof(key));
        }

        return new StorageReference(role, key, text);
    }

    /// <summary>
    /// Reads a reference back from its stored form. Returns <see langword="false"/> for anything
    /// malformed: a column can hold a value written by a different build, and a caller has to be
    /// able to say so rather than act on a half-understood key.
    /// </summary>
    public static bool TryParse(string? value, [NotNullWhen(true)] out StorageReference? reference)
    {
        reference = null;

        if (string.IsNullOrEmpty(value) || value.Length > MaxLength)
        {
            return false;
        }

        var separator = value.IndexOf('/');
        if (separator <= 0 || separator == value.Length - 1)
        {
            return false;
        }

        if (!StorageRoles.TryParse(value[..separator], out var role))
        {
            return false;
        }

        var key = value[(separator + 1)..];
        if (!IsValidKey(key))
        {
            return false;
        }

        reference = new StorageReference(role, key, value);
        return true;
    }

    /// <summary>The stored form, <c>role/key</c>. This is what goes into a reference column.</summary>
    public override string ToString() => _text;

    /// <summary>
    /// Rule 2 of docs/STORAGE_LAYOUT.md, executable: lower-case ASCII, <c>/</c> as the separator,
    /// nothing that has to be percent-encoded, and no segment that can walk out of its prefix.
    /// A trailing slash is allowed and is what makes the key a prefix.
    /// </summary>
    private static bool IsValidKey(string key)
    {
        if (key.Length == 0 || key[0] == '/')
        {
            return false;
        }

        var segments = key.Split('/');

        for (var index = 0; index < segments.Length; index++)
        {
            var segment = segments[index];

            // Split leaves one empty tail segment for a trailing slash — the prefix marker.
            // An empty segment anywhere else is a doubled slash.
            if (segment.Length == 0)
            {
                if (index != segments.Length - 1 || segments.Length == 1)
                {
                    return false;
                }

                continue;
            }

            // "." and ".." are the two ways a relative path stops being confined to where it began.
            if (segment is "." or "..")
            {
                return false;
            }

            foreach (var character in segment)
            {
                var allowed = char.IsAsciiDigit(character)
                    || char.IsAsciiLetterLower(character)
                    || character is '-' or '_' or '.';

                if (!allowed)
                {
                    return false;
                }
            }
        }

        return true;
    }
}
