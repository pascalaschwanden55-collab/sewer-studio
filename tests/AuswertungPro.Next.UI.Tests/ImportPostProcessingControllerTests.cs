using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Services;
using System.IO;

namespace AuswertungPro.Next.UI.Tests;

public sealed class ImportPostProcessingControllerTests
{
    [Fact]
    public void TrackImportSource_speichert_letzte_Quelle_und_begrenzt_Historie_auf_20()
    {
        var project = new Project();
        project.Metadata["ImportQuellenHistorie"] = string.Join(
            "\n",
            Enumerable.Range(1, 20).Select(value => $"alt-{value:00}"));

        ImportPostProcessingController.TrackImportSource(
            project,
            @"C:\Import\WinCan",
            "WinCan",
            new DateTime(2026, 7, 12, 14, 5, 0));

        Assert.Equal(@"C:\Import\WinCan", project.Metadata["ImportQuelle"]);
        Assert.Equal("WinCan", project.Metadata["ImportQuellTyp"]);
        var history = project.Metadata["ImportQuellenHistorie"].Split('\n');
        Assert.Equal(20, history.Length);
        Assert.DoesNotContain("alt-01", history);
        Assert.Equal("alt-02", history[0]);
        Assert.Equal("2026-07-12 14:05 | WinCan | C:\\Import\\WinCan", history[^1]);
    }

    [Fact]
    public async Task RunAsync_laesst_Pdfs_nachlesen_und_meldet_das_unspeicherte_Projekt()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "protokoll.pdf"), "test");
        File.WriteAllText(Path.Combine(temp.Path, "ignorieren.txt"), "test");
        var pdfImport = new FakePdfImportService(_ => Result<ImportStats>.Success(
            new ImportStats(Found: 2, Created: 0, Updated: 1, Errors: 0, Uncertain: 0, Messages: [])));
        var state = new UiState();
        var project = new Project();

        await ImportPostProcessingController.RunAsync(
            Request(temp.Path, project, pdfImport),
            Actions(state));

        Assert.Single(pdfImport.Paths);
        Assert.EndsWith("protokoll.pdf", pdfImport.Paths[0], StringComparison.OrdinalIgnoreCase);
        Assert.Contains("PDF-Scan: 1 Dateien, 2 Haltungen zugeordnet, 1 aktualisiert, 0 Fehler", state.Summary);
        Assert.Contains("PDF-Scan: 1 Dateien", state.Details);
        Assert.Contains("Projekt bitte speichern", state.Details);
        Assert.Equal(temp.Path, project.Metadata["ImportQuelle"]);
        Assert.Equal("WinCan", project.Metadata["ImportQuellTyp"]);
        Assert.Empty(state.StatusMessages);
    }

    [Fact]
    public async Task RunAsync_faengt_einen_Pdf_Fehler_ab_und_verarbeitet_die_naechste_Datei()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "defekt.pdf"), "test");
        File.WriteAllText(Path.Combine(temp.Path, "ok.pdf"), "test");
        var pdfImport = new FakePdfImportService(path =>
        {
            if (Path.GetFileName(path).Equals("defekt.pdf", StringComparison.OrdinalIgnoreCase))
                throw new IOException("Testfehler");

            return Result<ImportStats>.Success(
                new ImportStats(Found: 1, Created: 0, Updated: 1, Errors: 0, Uncertain: 0, Messages: []));
        });
        var state = new UiState();

        await ImportPostProcessingController.RunAsync(
            Request(temp.Path, new Project(), pdfImport),
            Actions(state));

        Assert.Equal(2, pdfImport.Paths.Count);
        Assert.Contains("PDF-Scan: 2 Dateien, 1 Haltungen zugeordnet, 1 aktualisiert, 1 Fehler", state.Summary);
    }

    [Fact]
    public async Task RunAsync_liesst_Pdfs_in_einem_benannten_Unterordner_nur_einmal()
    {
        using var temp = new TempDirectory();
        var reportFolder = Path.Combine(temp.Path, "Report");
        Directory.CreateDirectory(reportFolder);
        File.WriteAllText(Path.Combine(temp.Path, "wurzel.pdf"), "test");
        File.WriteAllText(Path.Combine(reportFolder, "bericht.pdf"), "test");
        var pdfImport = new FakePdfImportService(_ => Result<ImportStats>.Success(
            new ImportStats(Found: 1, Created: 0, Updated: 1, Errors: 0, Uncertain: 0, Messages: [])));

        await ImportPostProcessingController.RunAsync(
            Request(temp.Path, new Project(), pdfImport),
            Actions(new UiState()));

        Assert.Equal(2, pdfImport.Paths.Count);
        Assert.Equal(2, pdfImport.Paths.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    private static ImportPostProcessingRequest Request(
        string sourceFolder,
        Project project,
        IPdfImportService pdfImport)
        => new(
            SourceFolder: sourceFolder,
            SourceLabel: "WinCan",
            Project: project,
            ProjectFolder: null,
            PdfImport: pdfImport,
            PdfToTextPath: null,
            FillMissingOnly: true,
            Context: null,
            CollectionLock: null);

    private static ImportPostProcessingActions Actions(UiState state)
        => new(
            SetProgressText: value => state.Progress = value,
            SetProgressPercent: value => state.ProgressPercent = value,
            AppendSummaryText: value => state.Summary += value,
            AppendDetailsText: value => state.Details += value,
            SetStatus: value => state.StatusMessages.Add(value));

    private sealed class FakePdfImportService(
        Func<string, Result<ImportStats>> import) : IPdfImportService
    {
        public List<string> Paths { get; } = [];

        public Result<ImportStats> ImportPdf(
            string pdfPath,
            Project project,
            string? pdfToTextPath,
            bool fillMissingOnly = false,
            ImportRunContext? ctx = null)
        {
            Paths.Add(pdfPath);
            Assert.True(fillMissingOnly);
            return import(pdfPath);
        }
    }

    private sealed class UiState
    {
        public string Progress { get; set; } = "";
        public double ProgressPercent { get; set; }
        public string Summary { get; set; } = "";
        public string Details { get; set; } = "";
        public List<string> StatusMessages { get; } = [];
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "sewerstudio-import-post-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }
}
