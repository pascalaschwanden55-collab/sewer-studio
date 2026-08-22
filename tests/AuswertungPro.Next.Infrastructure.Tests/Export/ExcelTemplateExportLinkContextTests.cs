using System;
using System.IO;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Export;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Export.Excel;
using ClosedXML.Excel;

namespace AuswertungPro.Next.Infrastructure.Tests.Export;

public sealed class ExcelTemplateExportLinkContextTests
{
    [Fact]
    public void Neue_Overloads_bleiben_mit_alter_Implementierung_kompatibel()
    {
        IExcelExportService service = new AlteExcelExportImplementierung();
        var project = new Project();

        var haltungen = service.ExportToTemplate(
            project, "vorlage.xlsx", "ausgabe.xlsx", 1, 2, "projekt.json");
        var schaechte = service.ExportSchaechteToTemplate(
            project, "vorlage.xlsx", "ausgabe.xlsx", 1, 2, "projekt.json");

        Assert.True(haltungen.Ok);
        Assert.True(schaechte.Ok);
        Assert.Equal(2, ((AlteExcelExportImplementierung)service).Aufrufe);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Relativer_Haltungslink_wird_gegen_echten_Projektroot_aufgeloest(
        bool projektdateiImUnterordner)
    {
        using var directory = new TempDirectory();
        var templatePath = Path.Combine(directory.Path, "haltung-vorlage.xlsx");
        var outputPath = Path.Combine(directory.Path, "Berichte", "haltung.xlsx");
        var projectRoot = Path.Combine(directory.Path, "Projekt mit Umlaut ä");
        var projectFilePath = projektdateiImUnterordner
            ? Path.Combine(projectRoot, "Projektdateien", "projekt.json")
            : Path.Combine(projectRoot, "projekt.json");
        const string gespeichert = @"Haltungen_Verteilt\H 01\Prüfung ä.pdf";
        var erwartet = Path.GetFullPath(Path.Combine(projectRoot, gespeichert));
        ErzeugeHaltungsVorlage(templatePath);

        var result = new ExcelTemplateExportService().ExportToTemplate(
            ProjektMitHaltungslink(gespeichert),
            templatePath,
            outputPath,
            headerRow: 1,
            startRow: 2,
            projectFilePath);

        Assert.False(File.Exists(erwartet));
        Assert.True(result.Ok, $"{result.ErrorCode}: {result.ErrorMessage}");
        AssertExternesZiel(outputPath, "Haltungen", "B2", erwartet);
    }

    [Theory]
    [InlineData(@"C:\Nicht vorhanden\Prüfung ä.pdf")]
    [InlineData("https://example.org/Pruefung-%C3%A4.pdf")]
    public void Absoluter_oder_Web_Link_bleibt_mit_Projektkontext_unveraendert(string gespeichert)
    {
        using var directory = new TempDirectory();
        var templatePath = Path.Combine(directory.Path, "haltung-vorlage.xlsx");
        var outputPath = Path.Combine(directory.Path, "haltung.xlsx");
        var projectFilePath = Path.Combine(directory.Path, "Projekt", "Projektdateien", "projekt.json");
        ErzeugeHaltungsVorlage(templatePath);

        var result = new ExcelTemplateExportService().ExportToTemplate(
            ProjektMitHaltungslink(gespeichert),
            templatePath,
            outputPath,
            headerRow: 1,
            startRow: 2,
            projectFilePath);

        Assert.True(result.Ok, $"{result.ErrorCode}: {result.ErrorMessage}");
        AssertExternesZiel(outputPath, "Haltungen", "B2", gespeichert);
    }

    [Fact]
    public void Pfadausbruch_wird_nicht_als_anklickbarer_Link_exportiert()
    {
        using var directory = new TempDirectory();
        var templatePath = Path.Combine(directory.Path, "haltung-vorlage.xlsx");
        var outputPath = Path.Combine(directory.Path, "haltung.xlsx");
        var projectFilePath = Path.Combine(directory.Path, "Projekt", "Projektdateien", "projekt.json");
        const string gespeichert = @"..\fremd\geheim.pdf";
        ErzeugeHaltungsVorlage(templatePath);

        var result = new ExcelTemplateExportService().ExportToTemplate(
            ProjektMitHaltungslink(gespeichert),
            templatePath,
            outputPath,
            headerRow: 1,
            startRow: 2,
            projectFilePath);

        Assert.True(result.Ok, $"{result.ErrorCode}: {result.ErrorMessage}");
        using var workbook = new XLWorkbook(outputPath);
        var cell = workbook.Worksheet("Haltungen").Cell("B2");
        Assert.False(cell.HasHyperlink);
        Assert.Equal(gespeichert, cell.GetString());
    }

    [Theory]
    [InlineData(FieldKeys.PdfPath, false)]
    [InlineData(FieldKeys.PdfEigen, false)]
    [InlineData(FieldKeys.PdfAll, true)]
    public void Schacht_Linkspalte_nimmt_belegte_Pfad_Aliase_als_Fallback(
        string feld,
        bool mehrerePfade)
    {
        using var directory = new TempDirectory();
        var templatePath = Path.Combine(directory.Path, "schacht-vorlage.xlsx");
        var outputPath = Path.Combine(directory.Path, "Berichte", "schacht.xlsx");
        var projectRoot = Path.Combine(directory.Path, "Projekt ä");
        var projectFilePath = Path.Combine(projectRoot, "Projektdateien", "projekt.json");
        const string ersterPfad = @"Schächte_Verteilt\S 01\Protokoll ä.pdf";
        var gespeicherterWert = mehrerePfade
            ? $"{ersterPfad};Schächte_Verteilt\\S 01\\zweites.pdf"
            : ersterPfad;
        var erwartet = Path.GetFullPath(Path.Combine(projectRoot, ersterPfad));
        ErzeugeSchachtVorlage(templatePath);
        var project = new Project();
        var record = new SchachtRecord();
        record.SetFieldValue("Schachtnummer", "S 01");
        record.SetFieldValue(feld, gespeicherterWert);
        project.SchaechteData.Add(record);

        var result = new ExcelTemplateExportService().ExportSchaechteToTemplate(
            project,
            templatePath,
            outputPath,
            headerRow: 1,
            startRow: 2,
            projectFilePath);

        Assert.False(File.Exists(erwartet));
        Assert.True(result.Ok, $"{result.ErrorCode}: {result.ErrorMessage}");
        AssertExternesZiel(outputPath, "Schaechte", "B2", erwartet);
    }

    [Fact]
    public void Schacht_Link_Feld_hat_Vorrang_vor_den_Fallbacks()
    {
        using var directory = new TempDirectory();
        var templatePath = Path.Combine(directory.Path, "schacht-vorlage.xlsx");
        var outputPath = Path.Combine(directory.Path, "schacht.xlsx");
        var projectRoot = Path.Combine(directory.Path, "Projekt");
        var projectFilePath = Path.Combine(projectRoot, "projekt.json");
        const string link = @"Schächte_Verteilt\S-1\link.pdf";
        ErzeugeSchachtVorlage(templatePath);
        var project = new Project();
        var record = new SchachtRecord();
        record.SetFieldValue("Schachtnummer", "S-1");
        record.SetFieldValue(FieldKeys.Link, link);
        record.SetFieldValue(FieldKeys.PdfPath, @"Schächte_Verteilt\S-1\fallback.pdf");
        project.SchaechteData.Add(record);

        var result = new ExcelTemplateExportService().ExportSchaechteToTemplate(
            project,
            templatePath,
            outputPath,
            headerRow: 1,
            startRow: 2,
            projectFilePath);

        Assert.True(result.Ok, $"{result.ErrorCode}: {result.ErrorMessage}");
        AssertExternesZiel(
            outputPath,
            "Schaechte",
            "B2",
            Path.GetFullPath(Path.Combine(projectRoot, link)));
    }

    private static Project ProjektMitHaltungslink(string link)
    {
        var project = new Project();
        var record = new HaltungRecord();
        record.SetFieldValue(FieldKeys.HoldingName, "H 01", FieldSource.Manual, userEdited: false);
        record.SetFieldValue(FieldKeys.Link, link, FieldSource.Manual, userEdited: false);
        project.Data.Add(record);
        return project;
    }

    private static void ErzeugeHaltungsVorlage(string path)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Haltungen");
        sheet.Cell("A1").Value = "Haltungsnahme (ID)";
        sheet.Cell("B1").Value = "Link";
        workbook.SaveAs(path);
    }

    private static void ErzeugeSchachtVorlage(string path)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Schaechte");
        sheet.Cell("A1").Value = "Schachtnummer";
        sheet.Cell("B1").Value = "Link";
        workbook.SaveAs(path);
    }

    private static void AssertExternesZiel(
        string workbookPath,
        string sheetName,
        string cellAddress,
        string expected)
    {
        using var workbook = new XLWorkbook(workbookPath);
        var link = workbook.Worksheet(sheetName).Cell(cellAddress).GetHyperlink();
        Assert.True(link.IsExternal);
        Assert.NotNull(link.ExternalAddress);
        Assert.Equal(expected, link.ExternalAddress!.OriginalString);
        Assert.Null(link.InternalAddress);
    }

    private sealed class AlteExcelExportImplementierung : IExcelExportService
    {
        public int Aufrufe { get; private set; }

        public Result ExportToTemplate(
            Project project,
            string templatePath,
            string outputPath,
            int headerRow,
            int startRow)
        {
            Aufrufe++;
            return Result.Success();
        }

        public Result ExportSchaechteToTemplate(
            Project project,
            string templatePath,
            string outputPath,
            int headerRow,
            int startRow)
        {
            Aufrufe++;
            return Result.Success();
        }
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = Directory.CreateTempSubdirectory().FullName;

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                    Directory.Delete(Path, recursive: true);
            }
            catch
            {
                // Testdaten; ein Cleanup-Fehler darf den Befund nicht verdecken.
            }
        }
    }
}
