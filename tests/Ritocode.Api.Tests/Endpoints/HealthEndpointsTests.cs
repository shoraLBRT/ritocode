using System.Net;
using System.Text.Json;
using Ritocode.Api.Tests.Infrastructure;

namespace Ritocode.Api.Tests.Endpoints;

public sealed class HealthEndpointsTests(TestApi api) : IClassFixture<TestApi>
{
    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task Probes_RespondHealthy(string path)
    {
        var response = await api.Client.GetAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(body);
        Assert.Equal("Healthy", document.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Live_DoesNotRunDependencyChecks()
    {
        var response = await api.Client.GetAsync(new Uri("/health/live", UriKind.Relative), TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        using var document = JsonDocument.Parse(body);
        Assert.Empty(document.RootElement.GetProperty("checks").EnumerateArray());
    }
}
