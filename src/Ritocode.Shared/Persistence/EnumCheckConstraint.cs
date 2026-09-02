using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ritocode.Shared.Persistence;

/// <summary>
/// Enum-backed columns are stored as text so the schema stays readable in <c>psql</c>. Text alone
/// accepts anything, so each such column also gets a check constraint listing the allowed values —
/// the database, not only the application, rejects a value outside the enum.
/// </summary>
public static class EnumCheckConstraint
{
    /// <summary>
    /// Adds <c>CHECK (column IN (...))</c> covering every member of <typeparamref name="TEnum"/>.
    /// </summary>
    /// <param name="table">The table builder to constrain.</param>
    /// <param name="columnName">Column holding the enum, in its database (snake_case) form.</param>
    public static TableBuilder<TEntity> HasEnumCheckConstraint<TEntity, TEnum>(
        this TableBuilder<TEntity> table,
        string columnName)
        where TEntity : class
        where TEnum : struct, Enum
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentException.ThrowIfNullOrWhiteSpace(columnName);

        var allowed = string.Join(", ", Enum.GetNames<TEnum>().Select(name => $"'{name}'"));

        table.HasCheckConstraint(
            $"ck_{table.Name}_{columnName}",
            $"\"{columnName}\" IN ({allowed})");

        return table;
    }
}
