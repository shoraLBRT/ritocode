using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Ritocode.Api.Tests.Infrastructure;
using Ritocode.Modules.Attempts.Domain;
using Ritocode.Modules.Attempts.Persistence;

namespace Ritocode.Api.Tests.Endpoints;

/// <summary>
/// <c>GET /api/v1/me/progress</c> over real attempts at the reference content: first attempts count,
/// practice changes nothing, and nobody else's attempts count.
/// </summary>
public sealed class ProgressEndpointTests(ContentTestApi api) : IClassFixture<ContentTestApi>
{
    [Fact]
    public async Task ProgressIsBuiltFromFirstAttemptsOnly()
    {
        // Nothing yet: every class at zero, no cards.
        using (var empty = await ProgressAsync())
        {
            Assert.Equal(0, empty.RootElement.GetProperty("tasks").GetInt32());
            Assert.Equal(6, empty.RootElement.GetProperty("classes").GetArrayLength());
            Assert.Equal(0, empty.RootElement.GetProperty("cards").GetArrayLength());
        }

        // The easy task, first: secrets found and treated right, money found and treated wrong, the
        // swallowed error missed, hardcoded configuration picked though its key does not list it.
        await SolveAsync(ContentTestApi.EasyTask,
        [
            Pick("secrets-in-repo", "auto.secrets"),
            Pick("money-in-float", "manual.split"),
            Pick("hardcoded-config", "manual.extract-config"),
        ]);

        // The medium task, first: its one finding found and treated right.
        await SolveAsync(ContentTestApi.MediumTask, [Pick("hardcoded-config", "manual.extract-config")]);

        using var before = await ProgressAsync();

        // The easy task again, answered perfectly: practice, so nothing moves.
        await SolveAsync(ContentTestApi.EasyTask,
        [
            Pick("secrets-in-repo", "auto.secrets"),
            Pick("money-in-float", "manual.representation"),
            Pick("swallowed-error", "manual.handle-errors"),
        ]);

        // Another user's first attempt, with every finding found, is not the caller's progress either.
        await using (var scope = api.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AttemptsDbContext>();
            var theirs = Attempt.Start(Guid.CreateVersion7(), ContentTestApi.EasyTask, DateTimeOffset.UtcNow);
            theirs.Submit(DateTimeOffset.UtcNow, "first", "{\"picks\":[]}", """{"total":0,"maximum":15,"isCorrect":false,"cards":[{"card":"swallowed-error","outcome":"found","points":15,"keyLeaves":["manual.handle-errors"],"treatment":{"matched":true,"matchedLeaves":[],"wrongLeaves":[]}}]}""", "{}", 0, 15, countsTowardProgress: true);
            context.Attempts.Add(theirs);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using var after = await ProgressAsync();
        Assert.Equal(before.RootElement.GetRawText(), after.RootElement.GetRawText());

        var root = after.RootElement;
        Assert.Equal(2, root.GetProperty("tasks").GetInt32());

        var cards = root.GetProperty("cards").EnumerateArray()
            .Select(card => (
                card.GetProperty("card").GetString(),
                card.GetProperty("class").GetString(),
                card.GetProperty("met").GetInt32(),
                card.GetProperty("found").GetInt32(),
                card.GetProperty("missed").GetInt32(),
                card.GetProperty("pickedWhenAbsent").GetInt32(),
                card.GetProperty("treatedRight").GetInt32()))
            .ToList();
        Assert.Equal(
            [
                ("hardcoded-config", "hygiene", 1, 1, 0, 1, 1),
                ("secrets-in-repo", "hygiene", 1, 1, 0, 0, 1),
                ("swallowed-error", "hygiene", 1, 0, 1, 0, 0),
                ("money-in-float", "domain", 1, 1, 0, 0, 0),
            ],
            cards);

        var classes = root.GetProperty("classes").EnumerateArray()
            .ToDictionary(item => item.GetProperty("class").GetString()!, item => (item.GetProperty("met").GetInt32(), item.GetProperty("found").GetInt32(), item.GetProperty("treatedRight").GetInt32()));
        Assert.Equal((3, 2, 2), classes["hygiene"]);
        Assert.Equal((1, 1, 0), classes["domain"]);
        Assert.Equal((0, 0, 0), classes["growth"]);

        // Named for the page that shows them, retired or not: the page never asks for the catalogue.
        Assert.Equal("Секреты в репозитории", root.GetProperty("cards")[1].GetProperty("name").GetString());
        Assert.Equal("Предметная область", root.GetProperty("classes").EnumerateArray().Single(item => item.GetProperty("class").GetString() == "domain").GetProperty("name").GetString());
    }

    private static object Pick(string card, params string[] leaves) => new { card, leaves };

    private async Task SolveAsync(string task, object[] picks)
    {
        var start = await api.Client.PostAsJsonAsync(new Uri("/api/v1/attempts", UriKind.Relative), new { task }, TestContext.Current.CancellationToken);
        var id = (await start.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetProperty("id").GetString();
        var submit = await api.Client.PostAsJsonAsync(new Uri($"/api/v1/attempts/{id}/submit", UriKind.Relative), new { picks }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, submit.StatusCode);
    }

    private async Task<JsonDocument> ProgressAsync()
    {
        var response = await api.Client.GetAsync(new Uri("/api/v1/me/progress", UriKind.Relative), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }
}

/// <summary>Signed out, there is no progress to read.</summary>
public sealed class AnonymousProgressEndpointTests(AnonymousTestApi api) : IClassFixture<AnonymousTestApi>
{
    [Fact]
    public async Task ASignedOutCaller_IsUnauthenticated()
    {
        var response = await api.Client.GetAsync(new Uri("/api/v1/me/progress", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
