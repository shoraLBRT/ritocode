using FluentValidation;
using Ritocode.Modules.Attempts.Domain;
using Ritocode.Modules.Attempts.Persistence;

namespace Ritocode.Modules.Attempts.Lifecycle;

// The shape of each request, checked by the validation filter before a handler runs (ADR 0003).
// Whether a card or a leaf exists is checked against the task on submit, where the facts are.

internal sealed class StartAttemptRequestValidator : AbstractValidator<StartAttemptRequest>
{
    public StartAttemptRequestValidator()
    {
        RuleFor(request => request.Task).NotEmpty().MaximumLength(AttemptConfiguration.SlugMaxLength);
    }
}

internal sealed class RecordStepRequestValidator : AbstractValidator<RecordStepRequest>
{
    public RecordStepRequestValidator()
    {
        RuleFor(request => request.Step)
            .NotEmpty()
            .Must(step => AttemptSteps.TryParse(step, out _))
            .WithMessage("The step is 'diagnosis' or 'treatment'.");
    }
}

internal sealed class SubmitAttemptRequestValidator : AbstractValidator<SubmitAttemptRequest>
{
    /// <summary>More than the whole catalogue; a larger answer is not a learner's.</summary>
    public const int MaxPicks = 100;

    /// <summary>More than the whole treatment tree.</summary>
    public const int MaxLeaves = 50;

    public SubmitAttemptRequestValidator()
    {
        RuleFor(request => request.Picks)
            .NotNull()
            .Must(picks => picks!.Count <= MaxPicks).WithMessage($"At most {MaxPicks} cards can be picked.")
            .Must(picks => picks!.Select(pick => pick?.Card).Distinct(StringComparer.Ordinal).Count() == picks!.Count)
            .WithMessage("A card is picked at most once.")
            .When(request => request.Picks is not null, ApplyConditionTo.CurrentValidator);

        RuleForEach(request => request.Picks).NotNull().ChildRules(pick =>
        {
            pick.RuleFor(item => item!.Card).NotEmpty().MaximumLength(AttemptConfiguration.SlugMaxLength);

            // Step 2 needs at least one leaf for every picked card before the answer can be checked (SPEC §4.4).
            pick.RuleFor(item => item!.Leaves)
                .NotEmpty().WithMessage("Every picked card needs at least one leaf.")
                .Must(leaves => leaves!.Count <= MaxLeaves).WithMessage($"At most {MaxLeaves} leaves per card.")
                .When(item => item!.Leaves is not null, ApplyConditionTo.CurrentValidator);

            pick.RuleForEach(item => item!.Leaves).NotEmpty();
        });
    }
}

/// <summary>A step's name on the wire, as the host's JSON writes enums: camelCase.</summary>
internal static class AttemptSteps
{
    public static bool TryParse(string? value, out AttemptStep step)
    {
        step = default;

        return value is not null
               && !int.TryParse(value, out _)
               && Enum.TryParse(value, ignoreCase: true, out step)
               && Enum.IsDefined(step);
    }
}
