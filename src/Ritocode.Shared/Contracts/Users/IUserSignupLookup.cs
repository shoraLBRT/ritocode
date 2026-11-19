namespace Ritocode.Shared.Contracts.Users;

/// <summary>Answers when users signed up, for the admin area's usage over time (#304).</summary>
/// <remarks>A cross-module contract in the sense of ADR 0007, implemented by the Users module.</remarks>
public interface IUserSignupLookup
{
    /// <summary>The creation moment of every user created from <paramref name="from"/> (included) to <paramref name="to"/> (excluded).</summary>
    Task<IReadOnlyList<DateTimeOffset>> SignupsAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken);
}
