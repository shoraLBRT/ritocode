namespace Ritocode.Modules.Problems.Ingest;

/// <summary>
/// Where problem packages are read from, and whether the host ingests them on startup.
/// Bound from the <c>Problems:Content</c> configuration section.
/// </summary>
public sealed class ProblemContentOptions
{
    public const string SectionName = "Problems:Content";

    /// <summary>
    /// Whether <see cref="ProblemContentSeeder"/> ingests <see cref="Directory"/> on startup.
    /// Off unless a configuration says otherwise: seeding puts objects in a bucket, and a host
    /// that does that without being asked makes <c>dotnet test</c> and a bare <c>dotnet run</c>
    /// require MinIO — the property issue #37 spent a session buying back.
    /// </summary>
    public bool SeedOnStartup { get; init; }

    /// <summary>
    /// Directory holding one package per subdirectory. Relative paths resolve against the host's
    /// content root.
    /// </summary>
    public string Directory { get; init; } = "content/problems";
}
