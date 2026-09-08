using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Ritocode.Shared.Storage;

namespace Ritocode.Shared.Persistence;

/// <summary>
/// Maps a <see cref="StorageReference"/> onto the <c>varchar(512)</c> column that holds it, so a
/// <c>*_reference</c> column is typed in the model and never a raw string.
/// </summary>
/// <remarks>
/// <para>
/// docs/STORAGE_LAYOUT.md describes what these columns contain and <see cref="StorageReference"/>
/// is the only code that knows the shape, but without this converter that knowledge stops at the
/// edge of the entity: any <c>string</c> at all can be assigned to the property and PostgreSQL will
/// take it. The converter moves the check to the boundary the value actually crosses, which is the
/// cheapest place to put it while the tables are empty and the most expensive to retrofit once they
/// are not.
/// </para>
/// <para>
/// Reading an unparseable value throws rather than yielding null. A row whose reference this build
/// cannot resolve is a fault to report — the alternative is a module acting on a half-understood
/// key, which docs/STORAGE_LAYOUT.md rule 3 exists to prevent.
/// </para>
/// </remarks>
public sealed class StorageReferenceConverter : ValueConverter<StorageReference, string>
{
    public StorageReferenceConverter()
        : base(reference => reference.ToString(), text => Parse(text))
    {
    }

    private static StorageReference Parse(string text) =>
        StorageReference.TryParse(text, out var reference)
            ? reference
            : throw new InvalidOperationException(
                $"'{text}' is not a storage reference this build can resolve. "
                + "See docs/STORAGE_LAYOUT.md for the role/key form these columns hold.");
}
