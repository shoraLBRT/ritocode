using Ritocode.Modules.Problems.ContentFormat;

namespace Ritocode.Modules.Problems.Tests.ContentFormat;

/// <summary>
/// The reference content — the example of docs/CONTENT_FORMAT.md over the committed taxonomy — and
/// the committed content tree itself, loaded as they ship.
/// </summary>
public sealed class ReferenceContentTests
{
    [Fact]
    public void TheReferenceContent_HasNoErrors_AndOnlyTheShortlistWarning()
    {
        var (_, report) = ContentLoader.Load(TempContent.ReferenceRoot);

        Assert.Empty(report.Errors);

        // Four cards cannot fill an easy task's shortlist; that warning is the point of having it.
        var warning = Assert.Single(report.Warnings);
        Assert.Equal("tasks/invoice-mailer-monthly/task.yaml", warning.Path);
        Assert.Contains("shortlist", warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TheReferenceContent_ReadsEveryKindOfItem()
    {
        var (content, _) = ContentLoader.Load(TempContent.ReferenceRoot);

        Assert.Equal(
            ["hardcoded-config", "money-in-float", "secrets-in-repo", "swallowed-error"],
            content.Cards.Select(card => card.Slug));

        var secrets = content.Cards.Single(card => card.Slug == "secrets-in-repo");
        Assert.Equal("hygiene", secrets.Class);
        Assert.Equal(3, secrets.Weight);

        var text = secrets.Texts[ContentRules.DefaultLocale];
        Assert.Equal("Секреты в репозитории", text.Name);
        Assert.DoesNotContain('\n', text.Summary);
        Assert.EndsWith("«Хардкод конфигурации».", text.Summary, StringComparison.Ordinal);
        Assert.Equal(["пароль", "токен", "ключ", "credentials"], text.Keywords);
        Assert.Equal(
            [CardSection.Signs, CardSection.WhyAiDoesIt, CardSection.Cost, CardSection.AcceptableWhen, CardSection.Detection, CardSection.Treatment, CardSection.Sources],
            text.Sections.Keys.Order());
        Assert.Equal("Никогда.", text.Sections[CardSection.AcceptableWhen]);

        var material = Assert.Single(content.Materials);
        Assert.Equal("python", material.Language);
        var file = Assert.Single(material.Files);
        Assert.Equal("invoice.py", file.Path);
        Assert.Equal(20, file.LineCount);

        var task = Assert.Single(content.Tasks);
        Assert.Equal("invoice-mailer", task.Material);
        Assert.Equal(TaskDifficulty.Easy, task.Difficulty);
        Assert.Equal(["auto.secrets", "rule.conventions"], task.Findings[0].Leaves);
        Assert.Equal(["secrets-in-repo", "money-in-float", "swallowed-error"], task.Findings.Select(finding => finding.Card));

        var taskText = task.Texts[ContentRules.DefaultLocale];
        Assert.Equal("Счёт клиенту по почте", taskText.Title);
        Assert.StartsWith("«Напиши скрипт", taskText.Brief, StringComparison.Ordinal);
        Assert.Equal(["money-in-float"], taskText.Notes.Keys);
        Assert.NotNull(taskText.Lesson);
    }

    [Fact]
    public void TheCommittedContent_HasNoErrors_AndTheTaxonomyOfTheSpec()
    {
        var (content, report) = ContentLoader.Load(TempContent.CommittedRoot);

        Assert.Empty(report.Errors);

        Assert.Equal(
            ["disproportion", "project-knowledge", "hygiene", "growth", "false-confidence", "domain"],
            content.Taxonomy.Classes);
        Assert.Equal(["brief", "rule", "auto", "manual", "accept"], content.Taxonomy.Branches.Select(branch => branch.Id));
        Assert.Equal(29, content.Taxonomy.LeafIds.Count());
        Assert.Contains("accept.fits-context", content.Taxonomy.LeafIds);

        var labels = content.Taxonomy.Texts[ContentRules.DefaultLocale];
        Assert.All(content.Taxonomy.Branches, branch => Assert.Equal(branch.Leaves.Count, labels.Branches[branch.Id].Leaves.Count));
    }
}
