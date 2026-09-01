using Ritocode.Shared.Errors;

namespace Ritocode.Shared.Tests.Errors;

public sealed class ResultTests
{
    [Fact]
    public void Success_ExposesValue()
    {
        var result = Result<int>.Success(7);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Error);
        Assert.Equal(7, result.Value);
    }

    [Fact]
    public void Failure_ThrowsWhenValueIsRead()
    {
        var result = Result<int>.Failure(AppError.NotFound("gone", "Gone."));

        Assert.False(result.IsSuccess);
        var exception = Assert.Throws<InvalidOperationException>(() => result.Value);
        Assert.Contains("gone", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Match_SelectsTheCorrectBranch()
    {
        Result<string> success = "ok";
        Result<string> failure = AppError.Conflict("dup", "Duplicate.");

        Assert.Equal("ok", success.Match(v => v, e => e.Code));
        Assert.Equal("dup", failure.Match(v => v, e => e.Code));
    }
}
