using Ritocode.Shared.Errors;
using Ritocode.Shared.Http;

namespace Ritocode.Shared.Tests.Http;

public sealed class ErrorStatusCodeMapTests
{
    [Theory]
    [InlineData(ErrorType.Validation, 400)]
    [InlineData(ErrorType.Unauthenticated, 401)]
    [InlineData(ErrorType.Forbidden, 403)]
    [InlineData(ErrorType.NotFound, 404)]
    [InlineData(ErrorType.Conflict, 409)]
    [InlineData(ErrorType.PreconditionFailed, 412)]
    [InlineData(ErrorType.RateLimited, 429)]
    [InlineData(ErrorType.Unavailable, 503)]
    [InlineData(ErrorType.Unexpected, 500)]
    public void ToStatusCode_MatchesDocumentedMapping(ErrorType type, int expected)
    {
        Assert.Equal(expected, ErrorStatusCodeMap.ToStatusCode(type));
    }

    /// <summary>
    /// Guards the mapping against a new <see cref="ErrorType"/> silently falling into the 500 case.
    /// A member added without a mapping shows up here rather than in production.
    /// </summary>
    [Fact]
    public void EveryErrorType_HasAnExplicitMapping()
    {
        var unmapped = Enum.GetValues<ErrorType>()
            .Where(type => type != ErrorType.Unexpected && ErrorStatusCodeMap.ToStatusCode(type) == 500)
            .ToArray();

        Assert.Empty(unmapped);
    }

    [Fact]
    public void EveryErrorType_HasATitle()
    {
        foreach (var type in Enum.GetValues<ErrorType>())
        {
            Assert.False(string.IsNullOrWhiteSpace(ErrorStatusCodeMap.ToTitle(type)));
        }
    }
}
