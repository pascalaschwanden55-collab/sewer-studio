using System.Text.Json;
using AuswertungPro.Next.Application.DataPage;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Media;

namespace AuswertungPro.Next.Infrastructure.Tests.Media;

public sealed class InspectionProtocolFileLocatorTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(
        Path.GetTempPath(),
        $"InspectionProtocolFileLocatorTests_{Guid.NewGuid():N}");

    [Fact]
    public void Dienst_bevorzugt_Inspektionsprotokoll_vor_Lageplan()
    {
        var holdingDirectory = Directory.CreateDirectory(Path.Combine(_tempRoot, "06-001")).FullName;
        var protocol = Path.Combine(holdingDirectory, "A_Protokoll_06-001.pdf");
        var plan = Path.Combine(holdingDirectory, "Z_Plan_06-001.pdf");
        File.WriteAllText(protocol, "Haltungsinspektion 06-001 Leitungsbericht");
        File.WriteAllText(plan, "Leitungsende 06-001 Dachwasser angeschlossen");
        IInspectionProtocolFileLocator locator = new InspectionProtocolFileLocator();

        var found = locator.FindProtocolPath(
            CreateRecord(),
            resolvedLink: null,
            initialFolder: _tempRoot,
            projectPath: null,
            storedFilesRaw: null);

        Assert.Equal(protocol, found, ignoreCase: true);
    }

    [Fact]
    public void Gespeicherte_PDF_wird_aus_dem_modernen_Projektordner_gelesen()
    {
        var projectFilesDirectory = Directory.CreateDirectory(
            Path.Combine(_tempRoot, "Projektdateien")).FullName;
        var projectPath = Path.Combine(projectFilesDirectory, "projekt.json");
        File.WriteAllText(projectPath, "{}");
        var pdfDirectory = Directory.CreateDirectory(Path.Combine(_tempRoot, "Imports", "PDF")).FullName;
        var expected = Path.Combine(pdfDirectory, "Protokoll_06-001.pdf");
        File.WriteAllText(expected, "PDF");
        var legacyDirectory = Directory.CreateDirectory(
            Path.Combine(projectFilesDirectory, "Imports", "PDF")).FullName;
        File.WriteAllText(Path.Combine(legacyDirectory, Path.GetFileName(expected)), "alte PDF");
        var storedFilesRaw = JsonSerializer.Serialize(new[]
        {
            Path.Combine("Imports", "PDF", Path.GetFileName(expected))
        });
        IInspectionProtocolFileLocator locator = new InspectionProtocolFileLocator();

        var found = locator.FindProtocolPath(
            CreateRecord(),
            resolvedLink: null,
            initialFolder: null,
            projectPath,
            storedFilesRaw);

        Assert.Equal(expected, found, ignoreCase: true);
    }

    [Fact]
    public void Oeffentlicher_parameterloser_Konstruktor_bleibt_erhalten()
    {
        var constructor = typeof(InspectionProtocolFileLocator).GetConstructor(Type.EmptyTypes);

        Assert.NotNull(constructor);
        Assert.True(constructor!.IsPublic);
    }

    [Fact]
    public void Gespeicherte_PDF_wird_aus_der_alten_Projektdateien_Ablage_gelesen()
    {
        var projectFilesDirectory = Directory.CreateDirectory(
            Path.Combine(_tempRoot, "Projektdateien")).FullName;
        var projectPath = Path.Combine(projectFilesDirectory, "projekt.json");
        File.WriteAllText(projectPath, "{}");
        var pdfDirectory = Directory.CreateDirectory(
            Path.Combine(projectFilesDirectory, "Imports", "PDF")).FullName;
        var expected = Path.Combine(pdfDirectory, "Protokoll_06-001.pdf");
        File.WriteAllText(expected, "PDF");
        var storedFilesRaw = JsonSerializer.Serialize(new[]
        {
            Path.Combine("Imports", "PDF", Path.GetFileName(expected))
        });
        IInspectionProtocolFileLocator locator = new InspectionProtocolFileLocator();

        var found = locator.FindProtocolPath(
            CreateRecord(),
            resolvedLink: null,
            initialFolder: null,
            projectPath,
            storedFilesRaw);

        Assert.Equal(expected, found, ignoreCase: true);
    }

    [Fact]
    public void Gespeicherter_relativpfad_darf_den_Projektordner_nicht_verlassen()
    {
        var projectDirectory = Directory.CreateDirectory(Path.Combine(_tempRoot, "Projekt")).FullName;
        var projectPath = Path.Combine(projectDirectory, "projekt.json");
        File.WriteAllText(projectPath, "{}");
        var outside = Path.Combine(_tempRoot, "Protokoll_06-001.pdf");
        File.WriteAllText(outside, "PDF");
        var storedFilesRaw = JsonSerializer.Serialize(new[]
        {
            Path.Combine("..", Path.GetFileName(outside))
        });
        IInspectionProtocolFileLocator locator = new InspectionProtocolFileLocator();

        var found = locator.FindProtocolPath(
            CreateRecord(),
            resolvedLink: null,
            initialFolder: null,
            projectPath,
            storedFilesRaw);

        Assert.Null(found);
    }

    [Fact]
    public void Gespeicherter_absoluter_Pfad_bleibt_ohne_Projektdatei_lesbar()
    {
        Directory.CreateDirectory(_tempRoot);
        var expected = Path.Combine(_tempRoot, "Protokoll_06-001.pdf");
        File.WriteAllText(expected, "PDF");
        var storedFilesRaw = JsonSerializer.Serialize(new[] { expected });
        IInspectionProtocolFileLocator locator = new InspectionProtocolFileLocator();

        var found = locator.FindProtocolPath(
            CreateRecord(),
            resolvedLink: null,
            initialFolder: null,
            projectPath: null,
            storedFilesRaw);

        Assert.Equal(expected, found, ignoreCase: true);
    }

    [Fact]
    public void Gespeicherte_Nicht_PDF_wird_als_Protokoll_ignoriert()
    {
        Directory.CreateDirectory(_tempRoot);
        var textFile = Path.Combine(_tempRoot, "Protokoll_06-001.txt");
        File.WriteAllText(textFile, "kein PDF");
        var storedFilesRaw = JsonSerializer.Serialize(new[] { textFile });
        IInspectionProtocolFileLocator locator = new InspectionProtocolFileLocator();

        var found = locator.FindProtocolPath(
            CreateRecord(),
            resolvedLink: null,
            initialFolder: null,
            projectPath: null,
            storedFilesRaw);

        Assert.Null(found);
    }

    [Fact]
    public void Gespeicherte_PDF_Pfade_laufen_ueber_den_injizierten_Resolver()
    {
        Directory.CreateDirectory(_tempRoot);
        var expected = Path.Combine(_tempRoot, "Protokoll_06-001.pdf");
        File.WriteAllText(expected, "PDF");
        var projectPath = Path.Combine(_tempRoot, "projekt.json");
        var resolver = new RecordingStoredImportFilePathResolver(expected);
        IInspectionProtocolFileLocator locator = new InspectionProtocolFileLocator(resolver);

        var found = locator.FindProtocolPath(
            CreateRecord(),
            resolvedLink: null,
            initialFolder: null,
            projectPath,
            storedFilesRaw: "gespeicherte-liste");

        Assert.Equal(expected, found, ignoreCase: true);
        Assert.Equal("PDF_StoredFiles", resolver.MetadataKey);
        Assert.Equal(projectPath, resolver.ProjectFilePath);
        Assert.Equal("gespeicherte-liste", resolver.RawMetadataValue);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
    }

    private static HaltungRecord CreateRecord()
    {
        var record = new HaltungRecord();
        record.SetFieldValue("Haltungsname", "06-001", FieldSource.Manual, userEdited: true);
        return record;
    }

    private sealed class RecordingStoredImportFilePathResolver(string resolvedPath)
        : IStoredImportFilePathResolver
    {
        public string? MetadataKey { get; private set; }

        public string? ProjectFilePath { get; private set; }

        public string? RawMetadataValue { get; private set; }

        public IReadOnlyList<string> ResolveExistingFiles(
            IDictionary<string, string> metadata,
            string metadataKey,
            string? projectFilePath)
        {
            MetadataKey = metadataKey;
            ProjectFilePath = projectFilePath;
            RawMetadataValue = metadata.TryGetValue(metadataKey, out var raw) ? raw : null;
            return [resolvedPath];
        }
    }
}
