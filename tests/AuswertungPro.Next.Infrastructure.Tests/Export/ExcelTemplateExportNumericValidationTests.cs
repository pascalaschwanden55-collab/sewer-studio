using System;
using System.IO;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Export.Excel;
using ClosedXML.Excel;

namespace AuswertungPro.Next.Infrastructure.Tests.Export;

public sealed class ExcelTemplateExportNumericValidationTests
{
    [Fact]
    public void Ungueltige_Haltungszahl_stoppt_den_Export_ohne_Ziel_zu_veraendern()
    {
        using var directory = new TempDirectory();
        var templatePath = Path.Combine(directory.Path, "haltung-vorlage.xlsx");
        var outputPath = Path.Combine(directory.Path, "haltung.xlsx");
        ErzeugeVorlage(templatePath, "Haltungen", "Haltungsnahme (ID)", "Kosten");
        var bestehenderInhalt = "bereits veroeffentlichter Bericht"u8.ToArray();
        File.WriteAllBytes(outputPath, bestehenderInhalt);
        const string roherWert = "VERTRAULICH-KEINE-ZAHL";
        var project = new Project();
        var record = new HaltungRecord();
        record.SetFieldValue(FieldKeys.HoldingName, "H-1", FieldSource.Manual, userEdited: false);
        record.SetFieldValue(FieldKeys.Cost, roherWert, FieldSource.Manual, userEdited: false);
        project.Data.Add(record);

        var result = new ExcelTemplateExportService().ExportToTemplate(
            project, templatePath, outputPath, headerRow: 1, startRow: 2);

        Assert.False(result.Ok);
        Assert.Equal("EXP-EXCEL", result.ErrorCode);
        Assert.Contains("Zeile 2", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Spalte", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Kosten", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(roherWert, result.ErrorMessage, StringComparison.Ordinal);
        Assert.Equal(bestehenderInhalt, File.ReadAllBytes(outputPath));
    }

    [Fact]
    public void Ungueltige_Schachtzahl_stoppt_den_Export_ohne_Ziel_zu_veraendern()
    {
        using var directory = new TempDirectory();
        var templatePath = Path.Combine(directory.Path, "schacht-vorlage.xlsx");
        var outputPath = Path.Combine(directory.Path, "schacht.xlsx");
        ErzeugeVorlage(templatePath, "Schaechte", "Schachtnummer", "Kosten");
        var bestehenderInhalt = "bereits veroeffentlichter Bericht"u8.ToArray();
        File.WriteAllBytes(outputPath, bestehenderInhalt);
        const string roherWert = "VERTRAULICH-KEINE-ZAHL";
        var project = new Project();
        var record = new SchachtRecord();
        record.SetFieldValue("Schachtnummer", "S-1");
        record.SetFieldValue(FieldKeys.Cost, roherWert);
        project.SchaechteData.Add(record);

        var result = new ExcelTemplateExportService().ExportSchaechteToTemplate(
            project, templatePath, outputPath, headerRow: 1, startRow: 2);

        Assert.False(result.Ok);
        Assert.Equal("EXP-EXCEL-SCHACHT", result.ErrorCode);
        Assert.Contains("Zeile 2", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Spalte", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Kosten", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(roherWert, result.ErrorMessage, StringComparison.Ordinal);
        Assert.Equal(bestehenderInhalt, File.ReadAllBytes(outputPath));
    }

    private static void ErzeugeVorlage(
        string path,
        string sheetName,
        string ersteSpalte,
        string zweiteSpalte)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet(sheetName);
        sheet.Cell("A1").Value = ersteSpalte;
        sheet.Cell("B1").Value = zweiteSpalte;
        workbook.SaveAs(path);
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
