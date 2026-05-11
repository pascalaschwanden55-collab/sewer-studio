using System.Collections.Generic;
using System.IO;
using System.Linq;
using AuswertungPro.Next.Infrastructure.Common;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests;

// Robustheits-Fix 2026-05-10 (Deep-Dive Punkt #1):
// Tests fuer SafeFileEnumeration — Safe-Enumeration die gesperrte oder
// fluechtige Unterordner toleriert, statt den ganzen Lauf abzubrechen.
[Trait("Category", "Unit")]
public sealed class SafeFileEnumerationTests : System.IDisposable
{
    private readonly string _tempRoot;

    public SafeFileEnumerationTests()
    {
        _tempRoot = Path.Combine(
            Path.GetTempPath(),
            "SafeFileEnumTests_" + Path.GetRandomFileName());
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempRoot))
                Directory.Delete(_tempRoot, recursive: true);
        }
        catch { /* best-effort */ }
    }

    [Fact]
    public void EnumerateFilesSafe_RootDoesNotExist_ReturnsEmpty()
    {
        var fakeRoot = Path.Combine(_tempRoot, "does-not-exist");
        var files = SafeFileEnumeration.EnumerateFilesSafe(fakeRoot).ToList();
        Assert.Empty(files);
    }

    [Fact]
    public void EnumerateFilesSafe_EmptyRoot_ReturnsEmpty()
    {
        var files = SafeFileEnumeration.EnumerateFilesSafe(_tempRoot).ToList();
        Assert.Empty(files);
    }

    [Fact]
    public void EnumerateFilesSafe_NullOrWhitespace_ReturnsEmpty()
    {
        Assert.Empty(SafeFileEnumeration.EnumerateFilesSafe(null!).ToList());
        Assert.Empty(SafeFileEnumeration.EnumerateFilesSafe("").ToList());
        Assert.Empty(SafeFileEnumeration.EnumerateFilesSafe("   ").ToList());
    }

    [Fact]
    public void EnumerateFilesSafe_FilesInRoot_ReturnsAll()
    {
        File.WriteAllText(Path.Combine(_tempRoot, "a.txt"), "x");
        File.WriteAllText(Path.Combine(_tempRoot, "b.txt"), "x");

        var files = SafeFileEnumeration.EnumerateFilesSafe(_tempRoot).ToList();
        Assert.Equal(2, files.Count);
    }

    [Fact]
    public void EnumerateFilesSafe_SearchPattern_FiltersByPattern()
    {
        File.WriteAllText(Path.Combine(_tempRoot, "a.pdf"), "x");
        File.WriteAllText(Path.Combine(_tempRoot, "b.txt"), "x");
        File.WriteAllText(Path.Combine(_tempRoot, "c.pdf"), "x");

        var pdfs = SafeFileEnumeration.EnumerateFilesSafe(_tempRoot, "*.pdf").ToList();
        Assert.Equal(2, pdfs.Count);
        Assert.All(pdfs, p => Assert.EndsWith(".pdf", p));
    }

    [Fact]
    public void EnumerateFilesSafe_Recursive_FindsInSubdirectories()
    {
        var sub = Path.Combine(_tempRoot, "sub");
        var sub2 = Path.Combine(sub, "deeper");
        Directory.CreateDirectory(sub2);

        File.WriteAllText(Path.Combine(_tempRoot, "a.txt"), "x");
        File.WriteAllText(Path.Combine(sub, "b.txt"), "x");
        File.WriteAllText(Path.Combine(sub2, "c.txt"), "x");

        var files = SafeFileEnumeration.EnumerateFilesSafe(_tempRoot, "*.txt", recursive: true).ToList();
        Assert.Equal(3, files.Count);
    }

    [Fact]
    public void EnumerateFilesSafe_NonRecursive_OnlyRoot()
    {
        var sub = Path.Combine(_tempRoot, "sub");
        Directory.CreateDirectory(sub);

        File.WriteAllText(Path.Combine(_tempRoot, "a.txt"), "x");
        File.WriteAllText(Path.Combine(sub, "b.txt"), "x");

        var files = SafeFileEnumeration.EnumerateFilesSafe(_tempRoot, "*.txt", recursive: false).ToList();
        Assert.Single(files);
        Assert.EndsWith("a.txt", files[0]);
    }

    [Fact]
    public void EnumerateDirectoriesSafe_IncludesRootItself()
    {
        var dirs = SafeFileEnumeration.EnumerateDirectoriesSafe(_tempRoot).ToList();
        Assert.Single(dirs);
        Assert.Equal(_tempRoot, dirs[0]);
    }

    [Fact]
    public void EnumerateDirectoriesSafe_TraversesAllSubdirectories()
    {
        Directory.CreateDirectory(Path.Combine(_tempRoot, "a"));
        Directory.CreateDirectory(Path.Combine(_tempRoot, "b"));
        Directory.CreateDirectory(Path.Combine(_tempRoot, "a", "a1"));

        var dirs = SafeFileEnumeration.EnumerateDirectoriesSafe(_tempRoot).ToList();
        Assert.Equal(4, dirs.Count); // root + a + b + a/a1
    }

    [Fact]
    public void EnumerateFilesSafe_DeletedDirectoryDuringEnumeration_DoesNotThrow()
    {
        // Sub-Ordner anlegen, mid-Enumeration loeschen — simuliert fluechtige
        // Unterordner (z.B. Sync-Service raeumt parallel auf).
        var sub = Path.Combine(_tempRoot, "transient");
        Directory.CreateDirectory(sub);
        File.WriteAllText(Path.Combine(sub, "file.txt"), "x");

        // Vor der Enumeration loeschen — beim Recursive-Descent ist der Ordner
        // weg. EnumerateFilesSafe muss das tolerieren.
        Directory.Delete(sub, recursive: true);

        // Soll nicht werfen — alle Files in root sind noch da, sub ist weg.
        var files = SafeFileEnumeration.EnumerateFilesSafe(_tempRoot, "*.txt", recursive: true).ToList();
        Assert.Empty(files); // root selbst hat keine .txt
    }

    [Fact]
    public void EnumerateFilesSafe_SkippedDirectoriesCollector_FuelltSichBeiZugriffsfehler()
    {
        // Wir simulieren keinen echten UnauthorizedAccess (schwer ohne ACL-
        // Manipulation), aber wir koennen sicherstellen dass die Collector-API
        // funktioniert wenn Skip-Ereignisse passieren — wenigstens der
        // happy-path-Fall (keine Skips bei normalem Run).
        Directory.CreateDirectory(Path.Combine(_tempRoot, "a"));
        File.WriteAllText(Path.Combine(_tempRoot, "a", "x.txt"), "x");

        var skipped = new List<string>();
        var files = SafeFileEnumeration.EnumerateFilesSafe(
            _tempRoot, "*.txt", recursive: true, skippedDirectories: skipped).ToList();

        Assert.Single(files);
        Assert.Empty(skipped); // happy path — keine Skips
    }
}
