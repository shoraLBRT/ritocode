namespace Ritocode.Shared.PersonalData;

/// <summary>
/// What one module keeps about a user, which it hands over and erases on that user's request
/// (152-FZ; #299): <c>GET /api/v1/me/export</c> asks every module for its part, and
/// <c>DELETE /api/v1/me</c> has every module erase its own rows.
/// </summary>
/// <remarks>
/// <para>
/// Not a contract in the sense of ADR 0007, and deliberately outside <c>Ritocode.Shared.Contracts</c>:
/// a contract is one question one module answers for another, registered once. This is a seam every
/// module that keeps a user's rows implements over its own schema — as <see cref="Modules.IModule"/>
/// is — and the caller asks all of them, never one by name. ADR 0021 records why, and why it writes.
/// </para>
/// <para>
/// Each implementation reaches only its own module's rows, and only with the user in the query.
/// Erasing is idempotent: erasing what is already gone erases nothing and succeeds, so a deletion that
/// failed part-way is finished by asking again.
/// </para>
/// </remarks>
public interface IPersonalData
{
    /// <summary>The module's name, as <see cref="Modules.IModule.Name"/> gives it: its part's key in an export.</summary>
    string Module { get; }

    /// <summary>
    /// Whether these rows are the account itself — the Users module's. They are erased after every other
    /// module's, so a deletion that fails part-way leaves an account its owner can still delete.
    /// </summary>
    bool HoldsTheAccount { get; }

    /// <summary>
    /// Everything this module keeps about <paramref name="userId"/>, as a value that serializes to JSON —
    /// the module's own record types, never its entities. A user it keeps nothing about gets its empty shape.
    /// </summary>
    Task<object> ExportAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Erases every row this module keeps about <paramref name="userId"/>, and answers how many went.</summary>
    Task<int> EraseAsync(Guid userId, CancellationToken cancellationToken);
}
