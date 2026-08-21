using AuswertungPro.Next.Infrastructure.Import;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Infrastructure.Tests.Backup;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class ImportSourceArchiveServiceTests : IDisposable
{
    private readonly string _sourceFolder = Path.Combine(
        Path.GetTempPath(),
        $"import-archive-source-{Guid.NewGuid():N}");
    private readonly string _projectFolder = Path.Combine(
        Path.GetTempPath(),
        $"import-archive-project-{Guid.NewGuid():N}");

    [Fact]
    public void InstanceService_CopiesSourceFileThroughContract()
    {
        Directory.CreateDirectory(_sourceFolder);
        Directory.CreateDirectory(_projectFolder);
        var sourcePath = Path.Combine(_sourceFolder, "quelle.pdf");
        File.WriteAllText(sourcePath, "PDF-Inhalt");
        IImportSourceArchiver service = new ImportSourceArchiveService();

        var result = service.Archive(_sourceFolder, _projectFolder);

        var targetPath = Path.Combine(
            ProjectStructure.ImportdateienDir(_projectFolder, ProjectStructure.PdfDir),
            "quelle.pdf");
        Assert.Equal(1, result.Copied);
        Assert.Equal(0, result.Reused);
        Assert.Equal("PDF-Inhalt", File.ReadAllText(targetPath));
    }

    [Fact]
    public void InstanceService_bereitet_Archivdatei_mit_Staging_nur_vor()
    {
        Directory.CreateDirectory(_sourceFolder);
        Directory.CreateDirectory(_projectFolder);
        var sourcePath = Path.Combine(_sourceFolder, "quelle.pdf");
        File.WriteAllText(sourcePath, "PDF-Inhalt");
        var projectPath = Path.Combine(_projectFolder, "Projektdateien", "projekt.json");
        Directory.CreateDirectory(Path.GetDirectoryName(projectPath)!);
        File.WriteAllText(projectPath, "{}");
        using var staging = new ImportFileStagingService().Begin(projectPath)!;
        IImportSourceArchiver service = new ImportSourceArchiveService();

        var result = service.Archive(_sourceFolder, _projectFolder, staging);

        var targetDirectory = ProjectStructure.ImportdateienDir(
            _projectFolder,
            ProjectStructure.PdfDir);
        var targetPath = Path.Combine(targetDirectory, "quelle.pdf");
        Assert.Equal(1, result.Copied);
        Assert.False(File.Exists(targetPath));
        var readable = Assert.Single(staging.EnumerateReadableFiles(
            targetDirectory,
            "*.pdf",
            SearchOption.TopDirectoryOnly));
        Assert.Equal("PDF-Inhalt", File.ReadAllText(readable.ReadPath));
    }

    [Fact]
    public void InstanceService_GleicheGroesseMitAnderemInhalt_WirdNichtAlsWiederverwendetGemeldet()
    {
        Directory.CreateDirectory(_sourceFolder);
        Directory.CreateDirectory(_projectFolder);
        var sourcePath = Path.Combine(_sourceFolder, "quelle.pdf");
        File.WriteAllText(sourcePath, "NEU!");
        var targetDirectory = ProjectStructure.ImportdateienDir(
            _projectFolder,
            ProjectStructure.PdfDir);
        Directory.CreateDirectory(targetDirectory);
        var existing = Path.Combine(targetDirectory, "quelle.pdf");
        File.WriteAllText(existing, "ALT!");

        var result = new ImportSourceArchiveService().Archive(
            _sourceFolder,
            _projectFolder);

        Assert.Equal(1, result.Copied);
        Assert.Equal(0, result.Reused);
        Assert.Equal("ALT!", File.ReadAllText(existing));
        Assert.Contains(
            Directory.EnumerateFiles(targetDirectory, "*.pdf"),
            path => !string.Equals(path, existing, StringComparison.OrdinalIgnoreCase)
                    && File.ReadAllText(path) == "NEU!");
    }

    [JunctionFact]
    public void DirektesArchiv_VerknuepfterZielordner_SchreibtKeineDateiNachAussen()
    {
        Directory.CreateDirectory(_sourceFolder);
        Directory.CreateDirectory(_projectFolder);
        var sourcePath = Path.Combine(_sourceFolder, "quelle.pdf");
        File.WriteAllText(sourcePath, "PDF-Inhalt");
        var archiveRoot = Path.Combine(_projectFolder, ProjectStructure.Importdateien);
        var targetLink = Path.Combine(archiveRoot, ProjectStructure.PdfDir);
        var external = Path.Combine(_projectFolder, "Fremdziel");
        Directory.CreateDirectory(archiveRoot);
        Directory.CreateDirectory(external);
        JunctionTestSupport.CreateDirectoryLink(targetLink, external);

        try
        {
            var result = new ImportSourceArchiveService().Archive(
                _sourceFolder,
                _projectFolder);

            Assert.Equal(0, result.Copied);
            Assert.Contains(result.Messages, message =>
                message.Contains("Verknuepfung", StringComparison.OrdinalIgnoreCase));
            Assert.Empty(Directory.EnumerateFileSystemEntries(external));
            Assert.Equal("PDF-Inhalt", File.ReadAllText(sourcePath));
        }
        finally
        {
            try { Directory.Delete(targetLink); } catch { }
        }
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_sourceFolder)) Directory.Delete(_sourceFolder, recursive: true); } catch { }
        try { if (Directory.Exists(_projectFolder)) Directory.Delete(_projectFolder, recursive: true); } catch { }
    }
}
