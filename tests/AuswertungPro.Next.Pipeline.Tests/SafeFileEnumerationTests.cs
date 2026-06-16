using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class SafeFileEnumerationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "sfe_app_" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void EnumerateFilesSafe_TraversesRootThenSortedSubdirectories()
    {
        Directory.CreateDirectory(Path.Combine(_root, "b"));
        Directory.CreateDirectory(Path.Combine(_root, "a"));
        File.WriteAllText(Path.Combine(_root, "z.txt"), "z");
        File.WriteAllText(Path.Combine(_root, "a", "c.txt"), "c");
        File.WriteAllText(Path.Combine(_root, "b", "b.txt"), "b");

        var files = SafeFileEnumeration
            .EnumerateFilesSafe(_root, "*.txt", recursive: true)
            .Select(path => Path.GetRelativePath(_root, path).Replace('\\', '/'))
            .ToList();

        Assert.Equal(new[] { "z.txt", "a/c.txt", "b/b.txt" }, files);
    }

    [Fact]
    public void EnumerateFilesSafe_MissingRoot_ReturnsEmptyList()
    {
        var missing = Path.Combine(_root, "missing");

        var files = SafeFileEnumeration.EnumerateFilesSafe(missing, "*", recursive: true).ToList();

        Assert.Empty(files);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
        catch
        {
            // best-effort cleanup
        }
    }
}
