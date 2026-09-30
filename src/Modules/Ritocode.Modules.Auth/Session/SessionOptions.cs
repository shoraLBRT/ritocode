using System.ComponentModel.DataAnnotations;

namespace Ritocode.Modules.Auth.Session;

/// <summary>How long a sign-in lasts (ADR 0012).</summary>
public sealed class SessionOptions
{
    public const string SectionName = "Auth:Session";

    /// <summary>From sign-in to the session's end; signing out ends it sooner.</summary>
    [Range(typeof(TimeSpan), "01:00:00", "365.00:00:00")]
    public TimeSpan Lifetime { get; init; } = TimeSpan.FromDays(30);
}
