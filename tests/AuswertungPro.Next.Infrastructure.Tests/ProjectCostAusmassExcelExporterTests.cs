using System.Collections.Generic;
using System.IO;
using System.Linq;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Costs;
using ClosedXML.Excel;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class ProjectCostAusmassExcelExporterTests
{
    [Fact]
    public void BuildLines_AggregatesMixedHoldingMeasuresAndSplitsProjectPositions()
    {
        var exporter = new ProjectCostAusmassExcelExporter();

        var lines = exporter.BuildLines(Store()).ToList();

        var installation = lines.Single(l => l.Label == "Installation UV-Anlage");
        Assert.Equal("Projekt", installation.AllocationScope);
        Assert.Equal(1m, installation.Qty);
        Assert.Equal(1000m, installation.Amount);
        Assert.Equal(["H1", "H2"], installation.Holdings);

        var gfk = lines.Single(l => l.Label == "Schlauchliner (GFK) DN 300");
        Assert.Equal("600.5", gfk.SubmissionPos);
        Assert.Equal(12m, gfk.Qty);
        Assert.Equal(1644m, gfk.Amount);

        var kurzliner = lines.Single(l => l.Label == "Kurzliner (Pointliner, Partliner) DN 150");
        Assert.Equal("500.1", kurzliner.SubmissionPos);
        Assert.Equal(2m, kurzliner.Qty);
    }

    [Fact]
    public void Export_WritesCostFileAndTenderFileWithoutPrices()
    {
        var exporter = new ProjectCostAusmassExcelExporter();
        var dir = Path.Combine(Path.GetTempPath(), "AuswertungPro_AusmassTests");
        Directory.CreateDirectory(dir);
        var costPath = Path.Combine(dir, "ausmass_mit_kosten.xlsx");
        var tenderPath = Path.Combine(dir, "ausschreibung_ohne_preise.xlsx");
        if (File.Exists(costPath)) File.Delete(costPath);
        if (File.Exists(tenderPath)) File.Delete(tenderPath);

        var costResult = exporter.Export(
            Store(),
            costPath,
            new ProjectCostAusmassExportOptions { IncludePrices = true, Project = SampleProject() });
        var tenderResult = exporter.Export(
            Store(),
            tenderPath,
            new ProjectCostAusmassExportOptions { IncludePrices = false, Project = SampleProject() });

        Assert.True(costResult.Ok, costResult.Error);
        Assert.True(tenderResult.Ok, tenderResult.Error);
        Assert.Equal(4, costResult.PositionCount);
        Assert.True(File.Exists(costPath));
        Assert.True(File.Exists(tenderPath));

        using var workbook = new XLWorkbook(tenderPath);
        var ws = workbook.Worksheet("Ausschreibung ohne Preise");
        var gfkRow = ws.RowsUsed().Single(r => r.Cell(3).GetString() == "Schlauchliner (GFK) DN 300");

        Assert.Equal("12", gfkRow.Cell(5).GetString());
        Assert.Equal("", gfkRow.Cell(6).GetString());
        Assert.Equal("", gfkRow.Cell(7).GetString());

        var holdingSheet = workbook.Worksheet("Haltungen");
        Assert.Equal("Kosten", holdingSheet.Cell(11, 14).GetString());
        Assert.Equal("H1", holdingSheet.Cell(12, 2).GetString());
        Assert.Equal("", holdingSheet.Cell(12, 14).GetString());
        Assert.Equal("3", holdingSheet.Cell(12, 20).GetString());
    }

    private static Project SampleProject()
    {
        var project = new Project { Name = "Auswertung Test" };
        project.Metadata["Gemeinde"] = "Buerglen";
        project.Metadata["Zone"] = "5.01";
        project.Data.Add(HoldingRecord(
            nr: "1",
            holding: "H1",
            dn: "300",
            length: "12",
            measures: "-UV-Inliner mit LEM / -3 Stk. Anschluesse verpressen",
            inlinerCount: "1",
            inlinerMeters: "12",
            connections: "3"));
        project.Data.Add(HoldingRecord(
            nr: "2",
            holding: "H2",
            dn: "150",
            length: "5",
            measures: "-2 Stk. Kurzliner",
            shortliners: "2"));
        return project;
    }

    private static HaltungRecord HoldingRecord(
        string nr,
        string holding,
        string dn,
        string length,
        string measures,
        string inlinerCount = "",
        string inlinerMeters = "",
        string connections = "",
        string shortliners = "")
    {
        var record = new HaltungRecord();
        record.SetFieldValue("NR", nr, FieldSource.Manual, userEdited: false);
        record.SetFieldValue("Haltungsname", holding, FieldSource.Manual, userEdited: false);
        record.SetFieldValue("Strasse", "Teststrasse", FieldSource.Manual, userEdited: false);
        record.SetFieldValue("Rohrmaterial", "Beton", FieldSource.Manual, userEdited: false);
        record.SetFieldValue("DN_mm", dn, FieldSource.Manual, userEdited: false);
        record.SetFieldValue("Nutzungsart", "Schmutzwasser", FieldSource.Manual, userEdited: false);
        record.SetFieldValue("Haltungslaenge_m", length, FieldSource.Manual, userEdited: false);
        record.SetFieldValue("Inspektionsrichtung", "in Fliessrichtung", FieldSource.Manual, userEdited: false);
        record.SetFieldValue("Sanieren_JaNein", "Ja", FieldSource.Manual, userEdited: false);
        record.SetFieldValue("Empfohlene_Sanierungsmassnahmen", measures, FieldSource.Manual, userEdited: false);
        record.SetFieldValue("Kosten", "9999", FieldSource.Manual, userEdited: false);
        record.SetFieldValue("Renovierung_Inliner_Stk", inlinerCount, FieldSource.Manual, userEdited: false);
        record.SetFieldValue("Renovierung_Inliner_m", inlinerMeters, FieldSource.Manual, userEdited: false);
        record.SetFieldValue("Anschluesse_verpressen", connections, FieldSource.Manual, userEdited: false);
        record.SetFieldValue("Reparatur_Kurzliner", shortliners, FieldSource.Manual, userEdited: false);
        return record;
    }

    private static ProjectCostStore Store()
        => new()
        {
            ByHolding = new Dictionary<string, HoldingCost>
            {
                ["H1"] = new()
                {
                    Holding = "H1",
                    MwstRate = 0.081m,
                    Total = 4744m,
                    Measures =
                    [
                        new()
                        {
                            MeasureId = "SCHLAUCHLINER_GFK",
                            MeasureName = "Schlauchliner (GFK)",
                            Dn = 300,
                            Lines =
                            [
                                Line("Installation", "INSTALL_UV_ANLAGE", "Installation UV-Anlage", "pl", 1m, 1000m, "100.1"),
                                Line("Hauptarbeit", "SCHLAUCHLINER_GFK", "Schlauchliner (GFK)", "m", 12m, 137m, "600.5"),
                                Line("Hauptarbeit", "ANSCHLUSS_EINBINDEN", "Anschlusseinbinden (verpressen)", "stk", 3m, 700m, "600.6")
                            ]
                        }
                    ]
                },
                ["H2"] = new()
                {
                    Holding = "H2",
                    MwstRate = 0.081m,
                    Total = 2700m,
                    Measures =
                    [
                        new()
                        {
                            MeasureId = "KURZLINER_PARTLINER",
                            MeasureName = "Kurzliner",
                            Dn = 150,
                            Lines =
                            [
                                Line("Installation", "INSTALL_UV_ANLAGE", "Installation UV-Anlage", "pl", 1m, 1000m, "100.1"),
                                Line("Hauptarbeit", "KURZLINER_PARTLINER", "Kurzliner (Pointliner, Partliner)", "stk", 2m, 850m, "500.1")
                            ]
                        }
                    ]
                }
            }
        };

    private static CostLine Line(string group, string key, string text, string unit, decimal qty, decimal unitPrice, string submissionPos)
        => new()
        {
            Group = group,
            ItemKey = key,
            Text = text,
            Unit = unit,
            Qty = qty,
            UnitPrice = unitPrice,
            Selected = true,
            SubmissionPos = submissionPos
        };
}
