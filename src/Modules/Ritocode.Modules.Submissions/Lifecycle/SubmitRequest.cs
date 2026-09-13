using FluentValidation;

namespace Ritocode.Modules.Submissions.Lifecycle;

/// <summary>Body of <c>POST /api/v1/submissions</c>.</summary>
/// <remarks>
/// No user in it, for the reason <c>OpenWorkspaceRequest</c> has none: the owner comes from
/// <c>ICurrentUser</c>, and a <c>user_id</c> taken from a body is on ADR 0005's forbidden list.
/// </remarks>
/// <param name="WorkspaceId">
/// The workspace whose tree is submitted, as it is at the moment of the request. Nullable so an absent
/// field fails validation by name instead of arriving as <see cref="Guid.Empty"/>.
/// </param>
public sealed record SubmitRequest(Guid? WorkspaceId);

internal sealed class SubmitRequestValidator : AbstractValidator<SubmitRequest>
{
    public SubmitRequestValidator()
    {
        RuleFor(request => request.WorkspaceId).NotEmpty();
    }
}
