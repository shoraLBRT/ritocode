namespace Ritocode.Shared.Contracts.Users;

/// <summary>
/// What <see cref="IUserLookup"/> reports about a user. Its own record rather than the Users
/// module's entity, so a change to that entity is not a change to every module's compilation
/// (ADR 0007 §3).
/// </summary>
/// <param name="Id">The user's identifier, as stored in <c>users.users.id</c>.</param>
/// <param name="Username">Stored lower-cased, as the Users module normalises it.</param>
public sealed record UserSummary(Guid Id, string Username);
