using System.Text;

namespace Ritocode.Modules.Content.Format;

/// <summary>
/// Reads a content tree (docs/CONTENT_FORMAT.md) from disk and checks it, reporting every fault at
/// once. Loading is inspection only: nothing in a material is ever executed.
/// </summary>
public static class ContentLoader
{
    public const string TaxonomyDirectory = "taxonomy";
    public const string CardsDirectory = "problems";
    public const string MaterialsDirectory = "materials";
    public const string TasksDirectory = "tasks";

    private const string ClassesFile = "classes.yaml";
    private const string TreatmentsFile = "treatments.yaml";
    private const string CardFile = "card.yaml";
    private const string MaterialFile = "material.yaml";
    private const string MaterialFilesDirectory = "files";
    private const string TaskFile = "task.yaml";

    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private static readonly Dictionary<string, CardSection> CardHeadings = new(StringComparer.Ordinal)
    {
        ["Signs"] = CardSection.Signs,
        ["Why AI does it"] = CardSection.WhyAiDoesIt,
        ["Cost"] = CardSection.Cost,
        ["Acceptable when"] = CardSection.AcceptableWhen,
        ["Detection"] = CardSection.Detection,
        ["Treatment"] = CardSection.Treatment,
        ["Sources"] = CardSection.Sources,
        ["Counter-arguments"] = CardSection.CounterArguments,
    };

    private static readonly CardSection[] RequiredCardSections =
        [CardSection.Signs, CardSection.Cost, CardSection.AcceptableWhen, CardSection.Treatment];

    private const string ContextHeading = "Context";
    private const string BriefHeading = "Brief";
    private const string NotesHeading = "Notes";
    private const string LessonHeading = "Lesson";

    private static readonly HashSet<string> TaskHeadings =
        new(StringComparer.Ordinal) { ContextHeading, BriefHeading, NotesHeading, LessonHeading };

    /// <summary>Loads and checks the tree at <paramref name="root"/>.</summary>
    /// <returns>
    /// What could be read — an item with a fault that prevents reading it is left out — and the
    /// report. Content with any error in its report must not be ingested.
    /// </returns>
    public static (ContentSet Content, ContentReport Report) Load(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);

        var report = new ContentReport();
        var full = Path.GetFullPath(root);

        if (!Directory.Exists(full))
        {
            report.Error(".", $"There is no content directory at '{full}'.");
            return (new ContentSet(Taxonomy.Empty, [], [], []), report);
        }

        var tree = new Tree(full, report);
        var taxonomy = LoadTaxonomy(tree);
        var cards = tree.Items(CardsDirectory, LoadCard);
        var materials = tree.Items(MaterialsDirectory, LoadMaterial);
        var tasks = tree.Items(TasksDirectory, LoadTask);

        var content = new ContentSet(taxonomy, cards, materials, tasks);
        ContentValidator.Validate(content, report);

        return (content, report);
    }

    private static Taxonomy LoadTaxonomy(Tree tree)
    {
        var directory = Path.Combine(tree.Root, TaxonomyDirectory);

        if (!Directory.Exists(directory))
        {
            tree.Report.Error(TaxonomyDirectory, "The taxonomy directory is missing.");
            return Taxonomy.Empty;
        }

        var classes = ReadClasses(tree, directory);
        var branches = ReadBranches(tree, directory);
        var texts = new Dictionary<string, TaxonomyText>(StringComparer.Ordinal);

        foreach (var file in Directory.EnumerateFiles(directory).Order(StringComparer.Ordinal))
        {
            var name = Path.GetFileName(file);

            if (name is ClassesFile or TreatmentsFile)
            {
                continue;
            }

            var path = tree.Relative(file);
            var locale = Path.GetFileNameWithoutExtension(name);

            if (!name.EndsWith(".yaml", StringComparison.Ordinal) || !ContentRules.IsLocale(locale))
            {
                tree.Report.Error(path, "Unknown file: the taxonomy holds classes.yaml, treatments.yaml and one <locale>.yaml per locale.");
                continue;
            }

            var document = ContentYaml.Read<TaxonomyLocaleDocument>(File.ReadAllText(file), path, tree.Report);

            if (document is not null)
            {
                texts[locale] = TaxonomyTexts(document, locale, classes, branches, path, tree.Report);
            }
        }

        if (!texts.ContainsKey(ContentRules.DefaultLocale))
        {
            tree.Report.Error(
                $"{TaxonomyDirectory}/{ContentRules.DefaultLocale}.yaml",
                "The labels in the default locale are missing.");
        }

        return new Taxonomy(classes, branches, texts);
    }

    private static List<string> ReadClasses(Tree tree, string directory)
    {
        var path = $"{TaxonomyDirectory}/{ClassesFile}";
        var document = tree.ReadYaml<ClassesDocument>(Path.Combine(directory, ClassesFile), path);
        var classes = new List<string>();

        if (document is null)
        {
            return classes;
        }

        if (document.Classes is not { Length: > 0 })
        {
            tree.Report.Error(path, "'classes' must list at least one class.");
            return classes;
        }

        foreach (var id in document.Classes)
        {
            if (!ContentRules.IsSlug(id ?? string.Empty))
            {
                tree.Report.Error(path, $"'{id}' is not a valid class identifier.");
            }
            else if (classes.Contains(id!, StringComparer.Ordinal))
            {
                tree.Report.Error(path, $"The class '{id}' is listed twice.");
            }
            else
            {
                classes.Add(id!);
            }
        }

        return classes;
    }

    private static List<TreatmentBranch> ReadBranches(Tree tree, string directory)
    {
        var path = $"{TaxonomyDirectory}/{TreatmentsFile}";
        var document = tree.ReadYaml<TreatmentsDocument>(Path.Combine(directory, TreatmentsFile), path);
        var branches = new List<TreatmentBranch>();

        if (document is null)
        {
            return branches;
        }

        if (document.Branches is not { Length: > 0 })
        {
            tree.Report.Error(path, "'branches' must list at least one branch.");
            return branches;
        }

        foreach (var branch in document.Branches)
        {
            var id = branch.Id ?? string.Empty;

            if (!ContentRules.IsSlug(id))
            {
                tree.Report.Error(path, $"'{id}' is not a valid branch identifier.");
                continue;
            }

            if (branches.Any(existing => existing.Id == id))
            {
                tree.Report.Error(path, $"The branch '{id}' is listed twice.");
                continue;
            }

            var leaves = new List<string>();

            foreach (var leaf in branch.Leaves ?? [])
            {
                if (!ContentRules.IsSlug(leaf ?? string.Empty))
                {
                    tree.Report.Error(path, $"'{leaf}' in branch '{id}' is not a valid leaf identifier.");
                }
                else if (leaves.Contains(leaf!, StringComparer.Ordinal))
                {
                    tree.Report.Error(path, $"The leaf '{id}.{leaf}' is listed twice.");
                }
                else
                {
                    leaves.Add(leaf!);
                }
            }

            if (leaves.Count == 0)
            {
                tree.Report.Error(path, $"The branch '{id}' has no leaves.");
            }

            branches.Add(new TreatmentBranch(id, leaves));
        }

        return branches;
    }

    private static TaxonomyText TaxonomyTexts(
        TaxonomyLocaleDocument document,
        string locale,
        IReadOnlyList<string> classes,
        IReadOnlyList<TreatmentBranch> branches,
        string path,
        ContentReport report)
    {
        var isDefault = locale == ContentRules.DefaultLocale;
        var classTexts = new Dictionary<string, LabelText>(StringComparer.Ordinal);
        var branchTexts = new Dictionary<string, BranchText>(StringComparer.Ordinal);

        foreach (var (id, label) in document.Classes ?? [])
        {
            if (!classes.Contains(id, StringComparer.Ordinal))
            {
                report.Error(path, $"A label for '{id}', which is not a class.");
            }
            else if (string.IsNullOrWhiteSpace(label?.Name))
            {
                report.Error(path, $"The class '{id}' has no name.");
            }
            else
            {
                classTexts[id] = new LabelText(label.Name, label.Description);
            }
        }

        foreach (var (id, label) in document.Treatments ?? [])
        {
            var branch = branches.FirstOrDefault(candidate => candidate.Id == id);

            if (branch is null)
            {
                report.Error(path, $"A label for '{id}', which is not a branch.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(label?.Name))
            {
                report.Error(path, $"The branch '{id}' has no name.");
                continue;
            }

            var leaves = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var (leaf, text) in label.Leaves ?? [])
            {
                if (!branch.Leaves.Contains(leaf, StringComparer.Ordinal))
                {
                    report.Error(path, $"A label for '{id}.{leaf}', which is not a leaf.");
                }
                else if (string.IsNullOrWhiteSpace(text))
                {
                    report.Error(path, $"The leaf '{id}.{leaf}' has an empty label.");
                }
                else
                {
                    leaves[leaf] = text;
                }
            }

            if (isDefault)
            {
                foreach (var leaf in branch.Leaves.Where(leaf => !leaves.ContainsKey(leaf)))
                {
                    report.Error(path, $"The leaf '{id}.{leaf}' has no label in the default locale.");
                }
            }

            branchTexts[id] = new BranchText(label.Name, leaves);
        }

        if (isDefault)
        {
            foreach (var id in classes.Where(id => !classTexts.ContainsKey(id)))
            {
                report.Error(path, $"The class '{id}' has no label in the default locale.");
            }

            foreach (var branch in branches.Where(branch => !branchTexts.ContainsKey(branch.Id)))
            {
                report.Error(path, $"The branch '{branch.Id}' has no label in the default locale.");
            }
        }

        return new TaxonomyText(classTexts, branchTexts);
    }

    private static ProblemCard? LoadCard(Tree tree, string directory, string slug)
    {
        var path = tree.Relative(directory);
        var locales = tree.LocaleFiles(directory, CardFile, ".md");
        var document = tree.ReadYaml<CardDocument>(Path.Combine(directory, CardFile), $"{path}/{CardFile}");

        var texts = new Dictionary<string, CardText>(StringComparer.Ordinal);

        foreach (var (locale, file) in locales)
        {
            var text = ReadCardText(tree, file, locale == ContentRules.DefaultLocale);

            if (text is not null)
            {
                texts[locale] = text;
            }
        }

        if (document is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(document.Class))
        {
            tree.Report.Error($"{path}/{CardFile}", "'class' is required.");
        }

        if (document.Weight is null)
        {
            tree.Report.Error($"{path}/{CardFile}", "'weight' is required.");
        }
        else if (document.Weight is < ContentRules.MinWeight or > ContentRules.MaxWeight)
        {
            tree.Report.Error(
                $"{path}/{CardFile}",
                $"'weight' must be {ContentRules.MinWeight}, 2 or {ContentRules.MaxWeight}, not {document.Weight}.");
        }

        return new ProblemCard(slug, document.Class ?? string.Empty, document.Weight ?? 0, texts);
    }

    private static CardText? ReadCardText(Tree tree, string file, bool isDefault)
    {
        var path = tree.Relative(file);
        var document = LocaleDocument.Parse(File.ReadAllText(file), path, tree.Report);

        if (document is null)
        {
            return null;
        }

        var front = ContentYaml.Read<CardFrontMatter>(document.FrontMatter, path, tree.Report, LocaleDocument.FrontMatterLine);
        var sections = new Dictionary<CardSection, string>();

        foreach (var (heading, body) in document.Sections)
        {
            if (!CardHeadings.TryGetValue(heading, out var section))
            {
                tree.Report.Error(path, $"Unknown section '## {heading}'. A card's sections are: {string.Join(", ", CardHeadings.Keys)}.");
            }
            else if (body.Length > 0)
            {
                sections[section] = body;
            }
        }

        if (front is null)
        {
            return null;
        }

        if (isDefault)
        {
            Require(tree.Report, path, front.Name, "name");
            Require(tree.Report, path, front.Summary, "summary");

            foreach (var missing in RequiredCardSections.Where(section => !sections.ContainsKey(section)))
            {
                var heading = CardHeadings.First(pair => pair.Value == missing).Key;
                tree.Report.Error(path, $"The section '## {heading}' is required.");
            }
        }

        if (front.Summary is not null && front.Summary.Trim().Contains('\n', StringComparison.Ordinal))
        {
            tree.Report.Error(path, "'summary' must be one line.");
        }

        return new CardText(
            front.Name?.Trim() ?? string.Empty,
            front.Summary?.Trim() ?? string.Empty,
            [.. (front.Keywords ?? []).Where(keyword => !string.IsNullOrWhiteSpace(keyword)).Select(keyword => keyword.Trim())],
            sections);
    }

    private static Material? LoadMaterial(Tree tree, string directory, string slug)
    {
        var path = tree.Relative(directory);

        foreach (var entry in new DirectoryInfo(directory).EnumerateFileSystemInfos())
        {
            if (entry.Name is not (MaterialFile or MaterialFilesDirectory))
            {
                tree.Report.Error($"{path}/{entry.Name}", "Unknown entry: a material holds material.yaml and files/.");
            }
        }

        var document = tree.ReadYaml<MaterialDocument>(Path.Combine(directory, MaterialFile), $"{path}/{MaterialFile}");
        var filesDirectory = Path.Combine(directory, MaterialFilesDirectory);
        var files = new List<MaterialFile>();

        if (!Directory.Exists(filesDirectory))
        {
            tree.Report.Error($"{path}/{MaterialFilesDirectory}", "The files/ directory is missing.");
        }
        else
        {
            ReadMaterialFiles(tree, new DirectoryInfo(filesDirectory), filesDirectory, files);

            if (files.Count == 0)
            {
                tree.Report.Error($"{path}/{MaterialFilesDirectory}", "The material has no files.");
            }
        }

        if (document is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(document.Language))
        {
            tree.Report.Error($"{path}/{MaterialFile}", "'language' is required.");
        }

        files.Sort((left, right) => string.CompareOrdinal(left.Path, right.Path));

        return new Material(slug, document.Language ?? string.Empty, document.Notes, files);
    }

    private static void ReadMaterialFiles(Tree tree, DirectoryInfo directory, string filesRoot, List<MaterialFile> files)
    {
        foreach (var entry in directory.EnumerateFileSystemInfos())
        {
            var path = tree.Relative(entry.FullName);

            if (entry.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                tree.Report.Error(path, "Symbolic links are not allowed in a material.");
                continue;
            }

            if (entry is DirectoryInfo child)
            {
                ReadMaterialFiles(tree, child, filesRoot, files);
                continue;
            }

            string content;

            try
            {
                content = StrictUtf8.GetString(File.ReadAllBytes(entry.FullName));
            }
            catch (DecoderFallbackException)
            {
                tree.Report.Error(path, "The file is not UTF-8 text.");
                continue;
            }

            if (content.Contains('\0', StringComparison.Ordinal))
            {
                tree.Report.Error(path, "The file is binary; a material holds text only.");
                continue;
            }

            var relative = Path.GetRelativePath(filesRoot, entry.FullName).Replace('\\', '/');
            files.Add(new MaterialFile(relative, LocaleDocument.Normalise(content)));
        }
    }

    private static DiagnosisTask? LoadTask(Tree tree, string directory, string slug)
    {
        var path = tree.Relative(directory);
        var yamlPath = $"{path}/{TaskFile}";
        var locales = tree.LocaleFiles(directory, TaskFile, ".md");
        var document = tree.ReadYaml<TaskDocument>(Path.Combine(directory, TaskFile), yamlPath);

        var texts = new Dictionary<string, TaskText>(StringComparer.Ordinal);

        foreach (var (locale, file) in locales)
        {
            var text = ReadTaskText(tree, file, locale == ContentRules.DefaultLocale);

            if (text is not null)
            {
                texts[locale] = text;
            }
        }

        if (document is null)
        {
            return null;
        }

        Require(tree.Report, yamlPath, document.Material, "material");

        var difficulty = document.Difficulty switch
        {
            "easy" => TaskDifficulty.Easy,
            "medium" => TaskDifficulty.Medium,
            "hard" => TaskDifficulty.Hard,
            _ => (TaskDifficulty?)null,
        };

        if (difficulty is null)
        {
            tree.Report.Error(yamlPath, $"'difficulty' must be easy, medium or hard, not '{document.Difficulty}'.");
        }

        if (document.Findings is null)
        {
            tree.Report.Error(yamlPath, "'findings' is required; a clean task says 'findings: []'.");
        }

        var findings = (document.Findings ?? [])
            .Select(finding => new Finding(finding.Card ?? string.Empty, [.. (finding.Leaves ?? []).Select(leaf => leaf ?? string.Empty)]))
            .ToList();

        return new DiagnosisTask(slug, document.Material ?? string.Empty, difficulty ?? TaskDifficulty.Easy, findings, texts);
    }

    private static TaskText? ReadTaskText(Tree tree, string file, bool isDefault)
    {
        var path = tree.Relative(file);
        var document = LocaleDocument.Parse(File.ReadAllText(file), path, tree.Report);

        if (document is null)
        {
            return null;
        }

        var front = ContentYaml.Read<TaskFrontMatter>(document.FrontMatter, path, tree.Report, LocaleDocument.FrontMatterLine);

        foreach (var heading in document.Sections.Keys.Where(heading => !TaskHeadings.Contains(heading)))
        {
            tree.Report.Error(path, $"Unknown section '## {heading}'. A task's sections are: {string.Join(", ", TaskHeadings)}.");
        }

        var notes = new Dictionary<string, string>(StringComparer.Ordinal);

        if (document.Sections.TryGetValue(NotesHeading, out var notesBody))
        {
            var subsections = LocaleDocument.SplitSections(notesBody.Split('\n'), "### ", path, tree.Report) ?? [];

            foreach (var (card, note) in subsections.Where(pair => pair.Value.Length > 0))
            {
                notes[card] = note;
            }
        }

        if (front is null)
        {
            return null;
        }

        var context = document.Sections.GetValueOrDefault(ContextHeading, string.Empty);
        var brief = document.Sections.GetValueOrDefault(BriefHeading, string.Empty);
        var lesson = document.Sections.GetValueOrDefault(LessonHeading);

        if (isDefault)
        {
            Require(tree.Report, path, front.Title, "title");

            if (context.Length == 0)
            {
                tree.Report.Error(path, $"The section '## {ContextHeading}' is required.");
            }

            if (brief.Length == 0)
            {
                tree.Report.Error(path, $"The section '## {BriefHeading}' is required.");
            }
        }

        return new TaskText(front.Title?.Trim() ?? string.Empty, context, brief, notes, string.IsNullOrEmpty(lesson) ? null : lesson);
    }

    private static void Require(ContentReport report, string path, string? value, string key)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            report.Error(path, $"'{key}' is required.");
        }
    }

    /// <summary>The content root, the report, and the file-system walking every kind of item shares.</summary>
    private sealed class Tree(string root, ContentReport report)
    {
        public string Root { get; } = root;

        public ContentReport Report { get; } = report;

        public string Relative(string fullPath) => Path.GetRelativePath(Root, fullPath).Replace('\\', '/');

        /// <summary>Every item directory under <paramref name="kind"/>, loaded by <paramref name="load"/>.</summary>
        public List<T> Items<T>(string kind, Func<Tree, string, string, T?> load)
            where T : class
        {
            var directory = Path.Combine(Root, kind);
            var items = new List<T>();

            if (!Directory.Exists(directory))
            {
                return items;
            }

            foreach (var file in Directory.EnumerateFiles(directory).Order(StringComparer.Ordinal))
            {
                Report.Error(Relative(file), $"Unknown file: {kind}/ holds one directory per item.");
            }

            foreach (var item in Directory.EnumerateDirectories(directory).Order(StringComparer.Ordinal))
            {
                var slug = Path.GetFileName(item);

                if (!ContentRules.IsSlug(slug))
                {
                    Report.Error(
                        Relative(item),
                        $"'{slug}' is not a valid slug: lower-case letters, digits and hyphens, starting with a letter, at most {ContentRules.SlugMaxLength} characters.");
                    continue;
                }

                if (load(this, item, slug) is { } loaded)
                {
                    items.Add(loaded);
                }
            }

            return items;
        }

        /// <summary>
        /// The locale files of an item directory, which holds <paramref name="dataFile"/> and one
        /// <c>&lt;locale&gt;<paramref name="extension"/></c> per locale. Anything else is reported, and
        /// so is a missing default locale.
        /// </summary>
        public List<(string Locale, string File)> LocaleFiles(string directory, string dataFile, string extension)
        {
            var locales = new List<(string, string)>();

            foreach (var entry in new DirectoryInfo(directory).EnumerateFileSystemInfos().OrderBy(entry => entry.Name, StringComparer.Ordinal))
            {
                if (entry.Name == dataFile)
                {
                    continue;
                }

                var locale = Path.GetFileNameWithoutExtension(entry.Name);

                if (entry is FileInfo && entry.Name.EndsWith(extension, StringComparison.Ordinal) && ContentRules.IsLocale(locale))
                {
                    locales.Add((locale, entry.FullName));
                }
                else
                {
                    Report.Error(Relative(entry.FullName), $"Unknown entry: this directory holds {dataFile} and one <locale>{extension} per locale.");
                }
            }

            if (!locales.Any(pair => pair.Item1 == ContentRules.DefaultLocale))
            {
                Report.Error($"{Relative(directory)}/{ContentRules.DefaultLocale}{extension}", "The text in the default locale is missing.");
            }

            return locales;
        }

        public T? ReadYaml<T>(string file, string path)
            where T : class
        {
            if (!File.Exists(file))
            {
                Report.Error(path, "The file is missing.");
                return null;
            }

            return ContentYaml.Read<T>(File.ReadAllText(file), path, Report);
        }
    }
}
