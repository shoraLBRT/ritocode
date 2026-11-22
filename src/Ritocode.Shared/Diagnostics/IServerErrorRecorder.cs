namespace Ritocode.Shared.Diagnostics;

/// <summary>
/// Where an unhandled server exception is kept for the maintainer (ADR 0020 decision 1, #294). The
/// exception handler hands every unexpected exception to it; the Errors module groups and stores it.
/// </summary>
/// <remarks>
/// Host infrastructure, like <see cref="PersonalData.IPersonalData"/>, not a contract: it is a command
/// the host's exception handler gives, not a question one module asks another. A host without the
/// Errors module registers none, and nothing is recorded.
/// </remarks>
public interface IServerErrorRecorder
{
    /// <summary>
    /// Records <paramref name="error"/>. Never throws: an error that cannot be recorded (the database is
    /// what failed) is logged and dropped, as the 500 it came with must still be written.
    /// </summary>
    Task RecordAsync(ServerError error, CancellationToken cancellationToken);
}

/// <summary>
/// An unhandled exception as the request it escaped knew it: no body, no address, no user id — the
/// request id leads to the log lines, which have the rest (ADR 0020).
/// </summary>
/// <param name="Exception">What was thrown.</param>
/// <param name="Location">The method and route template (<c>GET /api/v1/attempts/{id}</c>), or the path when no endpoint matched.</param>
/// <param name="RequestId">The request's id, as its response and log lines carry it.</param>
public sealed record ServerError(Exception Exception, string Location, string? RequestId);
