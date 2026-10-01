using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Export.Excel;
using ClosedXML.Excel;

namespace AuswertungPro.Next.Infrastructure.Tests.Export;

public sealed class ExcelTemplateExportSafetyTests
{
    [Fact]
    public void Fehlende_Felder_bleiben_echte_Leerzellen_und_COUNTA_bleibt_null()
    {
        using var directory = new TempDirectory();
        var templatePath = Path.Combine(directory.Path, "vorlage.xlsx");
        var outputPath = Path.Combine(directory.Path, "ausgabe.xlsx");
        ErzeugeVorlage(templatePath, mitAltdaten: true);

        var project = ProjektMitHaltung(link: null);
        var result = new ExcelTemplateExportService().ExportToTemplate(
            project, templatePath, outputPath, headerRow: 1, startRow: 2);

        Assert.True(result.Ok, $"{result.ErrorCode}: {result.ErrorMessage}");
        using var workbook = new XLWorkbook(outputPath);
        workbook.RecalculateAllFormulas();
        var data = workbook.Worksheet("Haltungen");

        Assert.True(data.Cell("B2").IsEmpty(), "Eine fehlende Strasse muss eine echte Leerzelle bleiben.");
        Assert.Equal(XLDataType.Blank, data.Cell("B2").DataType);
        Assert.True(data.Cell("C2").IsEmpty(), "Fehlende Kosten muessen eine echte Leerzelle bleiben.");
        Assert.Equal(XLDataType.Blank, data.Cell("C2").DataType);
        Assert.True(data.Cell("D2").IsEmpty(), "Ein alter Vorlagenlink darf nicht erhalten bleiben.");
        Assert.False(data.Cell("D2").HasHyperlink);
        Assert.Equal(0d, workbook.Worksheet("Auswertung").Cell("A1").GetDouble());
    }

    [Fact]
    public void Ziel_gleich_Vorlage_wird_abgewiesen_und_die_Vorlage_bleibt_bytegleich()
    {
        using var directory = new TempDirectory();
        var templatePath = Path.Combine(directory.Path, "vorlage.xlsx");
        ErzeugeVorlage(templatePath);
        var vorher = SHA256.HashData(File.ReadAllBytes(templatePath));

        var result = new ExcelTemplateExportService().ExportToTemplate(
            ProjektMitHaltung(link: null), templatePath, templatePath, headerRow: 1, startRow: 2);

        Assert.False(result.Ok);
        Assert.Contains("Vorlage", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(vorher, SHA256.HashData(File.ReadAllBytes(templatePath)));
    }

    [Fact]
    public void Erfolgreicher_Export_veraendert_die_Vorlage_nicht()
    {
        using var directory = new TempDirectory();
        var templatePath = Path.Combine(directory.Path, "vorlage.xlsx");
        var outputPath = Path.Combine(directory.Path, "ausgabe.xlsx");
        ErzeugeVorlage(templatePath);
        var vorher = SHA256.HashData(File.ReadAllBytes(templatePath));

        var result = new ExcelTemplateExportService().ExportToTemplate(
            ProjektMitHaltung(link: null), templatePath, outputPath, headerRow: 1, startRow: 2);

        Assert.True(result.Ok, $"{result.ErrorCode}: {result.ErrorMessage}");
        Assert.Equal(vorher, SHA256.HashData(File.ReadAllBytes(templatePath)));
        using var pruefung = new XLWorkbook(outputPath);
        Assert.Equal("H-001", pruefung.Worksheet("Haltungen").Cell("A2").GetString());
    }

    [Fact]
    public void Ungueltige_Temporaerdatei_wird_nicht_veroeffentlicht_und_der_Bestand_bleibt_erhalten()
    {
        using var directory = new TempDirectory();
        var templatePath = Path.Combine(directory.Path, "vorlage.xlsx");
        var outputPath = Path.Combine(directory.Path, "ausgabe.xlsx");
        ErzeugeVorlage(templatePath);
        var bestehenderInhalt = "bereits freigegebene Arbeitsmappe"u8.ToArray();
        File.WriteAllBytes(outputPath, bestehenderInhalt);
        string? tatsaechlicherSchreibpfad = null;
        var service = new ExcelTemplateExportService((_, path) =>
        {
            tatsaechlicherSchreibpfad = path;
            File.WriteAllText(path, "absichtlich keine gueltige Excel-Datei");
        });

        var result = service.ExportToTemplate(
            ProjektMitHaltung(link: null), templatePath, outputPath, headerRow: 1, startRow: 2);

        Assert.False(result.Ok);
        Assert.Equal(bestehenderInhalt, File.ReadAllBytes(outputPath));
        Assert.NotNull(tatsaechlicherSchreibpfad);
        Assert.NotEqual(Path.GetFullPath(outputPath), Path.GetFullPath(tatsaechlicherSchreibpfad!));
        Assert.False(File.Exists(tatsaechlicherSchreibpfad));
    }

    [Fact]
    public void Abbruch_nach_dem_Schreiben_veroeffentlicht_keine_Datei_und_raeumt_temporaerdatei_auf()
    {
        using var directory = new TempDirectory();
        var templatePath = Path.Combine(directory.Path, "vorlage.xlsx");
        var outputPath = Path.Combine(directory.Path, "ausgabe.xlsx");
        ErzeugeVorlage(templatePath);
        var bestehenderInhalt = "bestehende freigegebene Datei"u8.ToArray();
        File.WriteAllBytes(outputPath, bestehenderInhalt);
        using var cancellation = new CancellationTokenSource();
        string? temporaerpfad = null;
        var service = new ExcelTemplateExportService((workbook, path) =>
        {
            temporaerpfad = path;
            workbook.SaveAs(path);
            cancellation.Cancel();
        });

        Assert.Throws<OperationCanceledException>(() => service.ExportToTemplate(
            ProjektMitHaltung(link: null),
            templatePath,
            outputPath,
            headerRow: 1,
            startRow: 2,
            projectFilePath: null,
            cancellationToken: cancellation.Token));

        Assert.Equal(bestehenderInhalt, File.ReadAllBytes(outputPath));
        Assert.NotNull(temporaerpfad);
        Assert.False(File.Exists(temporaerpfad));
    }

    [Theory]
    [InlineData(@"Haltungen_Verteilt\H-001\Pruefung ä 2026.pdf")]
    [InlineData(@"C:\Nicht vorhanden\Pruefung ä 2026.pdf")]
    [InlineData("https://example.org/Pruefung-%C3%A4-2026.pdf")]
    public void Link_wird_ohne_Quellzugriff_als_externes_Ziel_gespeichert(string ziel)
    {
        using var directory = new TempDirectory();
        var templatePath = Path.Combine(directory.Path, "vorlage.xlsx");
        var outputPath = Path.Combine(directory.Path, "ausgabe.xlsx");
        ErzeugeVorlage(templatePath);

        var result = new ExcelTemplateExportService().ExportToTemplate(
            ProjektMitHaltung(ziel), templatePath, outputPath, headerRow: 1, startRow: 2);

        Assert.True(result.Ok, $"{result.ErrorCode}: {result.ErrorMessage}");
        using var workbook = new XLWorkbook(outputPath);
        var link = workbook.Worksheet("Haltungen").Cell("D2").GetHyperlink();
        Assert.True(link.IsExternal);
        Assert.NotNull(link.ExternalAddress);
        Assert.Equal(ziel, link.ExternalAddress!.OriginalString);
        Assert.Null(link.InternalAddress);
    }

    private static Project ProjektMitHaltung(string? link)
    {
        var project = new Project { Name = "Sicherheitspruefung" };
        var record = new HaltungRecord();
        record.SetFieldValue("Haltungsname", "H-001", FieldSource.Manual, userEdited: false);
        if (link is not null)
            record.SetFieldValue("Link", link, FieldSource.Manual, userEdited: false);
        project.Data.Add(record);
        return project;
    }

    private static void ErzeugeVorlage(string path, bool mitAltdaten = false)
    {
        using var workbook = new XLWorkbook();
        var data = workbook.AddWorksheet("Haltungen");
        data.Cell("A1").Value = "Haltungsnahme (ID)";
        data.Cell("B1").Value = "Strasse";
        data.Cell("C1").Value = "Kosten";
        data.Cell("D1").Value = "Link";

        if (mitAltdaten)
        {
            data.Cell("B2").Value = "Altstrasse";
            data.Cell("C2").Value = 999d;
            data.Cell("D2").Value = "alt";
            data.Cell("D2").SetHyperlink(new XLHyperlink(new Uri("Alt.pdf", UriKind.Relative)));
        }

        var auswertung = workbook.AddWorksheet("Auswertung");
        auswertung.Cell("A1").FormulaA1 = "COUNTA(Haltungen!B2:B10)";
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
                // Der Testordner enthaelt keine Kundendaten; Cleanup darf den Befund nicht verdecken.
            }
        }
    }
}
