using System.IO;
using System.Text.Json;
using AuswertungPro.Next.Application.Import;

namespace AuswertungPro.Next.Pipeline.Tests;

public class ImportRunReportExporterTests : IDisposable
{
    private readonly string _tempDir;

    public ImportRunReportExporterTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"import_report_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    [Fact]
    public void Export_CreatesThreeFiles()
    {
        var log = CreateSampleLog();
        log.Complete();

        ImportRunReportExporter.Export(log, _tempDir);

        var files = Directory.GetFiles(_tempDir);
        Assert.Equal(3, files.Length);
        Assert.Single(files, f => f.EndsWith(".txt") && Path.GetFileName(f).StartsWith("run_"));
        Assert.Single(files, f => f.EndsWith(".json"));
        Assert.Single(files, f => Path.GetFileName(f).StartsWith("fehlerliste_"));
    }

    [Fact]
    public void TextReport_ContainsSummary()
    {
        var log = CreateSampleLog();
        log.Complete();

        var txtPath = ImportRunReportExporter.Export(log, _tempDir);
        var content = File.ReadAllText(txtPath);

        Assert.Contains("IMPORT-BERICHT", content);
        Assert.Contains(log.RunId, content);
        Assert.Contains("TestImport", content);
        Assert.Contains("Erstellt:", content);
        Assert.Contains("Aktualisiert:", content);
        Assert.Contains("Konflikte:", content);
    }

    [Fact]
    public void JsonReport_IsValidJson()
    {
        var log = CreateSampleLog();
        log.Complete();

        ImportRunReportExporter.Export(log, _tempDir);
        var jsonFile = Directory.GetFiles(_tempDir, "*.json").Single();
        var content = File.ReadAllText(jsonFile);

        var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;
        Assert.Equal(log.RunId, root.GetProperty("runId").GetString());
        Assert.Equal("TestImport", root.GetProperty("importType").GetString());
        Assert.True(root.GetProperty("summary").GetProperty("totalCreated").GetInt32() >= 0);
        Assert.True(root.GetProperty("entries").GetArrayLength() > 0);
    }

    [Fact]
    public void ErrorReport_ListsOnlyErrors()
    {
        var log = CreateSampleLog();
        log.Complete();

        ImportRunReportExporter.Export(log, _tempDir);
        var errorFile = Directory.GetFiles(_tempDir, "fehlerliste_*").Single();
        var content = File.ReadAllText(errorFile);

        Assert.Contains("FEHLER", content);
        Assert.Contains("KONFLIKT", content);
        Assert.DoesNotContain("[INFO]", content);
    }

    [Fact]
    public void EmptyLog_ProducesNoErrorsMessage()
    {
        var log = new ImportRunLog { ImportType = "Empty" };
        log.Complete();

        ImportRunReportExporter.Export(log, _tempDir);
        var errorFile = Directory.GetFiles(_tempDir, "fehlerliste_*").Single();
        var content = File.ReadAllText(errorFile);

        Assert.Contains("Keine Fehler oder Konflikte", content);
    }

    private static ImportRunLog CreateSampleLog()
    {
        var log = new ImportRunLog { ImportType = "TestImport", SourcePath = "/tmp/test" };
        log.AddEntry("Parse", "ReadFile", ImportLogStatus.Info, sourceFile: "test.xtf");
        log.AddEntry("Merge", "SetField", ImportLogStatus.Created, recordKey: "H1-H2", field: "DN_mm", detail: "leer -> 300");
        log.AddEntry("Merge", "SetField", ImportLogStatus.Updated, recordKey: "H3-H4", field: "Rohrmaterial", detail: "'PVC' -> 'PE'");
        log.AddEntry("Merge", "Conflict", ImportLogStatus.Conflict, recordKey: "H5-H6", field: "Nutzungsart", detail: "UserEdited 'SW' vs 'MW'");
        log.AddEntry("Parse", "ReadFile", ImportLogStatus.Error, sourceFile: "broken.xtf", detail: "Datei nicht lesbar");
        return log;
    }
}
