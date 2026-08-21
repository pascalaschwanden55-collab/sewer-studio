using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Infrastructure.Import;
using AuswertungPro.Next.Infrastructure.Tests.Backup;
using Microsoft.Extensions.Logging.Abstractions;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

public sealed class OneClickImportReportWriterTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(
        Path.GetTempPath(),
        "SewerStudio_OneClickReport_" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void TryWrite_schreibt_nachvollziehbaren_Report_in_Projektordner()
    {
        var writer = new OneClickImportReportWriter(NullLogger.Instance);
        var result = new OneClickProjectImportResult(
            OneClickProjectImportFormat.Ikas,
            Found: 5,
            Created: 2,
            Updated: 3,
            Errors: 1,
            Conflicts: 4,
            Messages: new[] { "Datei A verarbeitet" });

        writer.TryWrite(_tempRoot, result);

        var report = Assert.Single(Directory.GetFiles(
            Path.Combine(_tempRoot, "__IMPORT_REPORTS"),
            "kanalimport_*.txt"));
        var text = File.ReadAllText(report);
        Assert.Contains("Format: Ikas", text);
        Assert.Contains("Haltungen: 5 (neu 2, aktualisiert 3)", text);
        Assert.Contains("Fehler: 1, Feld-Konflikte: 4", text);
        Assert.Contains("Datei A verarbeitet", text);
    }

    [JunctionFact]
    public void TryWrite_SchreibtNichtDurchVerknuepftenBerichtsordner()
    {
        var projectRoot = Path.Combine(_tempRoot, "Projekt");
        var external = Path.Combine(_tempRoot, "Fremdziel");
        var reportLink = Path.Combine(projectRoot, ProjectStructure.ImportReports);
        Directory.CreateDirectory(projectRoot);
        Directory.CreateDirectory(external);
        JunctionTestSupport.CreateDirectoryLink(reportLink, external);

        try
        {
            var writer = new OneClickImportReportWriter(NullLogger.Instance);
            var result = new OneClickProjectImportResult(
                OneClickProjectImportFormat.Ikas,
                Found: 1,
                Created: 1,
                Updated: 0,
                Errors: 0,
                Conflicts: 0,
                Messages: Array.Empty<string>());

            writer.TryWrite(projectRoot, result);

            Assert.Empty(Directory.EnumerateFiles(external));
        }
        finally
        {
            if (Directory.Exists(reportLink))
                Directory.Delete(reportLink);
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
    }
}
