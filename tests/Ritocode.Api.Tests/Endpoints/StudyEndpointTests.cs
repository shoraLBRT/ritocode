using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ritocode.Api.Tests.Infrastructure;
using Ritocode.Modules.Attempts.Domain;
using Ritocode.Modules.Attempts.Persistence;
using Ritocode.TestSupport;

namespace Ritocode.Api.Tests.Endpoints;

/// <summary>
/// <c>GET /api/v1/me/studied</c>, <c>PUT</c> and <c>DELETE /api/v1/me/studied/{card}</c> (docs/SPEC.md
/// §4.2 › Studying, ADR 0018): the caller's marks «Изучено», on the reference fixture's cards. Each
/// test marks cards no other test here marks, since they share the caller.
/// </summary>
public sealed class StudyEndpointTests(ContentTestApi api) : IClassFixture<ContentTestApi>
{
    [Fact]
    public async Task ACard_IsMarked_MarkedAgainKeepingItsDate_AndTakenBack()
    {
        using var marked = await JsonAsync(await PutAsync("money-in-float"), HttpStatusCode.OK);
        Assert.Equal("money-in-float", marked.RootElement.GetProperty("card").GetString());
        var studiedAt = marked.RootElement.GetProperty("studiedAt").GetDateTimeOffset();

        using var again = await JsonAsync(await PutAsync("money-in-float"), HttpStatusCode.OK);
        Assert.Equal(studiedAt, again.RootElement.GetProperty("studiedAt").GetDateTimeOffset());

        var listed = await StudiedAsync();
        Assert.Equal(studiedAt, listed["money-in-float"]);

        Assert.Equal(HttpStatusCode.NoContent, (await DeleteAsync("money-in-float")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await DeleteAsync("money-in-float")).StatusCode);

        Assert.DoesNotContain("money-in-float", (await StudiedAsync()).Keys);

        await using var scope = api.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AttemptsDbContext>();
        Assert.False(await context.StudyMarks.AnyAsync(mark => mark.CardSlug == "money-in-float", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AnUnknownCard_IsNotFound_InTheApisErrorShape()
    {
        using var refused = await JsonAsync(await PutAsync("no-such-card"), HttpStatusCode.NotFound);

        Assert.Equal("card_not_found", refused.RootElement.GetProperty("code").GetString());
        Assert.True(refused.RootElement.TryGetProperty("detail", out _));
        Assert.DoesNotContain("no-such-card", (await StudiedAsync()).Keys);

        // Taking back a mark nobody made is the same as taking back one that was made.
        Assert.Equal(HttpStatusCode.NoContent, (await DeleteAsync("no-such-card")).StatusCode);
    }

    [Fact]
    public async Task AnotherUsersMarks_AreNotVisible_NorTakenBack()
    {
        var someoneElse = Guid.CreateVersion7();

        await using (var scope = api.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AttemptsDbContext>();
            context.StudyMarks.Add(StudyMark.Of(someoneElse, "swallowed-error", DateTimeOffset.UtcNow));
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        Assert.DoesNotContain("swallowed-error", (await StudiedAsync()).Keys);
        Assert.Equal(HttpStatusCode.NoContent, (await DeleteAsync("swallowed-error")).StatusCode);

        await using (var scope = api.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AttemptsDbContext>();
            Assert.True(await context.StudyMarks.AnyAsync(mark => mark.UserId == someoneElse, TestContext.Current.CancellationToken));
        }
    }

    [Fact]
    public async Task TheMarks_AreListedOldestFirst()
    {
        using (await JsonAsync(await PutAsync("secrets-in-repo"), HttpStatusCode.OK))
        {
        }

        using (await JsonAsync(await PutAsync("hardcoded-config"), HttpStatusCode.OK))
        {
        }

        var cards = (await StudiedAsync()).Keys.Where(card => card is "secrets-in-repo" or "hardcoded-config");

        Assert.Equal(["secrets-in-repo", "hardcoded-config"], cards);
    }

    private Task<HttpResponseMessage> PutAsync(string card) =>
        api.Client.PutAsync(new Uri($"/api/v1/me/studied/{card}", UriKind.Relative), null, TestContext.Current.CancellationToken);

    private Task<HttpResponseMessage> DeleteAsync(string card) =>
        api.Client.DeleteAsync(new Uri($"/api/v1/me/studied/{card}", UriKind.Relative), TestContext.Current.CancellationToken);

    private async Task<Dictionary<string, DateTimeOffset>> StudiedAsync() => await StudyTests.StudiedAsync(api.Client);

    private static Task<JsonDocument> JsonAsync(HttpResponseMessage response, HttpStatusCode expected) => StudyTests.JsonAsync(response, expected);
}

/// <summary>A mark on a card later retired stays stored, is not shown, and shows again when the card returns.</summary>
public sealed class RetiredCardStudyTests(RetiringCardContentTestApi api) : IClassFixture<RetiringCardContentTestApi>
{
    [Fact]
    public async Task AMarkOnARetiredCard_IsKept_ButNotShown_AndTheCardCannotBeMarked()
    {
        var marked = await api.Client.PutAsync(new Uri($"/api/v1/me/studied/{RetiringCardContentTestApi.Card}", UriKind.Relative), null, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, marked.StatusCode);

        api.RetireCard();
        await api.IngestAsync("second");

        Assert.DoesNotContain(RetiringCardContentTestApi.Card, (await StudyTests.StudiedAsync(api.Client)).Keys);

        var again = await api.Client.PutAsync(new Uri($"/api/v1/me/studied/{RetiringCardContentTestApi.Card}", UriKind.Relative), null, TestContext.Current.CancellationToken);
        using var refused = await StudyTests.JsonAsync(again, HttpStatusCode.NotFound);
        Assert.Equal("card_not_found", refused.RootElement.GetProperty("code").GetString());

        await using (var scope = api.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AttemptsDbContext>();
            Assert.True(await context.StudyMarks.AnyAsync(mark => mark.CardSlug == RetiringCardContentTestApi.Card, TestContext.Current.CancellationToken));
        }

        api.RestoreCard();
        await api.IngestAsync("third");

        Assert.Contains(RetiringCardContentTestApi.Card, (await StudyTests.StudiedAsync(api.Client)).Keys);
    }
}

/// <summary>Signed out, there are no marks to read or change.</summary>
public sealed class AnonymousStudyTests(AnonymousTestApi api) : IClassFixture<AnonymousTestApi>
{
    [Fact]
    public async Task ASignedOutCaller_IsUnauthenticated()
    {
        var path = new Uri("/api/v1/me/studied/money-in-float", UriKind.Relative);

        Assert.Equal(HttpStatusCode.Unauthorized, (await api.Client.GetAsync(new Uri("/api/v1/me/studied", UriKind.Relative), TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.Client.PutAsync(path, null, TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.Client.DeleteAsync(path, TestContext.Current.CancellationToken)).StatusCode);
    }
}

/// <summary>As <see cref="ContentTestApi"/>, with one more card, in no task, that a test retires and brings back.</summary>
public sealed class RetiringCardContentTestApi(PostgresTestServer postgres) : ContentTestApi(postgres)
{
    public const string Card = "retiring-card";

    private const string CardYaml = "class: hygiene\nweight: 1\n";

    private const string CardText =
        "---\nname: Уходящая карточка\nsummary: Карточка, которую тест снимает с публикации.\n---\n\n"
        + "## Signs\n\nПризнак.\n\n## Cost\n\nЦена.\n\n## Acceptable when\n\nНикогда.\n\n## Treatment\n\nЛечение.\n";

    public void RetireCard() => Directory.Delete(Path.Combine(ContentRoot, "problems", Card), recursive: true);

    public void RestoreCard() => AddContent();

    protected override void AddContent()
    {
        Write($"problems/{Card}/card.yaml", CardYaml);
        Write($"problems/{Card}/ru.md", CardText);
    }
}

internal static class StudyTests
{
    /// <summary>The caller's marks, by card, in the order the API lists them.</summary>
    public static async Task<Dictionary<string, DateTimeOffset>> StudiedAsync(HttpClient client)
    {
        using var listed = await JsonAsync(await client.GetAsync(new Uri("/api/v1/me/studied", UriKind.Relative), TestContext.Current.CancellationToken), HttpStatusCode.OK);

        return listed.RootElement.GetProperty("cards").EnumerateArray()
            .ToDictionary(mark => mark.GetProperty("card").GetString()!, mark => mark.GetProperty("studiedAt").GetDateTimeOffset());
    }

    public static async Task<JsonDocument> JsonAsync(HttpResponseMessage response, HttpStatusCode expected)
    {
        var text = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.True(expected == response.StatusCode, $"Expected {expected}, got {(int)response.StatusCode}: {text}");
        return JsonDocument.Parse(text);
    }
}
