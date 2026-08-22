using System;
using System.Collections.Generic;
using System.IO;
using AuswertungPro.Next.Application.Export;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Export.Excel;
using ClosedXML.Excel;

namespace AuswertungPro.Next.Infrastructure.Tests.Export;

/// <summary>
/// Schuetzt den Vertrag zwischen den gespeicherten SewerStudio-Feldern und den
/// lesbaren Spaltennamen der beiden Excel-Vorlagen. Besonders bei Schaechten
/// unterscheiden sich Feldschluessel und sichtbare Ueberschrift historisch.
/// </summary>
public sealed class ExcelTemplateDataTransferTests
{
    [Fact]
    public void Haltungen_werden_in_alle_Vorlagenspalten_und_mit_passenden_Typen_uebertragen()
    {
        var project = new Project { Name = "Musterprojekt" };
        var record = new HaltungRecord();
        Set(record, "NR", "007");
        Set(record, FieldKeys.HoldingName, "00123-00124");
        Set(record, FieldKeys.Street, "Bahnhofstrasse");
        Set(record, FieldKeys.PipeMaterial, "PP");
        Set(record, FieldKeys.NominalDiameterMm, "300");
        Set(record, FieldKeys.UsageType, "Schmutzwasser");
        Set(record, FieldKeys.HoldingLengthMeters, "12.5");
        Set(record, "Inspektionsrichtung", "In Fliessrichtung");
        Set(record, FieldKeys.PrimaryDamages, "BAB Riss");
        Set(record, FieldKeys.ConditionClass, "2");
        Set(record, "Pruefungsresultat", "Prüfung bestanden");
        Set(record, FieldKeys.RenovationDecision, "Ja");
        Set(record, FieldKeys.RecommendedRehabilitationMeasures, "Schlauchliner");
        Set(record, FieldKeys.Cost, "1'234.50");
        Set(record, FieldKeys.Owner, "Gemeinde");
        Set(record, FieldKeys.RehabilitationExecutor, "Kanalsanierer");
        Set(record, FieldKeys.Remarks, "kontrolliert");
        Set(record, FieldKeys.Link, @"Haltungen_Verteilt\00123-00124\Video ä 01.mpg");
        Set(record, FieldKeys.LinerRenovationCount, "1");
        Set(record, FieldKeys.LinerRenovationMeters, "12.5");
        Set(record, FieldKeys.ConnectionsToGrout, "2");
        Set(record, FieldKeys.RepairSleeve, "3");
        Set(record, FieldKeys.LinerEndSleeve, "4");
        Set(record, FieldKeys.ShortLinerRepair, "5");
        Set(record, "Erneuerung_Neubau_m", "6.5");
        Set(record, FieldKeys.WorkflowStatus, "offen");
        Set(record, FieldKeys.InspectionYear, "24.09.2025");
        project.Data.Add(record);

        using var output = new TempExcelFile();
        var result = new ExcelTemplateExportService().ExportToTemplate(
            project,
            Template("Haltungen.xlsx"),
            output.Path,
            ExcelVorlagenLayout.KopfZeile,
            ExcelVorlagenLayout.ErsteDatenZeile);

        Assert.True(result.Ok, $"{result.ErrorCode}: {result.ErrorMessage}");
        using var workbook = new XLWorkbook(output.Path);
        var worksheet = workbook.Worksheet("Haltungen");
        var columns = ReadColumns(worksheet);
        var row = ExcelVorlagenLayout.ErsteDatenZeile;

        AssertText(worksheet, row, columns, "NR.", "007");
        AssertText(worksheet, row, columns, "Haltungsname (ID)", "00123-00124");
        AssertText(worksheet, row, columns, "Strasse", "Bahnhofstrasse");
        AssertText(worksheet, row, columns, "Rohrmaterial", "Polypropylen");
        AssertNumber(worksheet, row, columns, "DN mm", 300d);
        AssertText(worksheet, row, columns, "Nutzungsart", "Schmutzwasser");
        AssertNumber(worksheet, row, columns, "Haltungslänge m", 12.5d);
        AssertText(worksheet, row, columns, "Inspektionsrichtung", "In Fliessrichtung");
        AssertText(worksheet, row, columns, "Primäre Schäden", "BAB Riss");
        AssertText(worksheet, row, columns, "Zustandsklasse", "2");
        AssertText(worksheet, row, columns, "Prüfungsresultat", "Prüfung bestanden");
        AssertText(worksheet, row, columns, "Sanieren Ja/Nein", "Ja");
        AssertText(worksheet, row, columns, "Empfohlene Sanierungsmassnahmen", "Schlauchliner");
        AssertNumber(worksheet, row, columns, "Kosten", 1234.5d);
        AssertText(worksheet, row, columns, "Eigentümer", "Gemeinde");
        AssertText(worksheet, row, columns, "Ausgeführt durch", "Kanalsanierer");
        AssertText(worksheet, row, columns, "Bemerkungen", "kontrolliert");
        AssertLink(worksheet, row, columns, "Link", @"Haltungen_Verteilt\00123-00124\Video ä 01.mpg");
        AssertNumber(worksheet, row, columns, "Renovierung Inliner Stk.", 1d);
        AssertNumber(worksheet, row, columns, "Renovierung Inliner m", 12.5d);
        AssertNumber(worksheet, row, columns, "Anschlüsse verpressen", 2d);
        AssertNumber(worksheet, row, columns, "Reparatur Manschette", 3d);
        AssertNumber(worksheet, row, columns, "Linerendmanschette LEM", 4d);
        AssertNumber(worksheet, row, columns, "Reparatur Kurzliner", 5d);
        AssertNumber(worksheet, row, columns, "Erneuerung Neubau m", 6.5d);
        AssertText(worksheet, row, columns, "offen/abgeschlossen", "offen");
        AssertText(worksheet, row, columns, "Datum/Jahr", "24.09.2025");
    }

    [Fact]
    public void Schaechte_werden_aus_den_persistierten_Feldvarianten_verlustfrei_uebertragen()
    {
        var project = new Project { Name = "Musterprojekt" };
        var record = new SchachtRecord();
        record.SetFieldValue("NR", "007");
        record.SetFieldValue("Funktion", "Kontrollschacht");
        record.SetFieldValue("Schachtnummer", "00123");
        record.SetFieldValue("Strasse", "Bahnhofstrasse");
        record.SetFieldValue("Primaere_Schaeden", "Korrosion");
        record.SetFieldValue("Zustandsklasse", "2");
        record.SetFieldValue("Sanieren_JaNein", "Ja");
        record.SetFieldValue("Massnahmen", "Abdeckung ersetzen");
        record.SetFieldValue("Kosten", "1'234.50");
        record.SetFieldValue("Eigentuemer", "Gemeinde");
        record.SetFieldValue("Ausgefuehrt_durch", "Baumeister");
        record.SetFieldValue("Bemerkungen", "kontrolliert");
        record.SetFieldValue("Link", @"Schächte_Verteilt\00123\Protokoll 01.pdf");
        record.SetFieldValue("Abdeckung Stk.", "2");
        record.SetFieldValue(FieldKeys.LoadClass, "D400");
        record.SetFieldValue(FieldKeys.WorkflowStatus, "offen");
        record.SetFieldValue(FieldKeys.InspectionYear, "2026");
        project.SchaechteData.Add(record);

        using var output = new TempExcelFile();
        var result = new ExcelTemplateExportService().ExportSchaechteToTemplate(
            project,
            Template("Schächte.xlsx"),
            output.Path,
            ExcelVorlagenLayout.KopfZeile,
            ExcelVorlagenLayout.ErsteDatenZeile);

        Assert.True(result.Ok, $"{result.ErrorCode}: {result.ErrorMessage}");
        using var workbook = new XLWorkbook(output.Path);
        var worksheet = workbook.Worksheet("Schaechte");
        var columns = ReadColumns(worksheet);
        var row = ExcelVorlagenLayout.ErsteDatenZeile;

        AssertText(worksheet, row, columns, "NR.", "007");
        AssertText(worksheet, row, columns, "Funktion", "Kontrollschacht");
        AssertText(worksheet, row, columns, "Schachtnummer", "00123");
        AssertText(worksheet, row, columns, "Strasse", "Bahnhofstrasse");
        AssertText(worksheet, row, columns, "Primäre Schäden", "Korrosion");
        AssertText(worksheet, row, columns, "Zustandsklasse", "2");
        AssertText(worksheet, row, columns, "Ja/Nein", "Ja");
        AssertText(worksheet, row, columns, "Massnahmen", "Abdeckung ersetzen");
        AssertNumber(worksheet, row, columns, "Kosten", 1234.5d);
        AssertText(worksheet, row, columns, "Eigentümer", "Gemeinde");
        AssertText(worksheet, row, columns, "Ausgeführt durch", "Baumeister");
        AssertText(worksheet, row, columns, "Bemerkungen", "kontrolliert");
        AssertLink(worksheet, row, columns, "Link", @"Schächte_Verteilt\00123\Protokoll 01.pdf");
        AssertNumber(worksheet, row, columns, "Abdeckung Stk.", 2d);
        AssertText(worksheet, row, columns, "Belastungsklasse", "D400");
        AssertText(worksheet, row, columns, "Status\noffen/abgeschlossen", "offen");
        AssertText(worksheet, row, columns, "Ausführung\nDatum/Jahr", "2026");
    }

    [Fact]
    public void Exportlimit_entspricht_exakt_dem_Bereich_der_Vorlagenformeln()
        => Assert.Equal(ExcelVorlagenLayout.MaximaleDatenzeilen, ExcelTemplateExportLimit.MaxRecords);

    private static void Set(HaltungRecord record, string field, string value)
        => record.SetFieldValue(field, value, FieldSource.Manual, userEdited: false);

    private static string Template(string fileName)
        => Path.Combine(TestPaths.FindSolutionRoot(), "Export_Vorlage", fileName);

    private static Dictionary<string, int> ReadColumns(IXLWorksheet worksheet)
    {
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var cell in worksheet.Row(ExcelVorlagenLayout.KopfZeile).CellsUsed())
            result[NormalizeHeader(cell.GetString())] = cell.Address.ColumnNumber;
        return result;
    }

    private static string NormalizeHeader(string header)
        => header.Replace("\r\n", "\n", StringComparison.Ordinal).Trim();

    private static void AssertText(
        IXLWorksheet worksheet, int row, IReadOnlyDictionary<string, int> columns,
        string header, string expected)
    {
        var cell = worksheet.Cell(row, columns[NormalizeHeader(header)]);
        Assert.Equal(XLDataType.Text, cell.DataType);
        Assert.Equal(expected, cell.GetString());
    }

    private static void AssertNumber(
        IXLWorksheet worksheet, int row, IReadOnlyDictionary<string, int> columns,
        string header, double expected)
    {
        var cell = worksheet.Cell(row, columns[NormalizeHeader(header)]);
        Assert.Equal(XLDataType.Number, cell.DataType);
        Assert.Equal(expected, cell.GetDouble(), 3);
    }

    private static void AssertLink(
        IXLWorksheet worksheet, int row, IReadOnlyDictionary<string, int> columns,
        string header, string expectedTarget)
    {
        var cell = worksheet.Cell(row, columns[header]);
        Assert.Equal("öffnen", cell.GetString());
        Assert.True(cell.HasHyperlink);
        Assert.Equal(expectedTarget, cell.GetHyperlink().ExternalAddress?.OriginalString);
    }

    private sealed class TempExcelFile : IDisposable
    {
        public string Path { get; } =
            System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"excel-export-{Guid.NewGuid():N}.xlsx");

        public void Dispose()
        {
            try
            {
                if (File.Exists(Path))
                    File.Delete(Path);
            }
            catch
            {
                // Temp-Datei ist kein Kundenoriginal; ein Cleanup-Fehler darf den Test nicht verdecken.
            }
        }
    }
}
