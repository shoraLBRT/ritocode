using Ritocode.Api.Tests.Infrastructure;
using Ritocode.Shared.Http;

namespace Ritocode.Api.Tests.Endpoints;

public sealed class RequestCorrelationTests(TestApi api) : IClassFixture<TestApi>
{
    [Fact]
    public async Task Response_AlwaysCarriesARequestId()
    {
        var response = await api.Client.GetAsync(new Uri("/health/live", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.True(response.Headers.TryGetValues(RequestId.HeaderName, out var values));
        Assert.False(string.IsNullOrWhiteSpace(values!.Single()));
    }

    [Fact]
    public async Task InboundRequestId_IsEchoedBack()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.Add(RequestId.HeaderName, "trace-from-gateway");

        var response = await api.Client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal("trace-from-gateway", response.Headers.GetValues(RequestId.HeaderName).Single());
    }

    [Theory]
    [InlineData("has spaces")]
    [InlineData("injected\tvalue")]
    [InlineData("")]
    public async Task MalformedInboundRequestId_IsReplacedWithAGeneratedOne(string candidate)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.TryAddWithoutValidation(RequestId.HeaderName, candidate);

        var response = await api.Client.SendAsync(request, TestContext.Current.CancellationToken);

        var echoed = response.Headers.GetValues(RequestId.HeaderName).Single();
        Assert.NotEqual(candidate, echoed);
        Assert.False(string.IsNullOrWhiteSpace(echoed));
    }

    [Fact]
    public async Task OverlongInboundRequestId_IsReplaced()
    {
        var tooLong = new string('a', RequestId.MaxLength + 1);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.Add(RequestId.HeaderName, tooLong);

        var response = await api.Client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.NotEqual(tooLong, response.Headers.GetValues(RequestId.HeaderName).Single());
    }
}
