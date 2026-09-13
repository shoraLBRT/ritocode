using Ritocode.Modules.Submissions.Lifecycle;
using Ritocode.TestSupport;

namespace Ritocode.Api.Tests.Infrastructure;

/// <summary>
/// <see cref="WorkspaceApi"/> with a cap of two attempts, so the refusal of the third can be observed over
/// HTTP without a class submitting ten times — and without sharing a cap with any other class.
/// </summary>
public sealed class RateLimitedWorkspaceApi(PostgresTestServer postgres, MinioTestServer minio) : WorkspaceApi(postgres, minio)
{
    public const int MaxSubmissions = 2;

    protected override IReadOnlyDictionary<string, string?>? Settings => new Dictionary<string, string?>
    {
        [$"{SubmissionRateLimitOptions.SectionName}:{nameof(SubmissionRateLimitOptions.MaxSubmissions)}"] = $"{MaxSubmissions}",
    };
}
