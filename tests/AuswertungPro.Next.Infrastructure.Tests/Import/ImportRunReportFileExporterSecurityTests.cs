using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Infrastructure.Import;
using AuswertungPro.Next.Infrastructure.Tests.Backup;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

public sealed class ImportRunReportFileExporterSecurityTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(
        Path.GetTempPath(),
        $"ImportRunReportSecurity_{Guid.NewGuid():N}");

    [JunctionFact]
    public void Export_SchreibtNichtDurchVerknuepftenProjektBerichtsordner()
    {
        var projectRoot = Path.Combine(_tempRoot, "Projekt");
        var external = Path.Combine(_tempRoot, "Fremdziel");
        var reportLink = Path.Combine(projectRoot, ProjectStructure.ImportReports);
        Directory.CreateDirectory(projectRoot);
        Directory.CreateDirectory(external);
        JunctionTestSupport.CreateDirectoryLink(reportLink, external);

        try
        {
            var log = new ImportRunLog { ImportType = "Sicherheitstest" };
            log.Complete();
            var exporter = new ImportRunReportFileExporter();

            Assert.Throws<IOException>(() => exporter.Export(log, reportLink));
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
        try
        {
            if (Directory.Exists(_tempRoot))
                Directory.Delete(_tempRoot, recursive: true);
        }
        catch
        {
            // Test-Aufraeumen darf das Ergebnis nicht verdecken.
        }
    }
}
