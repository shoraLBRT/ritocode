using Ritocode.Modules.Problems.ContentFormat;

namespace Ritocode.Modules.Problems.Tests.ContentFormat;

/// <summary>
/// A throwaway copy of the reference content, so a test states the one thing it breaks and the diff
/// between passing content and failing content is the test. Deleted when the test finishes.
/// </summary>
internal sealed class TempContent : IDisposable
{
    public static string ReferenceRoot => Path.Combine(AppContext.BaseDirectory, "ContentFixtures", "reference");

    public static string CommittedRoot => Path.Combine(AppContext.BaseDirectory, "committed-content");

    private TempContent(string root) => Root = root;

    public string Root { get; }

    public static TempContent FromReference()
    {
        var root = Path.Combine(Path.GetTempPath(), "ritocode-content-" + Guid.NewGuid().ToString("N"));
        Copy(ReferenceRoot, root);
        return new TempContent(root);
    }

    public TempContent Write(string relativePath, string text)
    {
        var path = Full(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
        return this;
    }

    public TempContent WriteBytes(string relativePath, byte[] bytes)
    {
        var path = Full(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes);
        return this;
    }

    public TempContent Replace(string relativePath, string oldValue, string newValue)
    {
        var text = File.ReadAllText(Full(relativePath));
        Assert.Contains(oldValue, text, StringComparison.Ordinal);
        return Write(relativePath, text.Replace(oldValue, newValue, StringComparison.Ordinal));
    }

    public TempContent Delete(string relativePath)
    {
        var path = Full(relativePath);

        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
        else
        {
            File.Delete(path);
        }

        return this;
    }

    public TempContent Move(string from, string to)
    {
        Directory.Move(Full(from), Full(to));
        return this;
    }

    public (ContentSet Content, ContentReport Report) Load() => ContentLoader.Load(Root);

    public void Dispose()
    {
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }
    }

    private string Full(string relativePath) => Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));

    private static void Copy(string source, string destination)
    {
        Directory.CreateDirectory(destination);

        foreach (var file in Directory.EnumerateFiles(source))
        {
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
        }

        foreach (var directory in Directory.EnumerateDirectories(source))
        {
            Copy(directory, Path.Combine(destination, Path.GetFileName(directory)));
        }
    }
}
