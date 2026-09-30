namespace Ritocode.Modules.Attempts.Signals;

/// <summary><c>POST /signals</c>: an extra pick of the caller's submitted attempt, and an optional comment.</summary>
public sealed record SendSignalRequest(string? Attempt, string? Card, string? Comment);

/// <summary>A signal as its sender sees it.</summary>
public sealed record SignalView(Guid Id, Guid Attempt, string Task, string Card, string? Comment, DateTimeOffset CreatedAt);
