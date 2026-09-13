using FluentValidation;

namespace Ritocode.Modules.Workspaces.Files;

/// <summary>Body of <c>PUT /api/v1/workspaces/{id}/files/content?path=</c>.</summary>
/// <remarks>
/// The path is not in it, for the reason <c>WorkspaceFileEndpoints</c> gives: a save is addressed
/// exactly as a read is. Neither is a user.
/// </remarks>
/// <param name="Content">
/// The file's whole new text. Empty is a valid file; absent is not a request to empty one.
/// </param>
/// <param name="BaseRevision">The <c>revision</c> the read of the edited copy returned.</param>
public sealed record WriteWorkspaceFileRequest(string? Content, string? BaseRevision);

internal sealed class WriteWorkspaceFileRequestValidator : AbstractValidator<WriteWorkspaceFileRequest>
{
    public WriteWorkspaceFileRequestValidator()
    {
        RuleFor(request => request.Content).NotNull();

        // Required, not optional with "last write wins" as the default: a client that forgets it is
        // exactly the client that would overwrite a change it never saw.
        RuleFor(request => request.BaseRevision)
            .Must(FileRevision.IsWellFormed)
            .WithMessage("Must be the revision a read of this file returned: 64 lower-case hexadecimal digits.");
    }
}
