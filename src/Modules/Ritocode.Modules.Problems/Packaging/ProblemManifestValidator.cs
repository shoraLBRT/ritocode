using FluentValidation;
using Ritocode.Modules.Problems.Domain;

namespace Ritocode.Modules.Problems.Packaging;

/// <summary>
/// The rules of docs/PROBLEM_PACKAGE_SPEC.md that can be decided from the manifest alone.
/// Everything that needs the package's files on disk is in <see cref="ProblemPackageLoader"/>.
/// </summary>
public sealed class ProblemManifestValidator : AbstractValidator<ProblemManifest>
{
    public ProblemManifestValidator()
    {
        RuleFor(m => m.SchemaVersion)
            .NotNull().WithMessage("A manifest must state its schema_version.")
            .Equal(ProblemManifest.CurrentSchemaVersion)
                .WithMessage($"Only schema_version {ProblemManifest.CurrentSchemaVersion} is understood.");

        RuleFor(m => m.Slug)
            .NotEmpty()
            .MaximumLength(ProblemManifest.SlugMaxLength)
            .Must(BeKebabCase).WithMessage("A slug is lower-case words joined by single hyphens.");

        RuleFor(m => m.Title)
            .NotEmpty()
            .MaximumLength(ProblemManifest.TitleMaxLength)
            .Must(title => !title.AsSpan().ContainsAny('\r', '\n')).WithMessage("A title is a single line.");

        RuleFor(m => m.Difficulty).IsInEnum();

        RuleFor(m => m.Language)
            .NotEmpty()
            .MaximumLength(ProblemManifest.LanguageMaxLength)
            .Must(language => ManifestPaths.Language.IsMatch(language))
                .WithMessage("A language is lower-case, for example 'csharp'.");

        RuleFor(m => m.Tags)
            .NotEmpty().WithMessage("A problem carries at least one tag.")
            .Must(tags => tags.Length <= ProblemManifest.MaxTags)
                .WithMessage($"At most {ProblemManifest.MaxTags} tags.")
            .Must(tags => tags.Distinct(StringComparer.Ordinal).Count() == tags.Length)
                .WithMessage("Tags must be distinct.");

        RuleForEach(m => m.Tags)
            .MaximumLength(ProblemManifest.TagMaxLength)
            .Must(BeKebabCase).WithMessage("A tag is lower-case words joined by single hyphens.");

        RuleFor(m => m.Description)
            .NotEmpty()
            .Must(path => ManifestPaths.IsInsidePackage(path))
                .WithMessage("A description is a relative path to a file inside the package.");

        RuleFor(m => m.Hints)
            .Must(hints => hints.Length <= ProblemManifest.MaxHints)
                .WithMessage($"At most {ProblemManifest.MaxHints} hints.");

        RuleForEach(m => m.Hints)
            .NotEmpty()
            .MaximumLength(ProblemManifest.HintMaxLength);

        RuleFor(m => m.Workspace).NotNull().SetValidator(new WorkspaceSpecValidator());
        RuleFor(m => m.Limits).NotNull().SetValidator(new LimitsSpecValidator());
        RuleFor(m => m.Fixtures).NotNull().SetValidator(new FixturesSpecValidator());

        RuleFor(m => m.Validators)
            .NotEmpty().WithMessage("A problem needs at least one validator.")
            .Must(validators => validators.Length <= ProblemManifest.MaxValidators)
                .WithMessage($"At most {ProblemManifest.MaxValidators} validators.")
            .Must(HaveDistinctIds).WithMessage("Validator ids must be distinct.")
            // Weights are stated, not normalised: normalising means adding a validator silently
            // reweights the others and the author never sees it happen.
            .Must(SumToFullWeight).WithMessage("Validator weights must sum to exactly 100.");

        RuleForEach(m => m.Validators).SetValidator(new ValidatorSpecValidator());
    }

    internal static bool BeKebabCase(string value) => ManifestPaths.Kebab.IsMatch(value);

    private static bool HaveDistinctIds(ValidatorSpec[] validators) =>
        validators.Select(v => v.Id).Distinct(StringComparer.Ordinal).Count() == validators.Length;

    private static bool SumToFullWeight(ValidatorSpec[] validators) =>
        validators.All(v => v.Weight is not null)
        && validators.Sum(v => v.Weight!.Value) == 100;
}

internal sealed class WorkspaceSpecValidator : AbstractValidator<WorkspaceSpec>
{
    public WorkspaceSpecValidator()
    {
        RuleFor(w => w.Root)
            .NotEmpty()
            .Must(root => ManifestPaths.IsInsidePackage(root))
                .WithMessage("A workspace root is a relative directory inside the package.");

        RuleFor(w => w.Editable)
            .NotEmpty().WithMessage("At least one path must be editable, or the task cannot be attempted.");

        RuleForEach(w => w.Editable).Must(BeGlob).WithMessage(GlobMessage);
        RuleForEach(w => w.Readonly).Must(BeGlob).WithMessage(GlobMessage);
    }

    private const string GlobMessage =
        "A path glob is relative to the workspace root, forward-slashed, and never leaves it.";

    private static bool BeGlob(string glob) => ManifestPaths.IsInsidePackage(glob, allowWildcards: true);
}

internal sealed class LimitsSpecValidator : AbstractValidator<LimitsSpec>
{
    public LimitsSpecValidator()
    {
        RuleFor(l => l.MaxFiles).InclusiveBetween(1, LimitsSpec.MaxMaxFiles);
        RuleFor(l => l.MaxFileBytes).InclusiveBetween(1, LimitsSpec.MaxMaxFileBytes);
        RuleFor(l => l.MaxTotalBytes)
            .InclusiveBetween(1, LimitsSpec.MaxMaxTotalBytes)
            .GreaterThanOrEqualTo(l => l.MaxFileBytes)
                .WithMessage("A workspace cannot be limited to less than one file's worth of bytes.");
    }
}

internal sealed class FixturesSpecValidator : AbstractValidator<FixturesSpec>
{
    public FixturesSpecValidator()
    {
        RuleFor(f => f.Passing).Must(BeDirectory).When(f => f.Passing is not null).WithMessage(Message);
        RuleFor(f => f.Failing).Must(BeDirectory).When(f => f.Failing is not null).WithMessage(Message);
    }

    private const string Message = "A fixture is a relative directory inside the package.";

    private static bool BeDirectory(string? path) => ManifestPaths.IsInsidePackage(path);
}

internal sealed class ValidatorSpecValidator : AbstractValidator<ValidatorSpec>
{
    public ValidatorSpecValidator()
    {
        RuleFor(v => v.Id)
            .NotEmpty()
            .MaximumLength(ValidatorSpec.IdMaxLength)
            .Must(ProblemManifestValidator.BeKebabCase)
                .WithMessage("A validator id is lower-case words joined by single hyphens.");

        // The set of known types is the plugin registry's (#18), not the manifest's: a format that
        // has to be edited to add a validator is a format that gets forked.
        RuleFor(v => v.Type)
            .NotEmpty()
            .MaximumLength(ValidatorSpec.TypeMaxLength)
            .Must(ProblemManifestValidator.BeKebabCase)
                .WithMessage("A validator type is lower-case words joined by single hyphens.");

        RuleFor(v => v.Weight)
            .NotNull().WithMessage("A validator must state its weight.")
            .InclusiveBetween(0, 100);

        RuleFor(v => v.TimeoutSeconds)
            .NotNull().WithMessage("A validator must state its timeout_seconds.")
            .InclusiveBetween(1, ValidatorSpec.MaxTimeoutSeconds);
    }
}
