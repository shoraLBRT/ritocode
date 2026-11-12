using System.Net;
using System.Text.Json;
using Ritocode.Api.Tests.Infrastructure;

namespace Ritocode.Api.Tests.Endpoints;

/// <summary>
/// A result without the answer, the deliberate reveal, and the four statuses of a task (docs/SPEC.md
/// §4.3, §4.5, ADR 0017), through the real host. Each test keeps to its own task, since a reveal is
/// for good: the easy task (three findings), the medium one (one, <c>hardcoded-config</c>) and the clean one.
/// </summary>
public sealed class AnswerRevealTests(CleanTaskContentTestApi api) : IClassFixture<CleanTaskContentTestApi>
{
    // Secrets found and treated, money found and mistreated, an extra card, the swallowed error missed.
    private static readonly object EasyAnswer = new
    {
        picks = new object[]
        {
            new { card = "money-in-float", leaves = new[] { "manual.split" } },
            new { card = "secrets-in-repo", leaves = new[] { "auto.secrets" } },
            new { card = "hardcoded-config", leaves = new[] { "manual.extract-config" } },
        },
    };

    // Every card and leaf of the easy task's key and answer: none may appear before the reveal.
    private static readonly string[] Named =
    [
        "money-in-float", "secrets-in-repo", "hardcoded-config", "swallowed-error",
        "manual.split", "auto.secrets", "rule.conventions", "manual.representation", "manual.handle-errors", "manual.extract-config",
        "keyLeaves", "Десять счетов в месяц", "Масштаб маленький",
    ];

    [Fact]
    public async Task BeforeTheReveal_TheResultIsTheScoreAndTheCounts_AndNamesNoCardOrLeaf_ThenRevealingShowsItAll_AndTheTaskIsGaveUp()
    {
        Assert.Equal("new", await StatusAsync(ContentTestApi.EasyTask));
        var id = await StartAsync(ContentTestApi.EasyTask);

        // Neither the submit's answer nor a later read names anything; only the counts.
        using (var submitted = await (await api.Client.PostJsonAsync($"/api/v1/attempts/{id}/submit", EasyAnswer)).JsonAsync(HttpStatusCode.OK))
        {
            AssertNamesNothing(submitted.RootElement);
            Assert.Equal("tried", submitted.RootElement.GetProperty("taskStatus").GetString());
        }

        using (var read = await api.Client.GetJsonAsync($"/api/v1/attempts/{id}", HttpStatusCode.OK))
        {
            AssertNamesNothing(read.RootElement);
            var outcome = read.RootElement.GetProperty("outcome");
            Assert.Equal(64, outcome.GetProperty("total").GetInt32());
            Assert.Equal(120, outcome.GetProperty("maximum").GetInt32());
            Assert.Equal(3, outcome.GetProperty("present").GetInt32());
            Assert.Equal(2, outcome.GetProperty("found").GetInt32());
            Assert.Equal(1, outcome.GetProperty("treated").GetInt32());
            Assert.Equal(1, outcome.GetProperty("missed").GetInt32());
            Assert.Equal(1, outcome.GetProperty("extra").GetInt32());
            Assert.False(outcome.GetProperty("solved").GetBoolean());
        }

        Assert.Equal("tried", await StatusAsync(ContentTestApi.EasyTask));

        using (var revealed = await (await api.Client.PostJsonAsync($"/api/v1/attempts/{id}/reveal", null)).JsonAsync(HttpStatusCode.OK))
        {
            var root = revealed.RootElement;
            Assert.True(root.GetProperty("revealed").GetBoolean());
            Assert.Equal("gaveUp", root.GetProperty("taskStatus").GetString());
            Assert.Equal(4, root.GetProperty("result").GetProperty("cards").GetArrayLength());
            Assert.Equal(3, root.GetProperty("answer").GetProperty("picks").GetArrayLength());
            Assert.StartsWith("Масштаб маленький", root.GetProperty("review").GetProperty("lesson").GetString(), StringComparison.Ordinal);
        }

        Assert.Equal("gaveUp", await StatusAsync(ContentTestApi.EasyTask));

        // A later attempt is practice and opens in full at once; solving it does not undo the reveal.
        var practice = await StartAsync(ContentTestApi.EasyTask);
        var perfect = new
        {
            picks = new object[]
            {
                new { card = "money-in-float", leaves = new[] { "manual.representation" } },
                new { card = "secrets-in-repo", leaves = new[] { "auto.secrets" } },
                new { card = "swallowed-error", leaves = new[] { "manual.handle-errors" } },
            },
        };

        using (var again = await (await api.Client.PostJsonAsync($"/api/v1/attempts/{practice}/submit", perfect)).JsonAsync(HttpStatusCode.OK))
        {
            Assert.True(again.RootElement.GetProperty("practice").GetBoolean());
            Assert.True(again.RootElement.GetProperty("outcome").GetProperty("solved").GetBoolean());
            Assert.Equal(JsonValueKind.Object, again.RootElement.GetProperty("result").ValueKind);
            Assert.Equal("gaveUp", again.RootElement.GetProperty("taskStatus").GetString());
        }

        Assert.Equal("gaveUp", await StatusAsync(ContentTestApi.EasyTask));

        // Revealing again changes nothing.
        (await api.Client.PostJsonAsync($"/api/v1/attempts/{id}/reveal", null)).EnsureSuccessStatusCode();
        Assert.Equal("gaveUp", await StatusAsync(ContentTestApi.EasyTask));
    }

    [Fact]
    public async Task ASolvingAttempt_MakesTheTaskSolved_StillHidden_AndRevealingItLaterKeepsItSolved()
    {
        var tried = await StartAsync(ContentTestApi.MediumTask);
        var wrong = new { picks = new[] { new { card = "hardcoded-config", leaves = new[] { "accept.fits-context" } } } };
        (await api.Client.PostJsonAsync($"/api/v1/attempts/{tried}/submit", wrong)).EnsureSuccessStatusCode();
        Assert.Equal("tried", await StatusAsync(ContentTestApi.MediumTask));

        var id = await StartAsync(ContentTestApi.MediumTask);
        var right = new { picks = new[] { new { card = "hardcoded-config", leaves = new[] { "manual.extract-config", "manual.split" } } } };

        using (var submitted = await (await api.Client.PostJsonAsync($"/api/v1/attempts/{id}/submit", right)).JsonAsync(HttpStatusCode.OK))
        {
            // A wrong leaf beside a right one costs points and still solves it; nothing is named yet.
            var outcome = submitted.RootElement.GetProperty("outcome");
            Assert.True(outcome.GetProperty("solved").GetBoolean());
            Assert.True(outcome.GetProperty("total").GetInt32() < outcome.GetProperty("maximum").GetInt32());
            Assert.Equal(JsonValueKind.Null, submitted.RootElement.GetProperty("result").ValueKind);
            Assert.DoesNotContain("hardcoded-config", submitted.RootElement.GetRawText(), StringComparison.Ordinal);
            Assert.Equal("solved", submitted.RootElement.GetProperty("taskStatus").GetString());
        }

        Assert.Equal("solved", await StatusAsync(ContentTestApi.MediumTask));

        // Revealed from the earlier, failed attempt: the task was solved before, and stays so.
        using (var revealed = await (await api.Client.PostJsonAsync($"/api/v1/attempts/{tried}/reveal", null)).JsonAsync(HttpStatusCode.OK))
        {
            Assert.Equal("solved", revealed.RootElement.GetProperty("taskStatus").GetString());
        }

        Assert.Equal("solved", await StatusAsync(ContentTestApi.MediumTask));
    }

    [Fact]
    public async Task OnACleanTask_AnyPickIsTried_NothingPickedSolves_AndTheRevealSaysThereWasNothing()
    {
        var picked = await StartAsync(CleanTaskContentTestApi.CleanTask);
        var extra = new { picks = new[] { new { card = "hardcoded-config", leaves = new[] { "manual.extract-config" } } } };

        using (var submitted = await (await api.Client.PostJsonAsync($"/api/v1/attempts/{picked}/submit", extra)).JsonAsync(HttpStatusCode.OK))
        {
            Assert.Equal(1, submitted.RootElement.GetProperty("outcome").GetProperty("extra").GetInt32());
            Assert.DoesNotContain("hardcoded-config", submitted.RootElement.GetRawText(), StringComparison.Ordinal);
        }

        Assert.Equal("tried", await StatusAsync(CleanTaskContentTestApi.CleanTask));

        var nothing = await StartAsync(CleanTaskContentTestApi.CleanTask);
        (await api.Client.PostJsonAsync($"/api/v1/attempts/{nothing}/submit", new { picks = Array.Empty<object>() })).EnsureSuccessStatusCode();
        Assert.Equal("solved", await StatusAsync(CleanTaskContentTestApi.CleanTask));

        using var revealed = await (await api.Client.PostJsonAsync($"/api/v1/attempts/{nothing}/reveal", null)).JsonAsync(HttpStatusCode.OK);
        Assert.Equal(0, revealed.RootElement.GetProperty("result").GetProperty("maximum").GetInt32());
        Assert.Empty(revealed.RootElement.GetProperty("result").GetProperty("cards").EnumerateArray());
        Assert.Equal("solved", revealed.RootElement.GetProperty("taskStatus").GetString());
    }

    [Fact]
    public async Task AnOpenAttempt_HasNoAnswerToReveal_AndAMalformedIdIsNotFound()
    {
        var open = await StartAsync(ContentTestApi.EasyTask);

        using var refused = await (await api.Client.PostJsonAsync($"/api/v1/attempts/{open}/reveal", null)).JsonAsync(HttpStatusCode.Conflict);
        Assert.Equal("attempt_not_submitted", refused.RootElement.GetProperty("code").GetString());

        using var malformed = await (await api.Client.PostJsonAsync("/api/v1/attempts/not-an-id/reveal", null)).JsonAsync(HttpStatusCode.NotFound);
        Assert.Equal("attempt_not_found", malformed.RootElement.GetProperty("code").GetString());
    }

    private static void AssertNamesNothing(JsonElement attempt)
    {
        Assert.False(attempt.GetProperty("revealed").GetBoolean());

        foreach (var field in new[] { "answer", "result", "review" })
        {
            Assert.Equal(JsonValueKind.Null, attempt.GetProperty(field).ValueKind);
        }

        Assert.Empty(attempt.GetProperty("signalledCards").EnumerateArray());

        var json = attempt.GetRawText();
        foreach (var name in Named)
        {
            Assert.DoesNotContain(name, json, StringComparison.Ordinal);
        }
    }

    private async Task<string?> StatusAsync(string task)
    {
        using var tasks = await api.Client.GetJsonAsync("/api/v1/tasks", HttpStatusCode.OK);
        return tasks.RootElement.GetProperty("items").EnumerateArray()
            .Single(item => item.GetProperty("slug").GetString() == task)
            .GetProperty("status").GetString();
    }

    private async Task<Guid> StartAsync(string task)
    {
        using var body = await (await api.Client.PostJsonAsync("/api/v1/attempts", new { task })).JsonAsync(HttpStatusCode.Created);
        return body.RootElement.GetProperty("id").GetGuid();
    }
}
