using System.Globalization;
using System.Text.RegularExpressions;

namespace Ritocode.Shared.Storage;

/// <summary>
/// The key layout of docs/STORAGE_LAYOUT.md, in one place. Every key the platform writes is built
/// here, so the layout has exactly one definition and a change to it is a change to one file.
/// </summary>
/// <remarks>
/// <para>
/// Rule 1 of that document is what the argument checking below enforces: a key is built only from
/// identifiers the platform generated, and whatever is interpolated is checked against its pattern
/// <em>where the key is built</em> — not only where it was parsed. A key assembled from user text
/// is a path-traversal question in a place nobody thinks to look for one.
/// </para>
/// <para>
/// Rule 3 is what these methods are <em>not</em> for: a row that already stores a reference is read
/// back from that reference, never recomputed here. Rebuilding a key in order to find an existing
/// object is what would make this file load-bearing forever and the layout impossible to change.
/// The one exception the layout allows is
/// <see cref="SubmissionInputTree(Guid)"/>, whose key has no column to be stored in.
/// </para>
/// </remarks>
public static partial class StorageKeys
{
    /// <summary>
    /// A validator's <c>id</c> from the package manifest. Constrained to the same slug the manifest
    /// schema already enforces (docs/PROBLEM_PACKAGE_SPEC.md), re-checked here because this is
    /// where it becomes part of a path.
    /// </summary>
    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex ValidatorId { get; }

    /// <summary>Manifest limit on a validator id, and the number rule 4's length budget assumes.</summary>
    private const int MaxValidatorIdLength = 32;

    /// <summary>The one archive a published problem version is materialised from. Written once.</summary>
    public static StorageReference ProblemBundle(Guid problemVersionId) =>
        StorageReference.Create(
            StorageRole.ProblemBundles,
            $"problem-versions/{Identifier(problemVersionId, nameof(problemVersionId))}/bundle.tar.gz");

    /// <summary>A workspace's current working tree. Overwritten on every save; a put is atomic.</summary>
    public static StorageReference WorkspaceSnapshot(Guid workspaceId) =>
        StorageReference.Create(
            StorageRole.WorkspaceSnapshots,
            $"workspaces/{Identifier(workspaceId, nameof(workspaceId))}/tree.tar.gz");

    /// <summary>
    /// Everything one evaluation produced, as a prefix — the set of files is not known when the
    /// reference is written, because it grows with the number of validators.
    /// </summary>
    public static StorageReference SubmissionArtifacts(Guid submissionId) =>
        StorageReference.Create(
            StorageRole.EvaluationArtifacts,
            $"submissions/{Identifier(submissionId, nameof(submissionId))}/");

    /// <summary>
    /// The frozen copy of the workspace tree a submission is evaluated from — never the live
    /// workspace key, which is overwritten on every save. Evaluating from the live key would mean
    /// re-evaluating one submission reads different bytes, and the determinism claim would fail
    /// underneath anything the sandbox contract guarantees.
    /// </summary>
    public static StorageReference SubmissionInputTree(Guid submissionId) =>
        StorageReference.Create(
            StorageRole.EvaluationArtifacts,
            $"submissions/{Identifier(submissionId, nameof(submissionId))}/input/tree.tar.gz");

    /// <summary>One validator's captured standard output, UTF-8 text.</summary>
    public static StorageReference ValidatorStdout(Guid submissionId, string validatorId) =>
        ValidatorArtifact(submissionId, validatorId, "stdout.txt");

    /// <summary>One validator's captured standard error, UTF-8 text.</summary>
    public static StorageReference ValidatorStderr(Guid submissionId, string validatorId) =>
        ValidatorArtifact(submissionId, validatorId, "stderr.txt");

    /// <summary>One validator's output mount, archived.</summary>
    public static StorageReference ValidatorOutput(Guid submissionId, string validatorId) =>
        ValidatorArtifact(submissionId, validatorId, "output.tar.gz");

    private static StorageReference ValidatorArtifact(Guid submissionId, string validatorId, string fileName) =>
        StorageReference.Create(
            StorageRole.EvaluationArtifacts,
            $"submissions/{Identifier(submissionId, nameof(submissionId))}"
            + $"/validators/{Slug(validatorId, nameof(validatorId))}/{fileName}");

    /// <summary>
    /// Lower-case canonical UUID, 36 characters. <see cref="Guid.Empty"/> is rejected because it is
    /// the value an unassigned identifier has, and a key built from it collides with every other
    /// caller that made the same mistake.
    /// </summary>
    private static string Identifier(Guid value, string parameterName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("An empty identifier cannot be part of a storage key.", parameterName);
        }

        return value.ToString("D", CultureInfo.InvariantCulture);
    }

    private static string Slug(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);

        if (value.Length > MaxValidatorIdLength || !ValidatorId.IsMatch(value))
        {
            throw new ArgumentException(
                $"'{value}' is not a valid validator id; storage keys are built only from validated slugs.",
                parameterName);
        }

        return value;
    }
}
