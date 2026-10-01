using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import;
using AuswertungPro.Next.Infrastructure.Tests.Backup;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

/// <summary>
/// Charakterisierungstests fuer ProjectFieldCsvExporter.
/// Sichert CSV-Format, Escape-Logik und Dateiausgabe.
/// </summary>
public class ProjectFieldCsvExporterTests : IDisposable
{
    private readonly string _tmpDir;

    public ProjectFieldCsvExporterTests()
    {
        _tmpDir = Path.Combine(Path.GetTempPath(), "ProjectFieldCsvExporterTests_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tmpDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tmpDir))
            Directory.Delete(_tmpDir, recursive: true);
    }

    [Fact]
    public void Export_LeeresProjekt_ErstelltCsvMitHeader()
    {
        var project = new Project();
        var path = ProjectFieldCsvExporter.Export(project, _tmpDir);

        Assert.True(File.Exists(path));
        var text = File.ReadAllText(path);
        Assert.StartsWith("Type;RecordId;Field;Value;Source;UserEdited;LastUpdatedUtc", text);
    }

    [Fact]
    public void Export_EineHaltung_EnthaltHaltungZeile()
    {
        var project = new Project();
        var rec = project.CreateNewRecord();
        rec.SetFieldValue("Haltungsname", "H-001", FieldSource.Manual, userEdited: false);
        project.Data.Add(rec);

        var path = ProjectFieldCsvExporter.Export(project, _tmpDir);
        var text = File.ReadAllText(path);

        Assert.Contains("Haltung", text);
        Assert.Contains("H-001", text);
        Assert.Contains("Haltungsname", text);
    }

    [Fact]
    public void Export_SchachtRecord_EnthaltSchachtZeile()
    {
        var project = new Project();
        var schacht = new SchachtRecord();
        schacht.Fields["SchachtNr"] = "S-042";
        project.SchaechteData.Add(schacht);

        var path = ProjectFieldCsvExporter.Export(project, _tmpDir);
        var text = File.ReadAllText(path);

        Assert.Contains("Schacht", text);
        Assert.Contains("S-042", text);
    }

    [Fact]
    public void Export_ErstelltZielordner_WennNichtVorhanden()
    {
        var nestedDir = Path.Combine(_tmpDir, "nicht_vorhanden", "tief");
        var project = new Project();
        var path = ProjectFieldCsvExporter.Export(project, nestedDir);

        Assert.True(File.Exists(path));
    }

    [Fact]
    public void ImportSummaryExporter_verwendet_Berichtsordner_neben_Projektdatei()
    {
        var exporter = new ImportSummaryExporter();

        var path = exporter.Export(Path.Combine(_tmpDir, "projekt.json"), new Project());

        Assert.Equal(
            Path.Combine(_tmpDir, "__IMPORT_REPORTS"),
            Path.GetDirectoryName(path));
        Assert.True(File.Exists(path));
    }

    [JunctionFact]
    public void ImportSummaryExporter_SchreibtNichtDurchVerknuepftenBerichtsordner()
    {
        var projectRoot = Path.Combine(_tmpDir, "Projekt");
        var external = Path.Combine(_tmpDir, "Fremdziel");
        var reportLink = Path.Combine(projectRoot, ProjectStructure.ImportReports);
        Directory.CreateDirectory(projectRoot);
        Directory.CreateDirectory(external);
        JunctionTestSupport.CreateDirectoryLink(reportLink, external);

        try
        {
            var exporter = new ImportSummaryExporter();

            Assert.Throws<IOException>(() =>
                exporter.Export(Path.Combine(projectRoot, "projekt.json"), new Project()));
            Assert.Empty(Directory.EnumerateFiles(external));
        }
        finally
        {
            if (Directory.Exists(reportLink))
                Directory.Delete(reportLink);
        }
    }

    [Fact]
    public void Escape_NormaleWerte_UnveraendertZurueck()
    {
        Assert.Equal("BCD", ProjectFieldCsvExporter.Escape("BCD"));
        Assert.Equal("", ProjectFieldCsvExporter.Escape(""));
        Assert.Equal("", ProjectFieldCsvExporter.Escape(null));
    }

    [Fact]
    public void Escape_WertMitSemikolon_WirdQuotiert()
    {
        var result = ProjectFieldCsvExporter.Escape("A;B");
        Assert.Equal("\"A;B\"", result);
    }

    [Fact]
    public void Escape_WertMitAnfuehrungszeichen_WirdEscaped()
    {
        var result = ProjectFieldCsvExporter.Escape("Er sagte \"Hallo\"");
        Assert.Equal("\"Er sagte \"\"Hallo\"\"\"", result);
    }

    [Fact]
    public void Escape_WertMitZeilenumbruch_WirdQuotiert()
    {
        var result = ProjectFieldCsvExporter.Escape("Zeile1\nZeile2");
        Assert.StartsWith("\"", result);
        Assert.EndsWith("\"", result);
    }

    // Codeaudit 2026-08-17: Diese Escape-Kette (ProjectFieldCsvExporter ->
    // ImportSummaryExporter) setzte nur Anfuehrungszeichen. Formelanfaenge
    // blieben stehen, obwohl CsvCell sie zentral entschaerft und die
    // Projektregel jeden halben Exportweg verbietet. Der Importbericht enthaelt
    // Feldwerte aus Kundendaten - also fremden Text.
    [Theory]
    [InlineData("=1+1")]
    [InlineData("+42")]
    [InlineData("@SUM(A1)")]
    public void Escape_EntschaerftFormelanfaenge(string eingabe)
    {
        var ergebnis = ProjectFieldCsvExporter.Escape(eingabe);

        Assert.StartsWith("'", ergebnis.TrimStart('"'));
    }

    [Fact]
    public void Escape_LaesstNegativeZahlZahlBleiben()
    {
        Assert.Equal("-12.5", ProjectFieldCsvExporter.Escape("-12.5"));
    }
}
