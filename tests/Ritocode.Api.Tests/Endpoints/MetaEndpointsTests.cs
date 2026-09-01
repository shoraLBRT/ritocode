using System.Net;
using System.Text.Json;
using Ritocode.Api.Setup;
using Ritocode.Api.Tests.Infrastructure;

namespace Ritocode.Api.Tests.Endpoints;

public sealed class MetaEndpointsTests(TestApi api) : IClassFixture<TestApi>
{
    [Fact]
    public async Task ModuleInventory_ListsEveryComposedModule()
    {
        var response = await api.Client.GetAsync(new Uri("/api/v1/meta/modules", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var names = document.RootElement.EnumerateArray()
            .Select(e => e.GetProperty("name").GetString())
            .ToArray();

        Assert.Equal(ModuleRegistry.All.Count, names.Length);
        foreach (var module in ModuleRegistry.All)
        {
            Assert.Contains(module.Name, names);
        }
    }

    [Fact]
    public void ModuleRegistry_HasNoDuplicateNamesOrRoutePrefixes()
    {
        Assert.Equal(ModuleRegistry.All.Count, ModuleRegistry.All.Select(m => m.Name).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(ModuleRegistry.All.Count, ModuleRegistry.All.Select(m => m.RoutePrefix).Distinct(StringComparer.Ordinal).Count());
    }
}
