using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Infrastructure.Import;
using System.Text.Json;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

public sealed class StoredImportFilePathResolverTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(
        Path.GetTempPath(),
        $"StoredImportFilePathResolverTests_{Guid.NewGuid():N}");

    [Theory]
    [InlineData("[\"Imports/XTF/erste.xtf\",\"Imports/XTF/zweite.xtf\"]")]
    [InlineData("Imports/XTF/erste.xtf;Imports/XTF/zweite.xtf")]
    public void ResolveExistingFiles_liest_Json_und_altes_Semikolonformat(string storedValue)
    {
        var projectFile = CreateProjectFile(inProjectFilesDirectory: false);
        var expectedFirst = CreateFile("Imports", "XTF", "erste.xtf");
        var expectedSecond = CreateFile("Imports", "XTF", "zweite.xtf");
        var metadata = new Dictionary<string, string>
        {
            ["XTF_StoredFiles"] = storedValue
        };
        IStoredImportFilePathResolver resolver = new StoredImportFilePathResolver();

        var result = resolver.ResolveExistingFiles(metadata, "XTF_StoredFiles", projectFile);

        Assert.Equal([expectedFirst, expectedSecond], result);
    }

    [Fact]
    public void ResolveExistingFiles_nutzt_bei_neuer_Struktur_den_echten_Projektordner()
    {
        var projectFile = CreateProjectFile(inProjectFilesDirectory: true);
        var expected = CreateFile("Imports", "PDF", "modern.pdf");
        var metadata = new Dictionary<string, string>
        {
            ["PDF_StoredFiles"] = "[\"Imports/PDF/modern.pdf\"]"
        };
        IStoredImportFilePathResolver resolver = new StoredImportFilePathResolver();

        var result = resolver.ResolveExistingFiles(metadata, "PDF_StoredFiles", projectFile);

        Assert.Equal([expected], result);
    }

    [Fact]
    public void ResolveExistingFiles_findet_alte_Ablage_neben_der_Projektdatei()
    {
        var projectFile = CreateProjectFile(inProjectFilesDirectory: true);
        var expected = CreateFile("Projektdateien", "Imports", "PDF", "legacy.pdf");
        var metadata = new Dictionary<string, string>
        {
            ["PDF_StoredFiles"] = "Imports/PDF/legacy.pdf"
        };
        IStoredImportFilePathResolver resolver = new StoredImportFilePathResolver();

        var result = resolver.ResolveExistingFiles(metadata, "PDF_StoredFiles", projectFile);

        Assert.Equal([expected], result);
    }

    [Fact]
    public void ResolveExistingFiles_unterstuetzt_bestehende_absolute_Pfade_ohne_Projektdatei()
    {
        var expected = CreateFile("extern", "quelle.pdf");
        var metadata = new Dictionary<string, string>
        {
            ["PDF_StoredFiles"] = expected
        };
        IStoredImportFilePathResolver resolver = new StoredImportFilePathResolver();

        var result = resolver.ResolveExistingFiles(metadata, "PDF_StoredFiles", projectFilePath: null);

        Assert.Equal([expected], result);
    }

    [Fact]
    public void ResolveExistingFiles_verwirft_relative_Pfade_ohne_Projektdatei()
    {
        var metadata = new Dictionary<string, string>
        {
            ["PDF_StoredFiles"] = "Imports/PDF/ohne-projekt.pdf"
        };
        IStoredImportFilePathResolver resolver = new StoredImportFilePathResolver();

        var result = resolver.ResolveExistingFiles(metadata, "PDF_StoredFiles", projectFilePath: null);

        Assert.Empty(result);
    }

    [Fact]
    public void ResolveExistingFiles_ueberspringt_fehlende_und_unsichere_Pfade_einzeln()
    {
        var projectRoot = Path.Combine(_tempRoot, "Projekt");
        var projectFilesDirectory = Path.Combine(projectRoot, ProjectFileLocator.ProjektdateienDir);
        Directory.CreateDirectory(projectFilesDirectory);
        var projectFile = Path.Combine(projectFilesDirectory, ProjectFileLocator.ProjectFileName);
        File.WriteAllText(projectFile, "{}");
        var outside = CreateFile("ausserhalb.xtf");
        var expected = CreateFile("Projekt", "Imports", "XTF", "vorhanden.xtf");
        var metadata = new Dictionary<string, string>
        {
            ["XTF_StoredFiles"] = "[\"Imports/XTF/fehlt.xtf\",\"../ausserhalb.xtf\",\"Imports/XTF/vorhanden.xtf\"]"
        };
        IStoredImportFilePathResolver resolver = new StoredImportFilePathResolver();

        var result = resolver.ResolveExistingFiles(metadata, "XTF_StoredFiles", projectFile);

        Assert.True(File.Exists(outside));
        Assert.Equal([expected], result);
    }

    [Fact]
    public void ResolveExistingFiles_gibt_dieselbe_Datei_nur_einmal_zurueck()
    {
        var projectFile = CreateProjectFile(inProjectFilesDirectory: true);
        var expected = CreateFile("Imports", "PDF", "einmal.pdf");
        var metadata = new Dictionary<string, string>
        {
            ["PDF_StoredFiles"] = JsonSerializer.Serialize(new[]
            {
                "Imports/PDF/einmal.pdf",
                expected
            })
        };
        IStoredImportFilePathResolver resolver = new StoredImportFilePathResolver();

        var result = resolver.ResolveExistingFiles(metadata, "PDF_StoredFiles", projectFile);

        Assert.Equal([expected], result);
    }

    [Fact]
    public void ResolveExistingFiles_bevorzugt_die_modern_gespeicherte_Datei_im_Projektordner()
    {
        var projectFile = CreateProjectFile(inProjectFilesDirectory: true);
        var expected = CreateFile("Imports", "PDF", "gleich.pdf");
        _ = CreateFile("Projektdateien", "Imports", "PDF", "gleich.pdf");
        var metadata = new Dictionary<string, string>
        {
            ["PDF_StoredFiles"] = "Imports/PDF/gleich.pdf"
        };
        IStoredImportFilePathResolver resolver = new StoredImportFilePathResolver();

        var result = resolver.ResolveExistingFiles(metadata, "PDF_StoredFiles", projectFile);

        Assert.Equal([expected], result);
    }

    private string CreateProjectFile(bool inProjectFilesDirectory)
    {
        var directory = inProjectFilesDirectory
            ? Path.Combine(_tempRoot, ProjectFileLocator.ProjektdateienDir)
            : _tempRoot;
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, ProjectFileLocator.ProjectFileName);
        File.WriteAllText(path, "{}");
        return path;
    }

    private string CreateFile(params string[] parts)
    {
        var path = parts.Aggregate(_tempRoot, Path.Combine);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "test");
        return path;
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
    }
}
