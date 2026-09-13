using FluentValidation;

namespace Ritocode.Modules.Workspaces.Lifecycle;

/// <summary>Body of <c>POST /api/v1/workspaces</c>.</summary>
/// <remarks>
/// There is no user in it, and there must never be one: the owner comes from <c>ICurrentUser</c>,
/// and a <c>user_id</c> taken from a request body is the second row of ADR 0005's forbidden list.
/// A client that sends one anyway has it ignored, because nothing here binds it.
/// </remarks>
/// <param name="ProblemVersionId">
/// The version to open, as the catalog reports it in <c>problemVersionId</c>. Nullable so an absent
/// field fails validation by name instead of arriving as <see cref="Guid.Empty"/>.
/// </param>
public sealed record OpenWorkspaceRequest(Guid? ProblemVersionId);

internal sealed class OpenWorkspaceRequestValidator : AbstractValidator<OpenWorkspaceRequest>
{
    public OpenWorkspaceRequestValidator()
    {
        RuleFor(request => request.ProblemVersionId).NotEmpty();
    }
}
