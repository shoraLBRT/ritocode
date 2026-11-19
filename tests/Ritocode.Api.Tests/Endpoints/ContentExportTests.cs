using System.Net;
using System.Text.Json.Nodes;
using Ritocode.Api.Tests.Infrastructure;
using Ritocode.Modules.Content.Catalogue;
using Ritocode.Modules.Content.Format;

namespace Ritocode.Api.Tests.Endpoints;

/// <summary>
/// The content export the frontend prerenders <c>/problems</c> from (docs/SPEC.md §4.1) is the
/// catalogue <c>GET /problems</c> serves, byte for byte in meaning: made from the same files by the
/// same parser, it cannot show a card the API does not.
/// </summary>
public sealed class ContentExportTests(ContentTestApi api) : IClassFixture<ContentTestApi>
{
    [Fact]
    public async Task TheExport_IsTheCatalogueTheApiServes()
    {
        var response = await api.Client.GetAsync(new Uri("/api/v1/problems", UriKind.Relative), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var served = JsonNode.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        var (content, report) = ContentLoader.Load(api.ContentRoot);
        Assert.False(report.HasErrors, string.Join("\n", report.Issues));
        var exported = JsonNode.Parse(ContentExport.From(content).ToJson())!["problems"];

        Assert.NotEmpty(served!["cards"]!.AsArray());
        Assert.True(JsonNode.DeepEquals(served, exported), $"GET /problems:\n{served}\n\nexport:\n{exported}");
    }

    /// <summary>The web image answers 404 for a task address the export does not name (#300).</summary>
    [Fact]
    public void TheExport_NamesEveryTaskOfTheFiles_InOrder()
    {
        var (content, report) = ContentLoader.Load(api.ContentRoot);
        Assert.False(report.HasErrors, string.Join("\n", report.Issues));
        var tasks = JsonNode.Parse(ContentExport.From(content).ToJson())!["tasks"]!.AsArray()
            .Select(slug => slug!.GetValue<string>())
            .ToList();

        Assert.NotEmpty(tasks);
        Assert.Equal(content.Tasks.Select(task => task.Slug).Order(StringComparer.Ordinal), tasks);
    }
}
