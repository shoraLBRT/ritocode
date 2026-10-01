namespace Ritocode.Shared.Contracts.Users;

/// <summary>What <see cref="IUserContactLookup"/> reports about a user (ADR 0007 §3).</summary>
/// <param name="Username">Stored lower-cased, as the Users module normalises it.</param>
/// <param name="Email">Stored lower-cased; always one a provider marked as verified (SPEC §6.1).</param>
public sealed record UserContact(Guid Id, string Username, string Email);
