using System.ComponentModel.DataAnnotations;
using Ritocode.Modules.Submissions.Lifecycle;

namespace Ritocode.Modules.Submissions.Tests.Lifecycle;

/// <summary>The cap's defaults and the ranges startup validation enforces. No database.</summary>
public sealed class SubmissionRateLimitOptionsTests
{
    [Fact]
    public void TheDefaults_AreTenAttemptsInTenMinutes_AndValid()
    {
        var options = new SubmissionRateLimitOptions();

        Assert.Equal(10, options.MaxSubmissions);
        Assert.Equal(TimeSpan.FromMinutes(10), options.Window);
        Assert.True(IsValid(options));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1001)]
    public void AMaximumOutsideItsRange_FailsValidation(int maxSubmissions)
    {
        // Zero would refuse every attempt; the host would start and nobody could submit.
        Assert.False(IsValid(new SubmissionRateLimitOptions { MaxSubmissions = maxSubmissions }));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(59)]
    public void AWindowShorterThanAMinute_FailsValidation(int seconds)
    {
        Assert.False(IsValid(new SubmissionRateLimitOptions { Window = TimeSpan.FromSeconds(seconds) }));
    }

    private static bool IsValid(SubmissionRateLimitOptions options) =>
        Validator.TryValidateObject(options, new ValidationContext(options), [], validateAllProperties: true);
}
