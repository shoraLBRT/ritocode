using FluentValidation;
using Ritocode.Modules.Attempts.Domain;
using Ritocode.Modules.Attempts.Persistence;

namespace Ritocode.Modules.Attempts.Signals;

// The shape of the request, checked by the validation filter (ADR 0003). Whether the card is an extra
// pick of the attempt is checked against the attempt, where the facts are.
internal sealed class SendSignalRequestValidator : AbstractValidator<SendSignalRequest>
{
    public SendSignalRequestValidator()
    {
        RuleFor(request => request.Attempt).NotEmpty();
        RuleFor(request => request.Card).NotEmpty().MaximumLength(AttemptConfiguration.SlugMaxLength);

        // Trimmed as it is stored, so surrounding blanks never make a comment too long.
        RuleFor(request => request.Comment)
            .Must(comment => comment!.Trim().Length <= Signal.CommentMaxLength)
            .WithMessage($"A comment is at most {Signal.CommentMaxLength} characters.")
            .When(request => request.Comment is not null);
    }
}
