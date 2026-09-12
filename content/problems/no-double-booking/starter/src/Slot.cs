namespace Scheduling;

/// <summary>
/// A stretch of time in a diary. A slot may be zero-length; it may not run backwards.
/// </summary>
public readonly record struct Slot(DateTimeOffset Start, DateTimeOffset End);
