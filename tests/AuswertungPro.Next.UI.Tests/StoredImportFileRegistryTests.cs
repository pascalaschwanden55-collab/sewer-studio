using System.IO;
using System.Text.Json;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Tests;

public sealed class StoredImportFileRegistryTests
{
    [Fact]
    public void Store_copies_files_into_import_subfolder_and_updates_metadata()
    {
        using var temp = new TempDir();
        var projectPath = temp.CreateFile("Projekt.aproj", "project");
        var source = temp.CreateFile("source/a.pdf", "pdf");
        var metadata = new Dictionary<string, string>();

        var result = StoredImportFileRegistry.Store(projectPath, metadata, "PDF", new[] { source });

        Assert.False(result.MissingProjectPath);
        var copied = Path.Combine(temp.Path, "Imports", "PDF", "a.pdf");
        Assert.True(File.Exists(copied));
        Assert.Equal("pdf", File.ReadAllText(copied));
        Assert.Equal(new[] { Path.Combine("Imports", "PDF", "a.pdf") }, result.StoredRelativePaths);
        Assert.Equal(result.StoredRelativePaths, ReadMetadata(metadata, "PDF_StoredFiles"));
    }

    [Fact]
    public void Store_reuses_existing_same_content_file_without_duplicate_metadata()
    {
        using var temp = new TempDir();
        var projectPath = temp.CreateFile("Projekt.aproj", "project");
        temp.CreateFile("Imports/PDF/a.pdf", "abc");
        var source = temp.CreateFile("source/a.pdf", "abc");
        var existing = Path.Combine("Imports", "PDF", "a.pdf");
        var metadata = new Dictionary<string, string>
        {
            ["PDF_StoredFiles"] = JsonSerializer.Serialize(new List<string> { existing })
        };

        var result = StoredImportFileRegistry.Store(projectPath, metadata, "PDF", new[] { source });

        Assert.Equal(new[] { existing }, result.StoredRelativePaths);
        Assert.Equal(new[] { existing }, ReadMetadata(metadata, "PDF_StoredFiles"));
        Assert.Equal("abc", File.ReadAllText(Path.Combine(temp.Path, existing)));
    }

    [Fact]
    public void Store_adds_timestamp_suffix_when_existing_file_has_same_size_but_different_content()
    {
        using var temp = new TempDir();
        var projectPath = temp.CreateFile("Projekt.aproj", "project");
        temp.CreateFile("Imports/PDF/a.pdf", "abc");
        var source = temp.CreateFile("source/a.pdf", "xyz");
        var metadata = new Dictionary<string, string>();

        var result = StoredImportFileRegistry.Store(
            projectPath,
            metadata,
            "PDF",
            new[] { source },
            now: () => new DateTime(2026, 6, 30, 12, 34, 56));

        var expected = Path.Combine("Imports", "PDF", "a_20260630_123456.pdf");
        Assert.Equal(new[] { expected }, result.StoredRelativePaths);
        Assert.Equal("abc", File.ReadAllText(Path.Combine(temp.Path, "Imports", "PDF", "a.pdf")));
        Assert.Equal("xyz", File.ReadAllText(Path.Combine(temp.Path, expected)));
    }

    [Fact]
    public void Store_adds_timestamp_suffix_when_existing_file_has_different_size()
    {
        using var temp = new TempDir();
        var projectPath = temp.CreateFile("Projekt.aproj", "project");
        temp.CreateFile("Imports/XTF/data.xtf", "old");
        var source = temp.CreateFile("source/data.xtf", "new-content");
        var metadata = new Dictionary<string, string>();

        var result = StoredImportFileRegistry.Store(
            projectPath,
            metadata,
            "XTF",
            new[] { source },
            now: () => new DateTime(2026, 6, 30, 12, 34, 56));

        var expected = Path.Combine("Imports", "XTF", "data_20260630_123456.xtf");
        Assert.Equal(new[] { expected }, result.StoredRelativePaths);
        Assert.True(File.Exists(Path.Combine(temp.Path, expected)));
        Assert.Equal("new-content", File.ReadAllText(Path.Combine(temp.Path, expected)));
    }

    [Fact]
    public void Store_merges_semicolon_metadata_fallback_with_new_paths()
    {
        using var temp = new TempDir();
        var projectPath = temp.CreateFile("Projekt.aproj", "project");
        var source = temp.CreateFile("source/new.txt", "txt");
        var existing = Path.Combine("Imports", "TXT", "old.txt");
        var metadata = new Dictionary<string, string>
        {
            ["TXT_StoredFiles"] = existing + "; ;"
        };

        StoredImportFileRegistry.Store(projectPath, metadata, "TXT", new[] { source });

        Assert.Equal(
            new[] { existing, Path.Combine("Imports", "TXT", "new.txt") },
            ReadMetadata(metadata, "TXT_StoredFiles"));
    }

    [Fact]
    public void Store_reports_missing_project_path_without_copying()
    {
        using var temp = new TempDir();
        var source = temp.CreateFile("source/a.pdf", "pdf");
        var metadata = new Dictionary<string, string>();

        var result = StoredImportFileRegistry.Store(null, metadata, "PDF", new[] { source });

        Assert.True(result.MissingProjectPath);
        Assert.Empty(result.StoredRelativePaths);
        Assert.Empty(metadata);
    }

    private static List<string> ReadMetadata(Dictionary<string, string> metadata, string key)
        => JsonSerializer.Deserialize<List<string>>(metadata[key])!;

    private sealed class TempDir : IDisposable
    {
        public string Path { get; }

        public TempDir()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ssd_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string CreateFile(string relativePath, string content)
        {
            var full = System.IO.Path.Combine(Path, relativePath.Replace('/', System.IO.Path.DirectorySeparatorChar));
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full)!);
            File.WriteAllText(full, content);
            return full;
        }

        public void Dispose()
        {
            try { Directory.Delete(Path, recursive: true); }
            catch { /* best effort */ }
        }
    }
}
