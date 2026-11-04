using Ritocode.Modules.Content.Format;

namespace Ritocode.Modules.Content.Tests.Format;

/// <summary>
/// Every rule of docs/CONTENT_FORMAT.md §7, each broken once in an otherwise valid copy of the
/// reference content.
/// </summary>
public sealed class ContentValidationTests
{
    private const string Card = "problems/secrets-in-repo";
    private const string Task = "tasks/invoice-mailer-monthly";
    private const string Material = "materials/invoice-mailer";

    // ---- errors ------------------------------------------------------------------------------

    [Fact]
    public void AMissingContentRoot_IsAnError()
    {
        var (_, report) = ContentLoader.Load(Path.Combine(Path.GetTempPath(), "no-such-content-" + Guid.NewGuid().ToString("N")));

        Assert.Single(report.Errors);
    }

    [Fact]
    public void AMalformedSlug_IsAnError_AndTheItemIsSkipped()
    {
        using var content = TempContent.FromReference().Move(Card, "problems/Secrets_In_Repo");

        var (set, report) = content.Load();

        AssertError(report, "problems/Secrets_In_Repo", "not a valid slug");
        Assert.DoesNotContain(set.Cards, card => card.Slug == "Secrets_In_Repo");
    }

    [Fact]
    public void AMissingDataFile_IsAnError()
    {
        using var content = TempContent.FromReference().Delete($"{Card}/card.yaml");

        AssertError(content.Load().Report, $"{Card}/card.yaml", "missing");
    }

    [Fact]
    public void AMissingDefaultLocale_IsAnError()
    {
        using var content = TempContent.FromReference().Move($"{Card}/ru.md", $"{Card}/en.md");

        AssertError(content.Load().Report, $"{Card}/ru.md", "default locale is missing");
    }

    [Fact]
    public void AnotherLocale_MayBeIncomplete()
    {
        using var content = TempContent.FromReference().Write($"{Card}/en.md", "---\nname: Secrets in the repository\n---\n");

        Assert.Empty(content.Load().Report.Errors);
    }

    [Fact]
    public void AnUnknownFile_InAnItem_IsAnError()
    {
        using var content = TempContent.FromReference().Write($"{Card}/notes.txt", "draft");

        AssertError(content.Load().Report, $"{Card}/notes.txt", "Unknown entry");
    }

    [Fact]
    public void ARequiredField_MissingInTheDefaultLocale_IsAnError()
    {
        using var content = TempContent.FromReference().Replace($"{Card}/ru.md", "name: Секреты в репозитории\n", string.Empty);

        AssertError(content.Load().Report, $"{Card}/ru.md", "'name' is required");
    }

    [Fact]
    public void ARequiredSection_MissingInTheDefaultLocale_IsAnError()
    {
        using var content = TempContent.FromReference().Replace(
            $"{Card}/ru.md",
            "## Cost\n\n- Пароль в истории git остаётся навсегда.\n- Доступ получает каждый, у кого есть доступ к репозиторию.\n\n",
            string.Empty);

        AssertError(content.Load().Report, $"{Card}/ru.md", "'## Cost' is required");
    }

    [Fact]
    public void AHeadingWrittenTwice_IsAnError()
    {
        using var content = TempContent.FromReference().Replace($"{Card}/ru.md", "## Cost\n", "## Sources\n");

        AssertError(content.Load().Report, $"{Card}/ru.md", "'## Sources' appears twice");
    }

    [Fact]
    public void AnUnknownSectionHeading_IsAnError_SoATypoNeverDropsAField()
    {
        using var content = TempContent.FromReference().Replace($"{Card}/ru.md", "## Detection", "## Detecton");

        AssertError(content.Load().Report, $"{Card}/ru.md", "Unknown section '## Detecton'");
    }

    [Fact]
    public void ASummaryOverSeveralLines_IsAnError()
    {
        using var content = TempContent.FromReference().Replace($"{Card}/ru.md", "summary: >-", "summary: |-");

        AssertError(content.Load().Report, $"{Card}/ru.md", "one line");
    }

    [Fact]
    public void TextBeforeTheFirstSection_IsAnError()
    {
        using var content = TempContent.FromReference().Replace($"{Card}/ru.md", "---\n\n## Signs", "---\nstray text\n\n## Signs");

        AssertError(content.Load().Report, $"{Card}/ru.md", "Text outside");
    }

    [Fact]
    public void AFileWithoutFrontMatter_IsAnError()
    {
        using var content = TempContent.FromReference().Write($"{Card}/ru.md", "## Signs\n\nSomething.\n");

        AssertError(content.Load().Report, $"{Card}/ru.md", "front matter");
    }

    [Fact]
    public void AnUnknownYamlKey_IsAnError()
    {
        using var content = TempContent.FromReference().Write($"{Card}/card.yaml", "class: hygiene\nweight: 3\nseverity: high\n");

        AssertError(content.Load().Report, $"{Card}/card.yaml", "severity");
    }

    [Theory]
    [InlineData("keywords: [пароль, type: ignore, ключ]", "line 6, column 20: 'keywords': ")]
    [InlineData("keywords:\n  - пароль\n  - type: ignore", "line 8, column 5: 'keywords': ")]
    public void AnUnquotedColon_InAKeyword_IsAnError_ThatNamesTheFieldAndSaysToQuoteIt(string keywords, string where)
    {
        using var content = TempContent.FromReference().Replace($"{Card}/ru.md", "keywords: [пароль, токен, ключ, credentials]", keywords);

        AssertError(
            content.Load().Report,
            $"{Card}/ru.md",
            $"{where}YAML reads 'type: ignore' as a key and a value, not as text. Put a value that contains ': ' in quotes, as in \"type: ignore\".");
    }

    [Fact]
    public void AnUnquotedColon_InAQuotedKeyword_IsFine()
    {
        using var content = TempContent.FromReference().Replace($"{Card}/ru.md", "[пароль, токен,", "[пароль, \"type: ignore\",");

        Assert.Empty(content.Load().Report.Errors);
    }

    [Theory]
    [InlineData(Card, "name: Секреты в репозитории", "name: Секреты: в репозитории", "line 2, column")]
    [InlineData(Task, "title: Счёт клиенту по почте", "title: Счёт: клиенту по почте", "line 2, column")]
    public void AnUnquotedColon_InAText_IsAnError_ThatSaysToQuoteIt(string item, string text, string broken, string where)
    {
        using var content = TempContent.FromReference().Replace($"{item}/ru.md", text, broken);

        var (_, report) = content.Load();

        AssertError(report, $"{item}/ru.md", where);
        AssertError(report, $"{item}/ru.md", $"Put a value that contains ': ' in quotes, as in {broken[..(broken.IndexOf(':') + 2)]}\"{broken[(broken.IndexOf(':') + 2)..]}\".");
    }

    [Theory]
    [InlineData("name: Секреты в репозитории", "name: {Секреты: в репозитории}", "'name': YAML reads 'Секреты: в репозитории' as a key and a value")]
    [InlineData("name: Секреты в репозитории", "name: [Секреты, ключи]", "'name': YAML reads '[Секреты, ключи]' as a list")]
    public void AMappingOrAList_WhereTextBelongs_IsAnError_ThatNamesTheField(string text, string broken, string fragment)
    {
        using var content = TempContent.FromReference().Replace($"{Card}/ru.md", text, broken);

        AssertError(content.Load().Report, $"{Card}/ru.md", fragment);
    }

    [Fact]
    public void AClassNotInTheTaxonomy_IsAnError()
    {
        using var content = TempContent.FromReference().Write($"{Card}/card.yaml", "class: security\nweight: 3\n");

        AssertError(content.Load().Report, $"{Card}/card.yaml", "'security' is not a class");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    public void AWeightOutsideOneToThree_IsAnError(int weight)
    {
        using var content = TempContent.FromReference().Write($"{Card}/card.yaml", $"class: hygiene\nweight: {weight}\n");

        AssertError(content.Load().Report, $"{Card}/card.yaml", "weight");
    }

    [Fact]
    public void AFindingNamingACardThatDoesNotExist_IsAnError()
    {
        using var content = TempContent.FromReference().Delete("problems/swallowed-error");

        AssertError(content.Load().Report, $"{Task}/task.yaml", "'swallowed-error' names a card that does not exist");
    }

    [Fact]
    public void AFindingNamingALeafThatDoesNotExist_IsAnError()
    {
        using var content = TempContent.FromReference().Replace($"{Task}/task.yaml", "manual.representation", "manual.rewrite");

        AssertError(content.Load().Report, $"{Task}/task.yaml", "'manual.rewrite', which is not a leaf");
    }

    [Fact]
    public void AFindingWithNoLeaves_IsAnError()
    {
        using var content = TempContent.FromReference().Replace($"{Task}/task.yaml", "leaves: [manual.handle-errors]", "leaves: []");

        AssertError(content.Load().Report, $"{Task}/task.yaml", "has no leaves");
    }

    [Fact]
    public void ACardListedTwiceInOneTask_IsAnError()
    {
        using var content = TempContent.FromReference().Replace(
            $"{Task}/task.yaml",
            "  - card: swallowed-error",
            "  - card: money-in-float\n    leaves: [accept.fits-context]\n  - card: swallowed-error");

        AssertError(content.Load().Report, $"{Task}/task.yaml", "listed twice");
    }

    [Fact]
    public void ANoteNamingACardThatIsNotAFinding_IsAnError()
    {
        using var content = TempContent.FromReference().Replace($"{Task}/ru.md", "### money-in-float", "### hardcoded-config");

        AssertError(content.Load().Report, $"{Task}/ru.md", "'### hardcoded-config' names a card that is not among the task's findings");
    }

    [Fact]
    public void ACleanTask_IsValid_ButFindingsMustBeStated()
    {
        using var clean = TempContent.FromReference()
            .Write($"{Task}/task.yaml", "material: invoice-mailer\ndifficulty: medium\nfindings: []\n")
            .Replace($"{Task}/ru.md", "## Notes\n\n### money-in-float\n\nДесять счетов в месяц — а копейки всё равно расходятся с бухгалтерией.\n\n", string.Empty);

        Assert.Empty(clean.Load().Report.Errors);

        using var unstated = TempContent.FromReference()
            .Write($"{Task}/task.yaml", "material: invoice-mailer\ndifficulty: medium\n")
            .Replace($"{Task}/ru.md", "### money-in-float", "### nothing");

        AssertError(unstated.Load().Report, $"{Task}/task.yaml", "'findings' is required");
    }

    [Fact]
    public void AnUnknownDifficulty_IsAnError()
    {
        using var content = TempContent.FromReference().Replace($"{Task}/task.yaml", "difficulty: easy", "difficulty: expert");

        AssertError(content.Load().Report, $"{Task}/task.yaml", "'difficulty' must be");
    }

    [Fact]
    public void ATaskWhoseMaterialDoesNotExist_IsAnError()
    {
        using var content = TempContent.FromReference().Replace($"{Task}/task.yaml", "material: invoice-mailer", "material: invoice-sender");

        AssertError(content.Load().Report, $"{Task}/task.yaml", "'invoice-sender' does not exist");
    }

    [Fact]
    public void AMaterialFileThatIsNotUtf8_IsAnError()
    {
        using var content = TempContent.FromReference().WriteBytes($"{Material}/files/legacy.py", [0x70, 0xC3, 0x28, 0x0A]);

        AssertError(content.Load().Report, $"{Material}/files/legacy.py", "not UTF-8");
    }

    [Fact]
    public void ABinaryMaterialFile_IsAnError()
    {
        using var content = TempContent.FromReference().WriteBytes($"{Material}/files/logo.bin", [0x41, 0x00, 0x42]);

        AssertError(content.Load().Report, $"{Material}/files/logo.bin", "binary");
    }

    [Fact]
    public void AMaterialInAnUnsupportedLanguage_IsAnError()
    {
        using var content = TempContent.FromReference().Replace($"{Material}/material.yaml", "language: python", "language: csharp");

        AssertError(content.Load().Report, $"{Material}/material.yaml", "'csharp' is not a supported language");
    }

    [Fact]
    public void ALeafWithoutALabelInTheDefaultLocale_IsAnError()
    {
        using var content = TempContent.FromReference().Replace("taxonomy/ru.yaml", "      debt: записать как долг с условием пересмотра\n", string.Empty);

        AssertError(content.Load().Report, "taxonomy/ru.yaml", "'accept.debt' has no label");
    }

    [Fact]
    public void ALeafListedTwice_IsAnError()
    {
        using var content = TempContent.FromReference().Replace("taxonomy/treatments.yaml", "[fits-context, not-worth-it, debt]", "[fits-context, not-worth-it, debt, debt]");

        AssertError(content.Load().Report, "taxonomy/treatments.yaml", "'accept.debt' is listed twice");
    }

    [Fact]
    public void EveryFault_IsReportedInOneRun()
    {
        using var content = TempContent.FromReference()
            .Write($"{Card}/card.yaml", "class: security\nweight: 9\n")
            .Replace($"{Task}/task.yaml", "manual.representation", "manual.rewrite")
            .Replace($"{Material}/material.yaml", "language: python", "language: csharp");

        var errors = content.Load().Report.Errors.ToList();

        Assert.True(errors.Count >= 4, string.Join("\n", errors));
        Assert.Contains(errors, issue => issue.Path.StartsWith(Card, StringComparison.Ordinal));
        Assert.Contains(errors, issue => issue.Path.StartsWith(Task, StringComparison.Ordinal));
        Assert.Contains(errors, issue => issue.Path.StartsWith(Material, StringComparison.Ordinal));
    }

    // ---- warnings ----------------------------------------------------------------------------

    [Fact]
    public void MaterialOverItsDifficultysBand_IsAWarning()
    {
        using var content = TempContent.FromReference()
            .Write($"{Material}/files/extra.py", string.Concat(Enumerable.Repeat("x = 1\n", 70)));

        var report = content.Load().Report;

        Assert.Empty(report.Errors);
        AssertWarning(report, $"{Task}/task.yaml", "90 lines in 2 files; a easy task's band is at most 80 lines");
    }

    [Fact]
    public void ALineLongerThan79Characters_IsAWarning()
    {
        using var content = TempContent.FromReference()
            .Write($"{Material}/files/wide.py", "ok = 1\n" + new string('x', 80) + "\n");

        var report = content.Load().Report;

        Assert.Empty(report.Errors);
        AssertWarning(report, $"{Material}/files/wide.py", "the first at line 2");
    }

    [Fact]
    public void AMaterialNoTaskUses_IsAWarning()
    {
        using var content = TempContent.FromReference()
            .Write("materials/spare/material.yaml", "language: python\n")
            .Write("materials/spare/files/main.py", "print(1)\n");

        var report = content.Load().Report;

        Assert.Empty(report.Errors);
        AssertWarning(report, "materials/spare/material.yaml", "No task uses");
    }

    [Fact]
    public void AMediumTask_NeedsNoShortlist()
    {
        using var content = TempContent.FromReference().Replace($"{Task}/task.yaml", "difficulty: easy", "difficulty: medium");

        Assert.Empty(content.Load().Report.Issues);
    }

    private static void AssertError(ContentReport report, string path, string fragment) =>
        AssertIssue(report.Errors, path, fragment, report);

    private static void AssertWarning(ContentReport report, string path, string fragment) =>
        AssertIssue(report.Warnings, path, fragment, report);

    private static void AssertIssue(IEnumerable<ContentIssue> issues, string path, string fragment, ContentReport report) =>
        Assert.True(
            issues.Any(issue => issue.Path == path && issue.Message.Contains(fragment, StringComparison.Ordinal)),
            $"Expected an issue at '{path}' containing '{fragment}'. Got:\n{string.Join("\n", report.Issues)}");
}
