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

    /// <summary>
    /// The web image answers 404 for a task address the export does not name (#300), and the task
    /// catalogue's prerendered page lists them as <c>GET /tasks</c> does (#301).
    /// </summary>
    [Fact]
    public async Task TheExport_HasEveryTask_InTheOrderAndWithTheTextTheApiServes()
    {
        var response = await api.Client.GetAsync(new Uri("/api/v1/tasks?pageSize=100", UriKind.Relative), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var served = JsonNode.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))!["items"]!.AsArray();

        var (content, report) = ContentLoader.Load(api.ContentRoot);
        Assert.False(report.HasErrors, string.Join("\n", report.Issues));
        var tasks = JsonNode.Parse(ContentExport.From(content).ToJson())!["tasks"]!.AsArray();

        Assert.NotEmpty(tasks);
        Assert.Equal(content.Tasks.Count, tasks.Count);
        Assert.Equal(
            served.Select(task => $"{task!["slug"]}|{task["title"]}|{task["difficulty"]}"),
            tasks.Select(task => $"{task!["slug"]}|{task["title"]}|{task["difficulty"]}"));

        foreach (var task in content.Tasks)
        {
            var exported = tasks.Single(candidate => candidate!["slug"]!.GetValue<string>() == task.Slug)!;
            Assert.Equal(task.Texts[ContentRules.DefaultLocale].Context, exported["context"]!.GetValue<string>());
            Assert.Equal(task.Texts[ContentRules.DefaultLocale].Brief, exported["brief"]!.GetValue<string>());
        }
    }

    /// <summary>
    /// A task's public page is prerendered from the export, so the export holds what a learner sees
    /// before answering and nothing that gives the answer away (#301).
    /// </summary>
    [Fact]
    public void TheExport_GivesATask_NoKeyNotesWeightsOrCards()
    {
        var (content, report) = ContentLoader.Load(api.ContentRoot);
        Assert.False(report.HasErrors, string.Join("\n", report.Issues));
        var json = ContentExport.From(content).ToJson();

        foreach (var task in JsonNode.Parse(json)!["tasks"]!.AsArray())
        {
            Assert.Equal(["slug", "title", "difficulty", "context", "brief"], task!.AsObject().Select(field => field.Key));
        }

        foreach (var note in content.Tasks.SelectMany(task => task.Texts.Values).SelectMany(text => text.Notes.Values.Append(text.Lesson ?? string.Empty)))
        {
            if (note.Trim().Length > 0)
            {
                Assert.DoesNotContain(note.Trim(), json, StringComparison.Ordinal);
            }
        }
    }
}
