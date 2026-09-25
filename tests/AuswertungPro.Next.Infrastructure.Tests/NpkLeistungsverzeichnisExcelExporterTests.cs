using System.IO;
using System.Linq;
using AuswertungPro.Next.Application.Costs;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Costs;
using ClosedXML.Excel;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class NpkLeistungsverzeichnisExcelExporterTests
{
    // NPK=A, NPK D/16=B, Position=C, DN=D, Menge=E, Einheit=F, EP=G, Total=H, Haltungen=I
    private const int ColNpk = 1;
    private const int ColMenge = 5;
    private const int ColEp = 7;
    private const int ColTotal = 8;

    private static AggregatedPosition Fixed(string npk, decimal qty, decimal ep, decimal net)
        => new(npk, "600", "key-" + npk, "Position " + npk, "m", 250, qty, net, 2, false, ep, "");

    private static XLWorkbook Open(byte[] bytes) => new(new MemoryStream(bytes));

    [Fact]
    public void Instanz_erzeugt_dieselbe_geschuetzte_Arbeitsmappe()
    {
        INpkLeistungsverzeichnisExcelExporter exporter =
            new NpkLeistungsverzeichnisExcelExportService();

        var bytes = exporter.BuildWorkbook(
            new[] { Fixed("612.113", 10m, 200m, 2000m) });

        using var workbook = Open(bytes);
        Assert.Contains(
            workbook.Worksheets,
            sheet => sheet.Name.StartsWith("Zum Ausf", System.StringComparison.Ordinal));
        Assert.Contains("Kalkulation (intern)", workbook.Worksheets.Select(sheet => sheet.Name));
    }

    [Fact]
    public void BuildWorkbook_hat_reiter_zum_ausfuellen_und_intern()
    {
        var bytes = NpkLeistungsverzeichnisExcelExporter.BuildWorkbook(
            new[] { Fixed("612.113", 10m, 200m, 2000m) });

        using var wb = Open(bytes);
        var names = wb.Worksheets.Select(w => w.Name).ToList();

        Assert.Contains("Zum Ausfüllen", names);
        Assert.Contains("Kalkulation (intern)", names);
    }

    [Fact]
    public void Ausfuell_reiter_laesst_ep_leer_und_total_ist_formel()
    {
        var bytes = NpkLeistungsverzeichnisExcelExporter.BuildWorkbook(
            new[] { Fixed("612.113", 10m, 200m, 2000m) });

        using var wb = Open(bytes);
        var ws = wb.Worksheet("Zum Ausfüllen");
        var row = PositionRow(ws, "612.113");

        Assert.True(row.Cell(ColEp).IsEmpty());
        Assert.True(row.Cell(ColTotal).HasFormula);
    }

    [Fact]
    public void Intern_reiter_traegt_den_einheitspreis_ein()
    {
        var bytes = NpkLeistungsverzeichnisExcelExporter.BuildWorkbook(
            new[] { Fixed("612.113", 10m, 200m, 2000m) });

        using var wb = Open(bytes);
        var ws = wb.Worksheet("Kalkulation (intern)");
        var row = PositionRow(ws, "612.113");

        Assert.Equal(200d, row.Cell(ColEp).GetDouble(), 3);
        Assert.Equal(10d, row.Cell(ColMenge).GetDouble(), 3);
    }

    [Fact]
    public void Beide_reiter_haben_eine_totalzeile()
    {
        var bytes = NpkLeistungsverzeichnisExcelExporter.BuildWorkbook(
            new[] { Fixed("612.113", 10m, 200m, 2000m) });

        using var wb = Open(bytes);
        foreach (var sheet in new[] { "Zum Ausfüllen", "Kalkulation (intern)" })
        {
            var ws = wb.Worksheet(sheet);
            var hasTotal = ws.RowsUsed().Any(r =>
                r.Cell(3).GetString().Contains("TOTAL", System.StringComparison.OrdinalIgnoreCase));
            Assert.True(hasTotal, $"Reiter '{sheet}' hat keine TOTAL-Zeile.");
        }
    }

    [Fact]
    public void Npk_nummer_mit_endnuller_bleibt_text_und_wird_keine_zahl()
    {
        // Regression: "612.110" darf von Excel nicht als Zahl 612.11 interpretiert werden —
        // die NPK-Position waere im Leistungsverzeichnis sonst verfaelscht (612.110 <> 612.11).
        var bytes = NpkLeistungsverzeichnisExcelExporter.BuildWorkbook(
            new[] { Fixed("612.110", 10m, 200m, 2000m) });

        using var wb = Open(bytes);
        foreach (var sheet in new[] { "Zum Ausfüllen", "Kalkulation (intern)" })
        {
            var ws = wb.Worksheet(sheet);
            // PositionRow findet die Zeile nur, wenn der Zelltext unverstuemmelt vorliegt.
            var cell = PositionRow(ws, "612.110").Cell(ColNpk);

            Assert.Equal(XLDataType.Text, cell.DataType);
            Assert.Equal("612.110", cell.GetString());
        }
    }

    [Fact]
    public void Beispiel_LV_behaelt_Positionen_Formeln_Summen_und_Preistrennung()
    {
        // Kuenftige Aenderungen muessen den fachlichen Inhalt beider Reiter erhalten.
        // Ein XLSX-Dateihash waere wegen ZIP-Metadaten kein verlaesslicher Vergleich.
        var positions = new[]
        {
            Fixed("612.110", 2.5m, 100m, 250m) with { Chapter = "600", NpkCodeD16 = "612.110" },
            Fixed("711.100", 3m, 40m, 120m) with { Chapter = "700", Dn = null }
        };
        var bytes = NpkLeistungsverzeichnisExcelExporter.BuildWorkbook(
            positions, projectName: "Musterprojekt", excludedPauschaleTotal: 50m,
            excludedPauschaleCount: 1);

        using var wb = Open(bytes);
        foreach (var sheetName in new[] { "Zum Ausfüllen", "Kalkulation (intern)" })
        {
            var ws = wb.Worksheet(sheetName);
            Assert.Contains("Musterprojekt", ws.Cell(4, 1).GetString());
            Assert.Equal("EP CHF", ws.Cell(7, ColEp).GetString());
            Assert.Equal("Total CHF", ws.Cell(7, ColTotal).GetString());

            var first = PositionRow(ws, "612.110");
            var second = PositionRow(ws, "711.100");
            Assert.Equal(XLDataType.Text, first.Cell(ColNpk).DataType);
            Assert.Equal("612.110", first.Cell(2).GetString());
            Assert.Equal(2.5d, first.Cell(ColMenge).GetDouble());
            Assert.Equal(3d, second.Cell(ColMenge).GetDouble());
            Assert.Equal("#,##0.00", first.Cell(ColTotal).Style.NumberFormat.Format);
            Assert.Equal($"E{first.RowNumber()}*G{first.RowNumber()}",
                first.Cell(ColTotal).FormulaA1.TrimStart('='));
            Assert.Equal($"E{second.RowNumber()}*G{second.RowNumber()}",
                second.Cell(ColTotal).FormulaA1.TrimStart('='));

            var subtotalRows = ws.RowsUsed().Where(r =>
                r.Cell(3).GetString().StartsWith("Zwischentotal", System.StringComparison.Ordinal))
                .ToArray();
            Assert.Equal(2, subtotalRows.Length);
            Assert.All(subtotalRows, r => Assert.True(r.Cell(ColTotal).HasFormula));

            var grand = ws.RowsUsed().Single(r => r.Cell(3).GetString() == "TOTAL (exkl. MwSt.)");
            var vat = ws.RowsUsed().Single(r => r.Cell(3).GetString().StartsWith("MwSt", System.StringComparison.Ordinal));
            var includingVat = ws.RowsUsed().Single(r => r.Cell(3).GetString() == "TOTAL (inkl. MwSt.)");
            var excluded = ws.RowsUsed().Single(r => r.Cell(3).GetString().StartsWith("Nicht enthaltene Pauschalkosten", System.StringComparison.Ordinal));
            Assert.Contains("1 Haltung(en)", excluded.Cell(3).GetString());
            Assert.Equal(50d, excluded.Cell(ColTotal).GetDouble());
            Assert.Equal($"H{grand.RowNumber()}*0.081", vat.Cell(ColTotal).FormulaA1.TrimStart('='));
            Assert.Equal($"H{grand.RowNumber()}+H{vat.RowNumber()}", includingVat.Cell(ColTotal).FormulaA1.TrimStart('='));
            Assert.DoesNotContain($"H{excluded.RowNumber()}", grand.Cell(ColTotal).FormulaA1);

            if (sheetName == "Zum Ausfüllen")
            {
                Assert.True(first.Cell(ColEp).IsEmpty());
                Assert.True(second.Cell(ColEp).IsEmpty());
                Assert.Equal(XLColor.FromHtml("#FEF9C3"), first.Cell(ColEp).Style.Fill.BackgroundColor);
            }
            else
            {
                Assert.Equal(100d, first.Cell(ColEp).GetDouble());
                Assert.Equal(40d, second.Cell(ColEp).GetDouble());
                wb.RecalculateAllFormulas();
                Assert.Equal(250d, subtotalRows[0].Cell(ColTotal).GetDouble(), 2);
                Assert.Equal(120d, subtotalRows[1].Cell(ColTotal).GetDouble(), 2);
                Assert.Equal(370d, grand.Cell(ColTotal).GetDouble(), 2);
                Assert.Equal(29.97d, vat.Cell(ColTotal).GetDouble(), 2);
                Assert.Equal(399.97d, includingVat.Cell(ColTotal).GetDouble(), 2);
            }
        }
    }

    [Fact]
    public void Variabler_Preis_bleibt_nur_in_der_internen_Kalkulation()
    {
        var variable = Fixed("612.120", 5m, 100m, 789.45m) with
        {
            IsVariablePrice = true,
            UnitPrice = null
        };
        var bytes = NpkLeistungsverzeichnisExcelExporter.BuildWorkbook(
            new[] { variable }, excludedPauschaleTotal: 22.20m);

        using var wb = Open(bytes);
        var offer = wb.Worksheet("Zum Ausfüllen");
        var internalSheet = wb.Worksheet("Kalkulation (intern)");
        var offerPosition = PositionRow(offer, "612.120");
        var internalPosition = PositionRow(internalSheet, "612.120");

        Assert.Equal(5d, offerPosition.Cell(ColMenge).GetDouble());
        Assert.Equal("m", offerPosition.Cell(6).GetString());
        Assert.Equal("Position 612.120", offerPosition.Cell(3).GetString());
        Assert.True(offerPosition.Cell(ColEp).IsEmpty());
        Assert.True(offerPosition.Cell(ColTotal).HasFormula);
        Assert.True(internalPosition.Cell(ColEp).IsEmpty());
        Assert.False(internalPosition.Cell(ColTotal).HasFormula);
        Assert.Equal(789.45d, internalPosition.Cell(ColTotal).GetDouble(), 2);

        wb.RecalculateAllFormulas();
        var offerTotal = offer.RowsUsed().Single(r => r.Cell(3).GetString() == "TOTAL (exkl. MwSt.)");
        var internalTotal = internalSheet.RowsUsed().Single(r => r.Cell(3).GetString() == "TOTAL (exkl. MwSt.)");
        Assert.Equal(0d, offerTotal.Cell(ColTotal).GetDouble(), 2);
        Assert.Equal(789.45d, internalTotal.Cell(ColTotal).GetDouble(), 2);
        Assert.Contains(offer.RowsUsed(), r =>
            r.Cell(3).GetString().StartsWith("Nicht enthaltene Pauschalkosten", System.StringComparison.Ordinal)
            && r.Cell(ColTotal).GetDouble() == 22.20d);
        Assert.Contains(internalSheet.RowsUsed(), r =>
            r.Cell(3).GetString().StartsWith("Nicht enthaltene Pauschalkosten", System.StringComparison.Ordinal)
            && r.Cell(ColTotal).GetDouble() == 22.20d);
    }

    private static IXLRangeRow PositionRow(IXLWorksheet ws, string npk)
    {
        var row = ws.RangeUsed()?.RowsUsed()
            .FirstOrDefault(r => r.Cell(ColNpk).GetString().Contains(npk));
        Assert.NotNull(row);
        return row!;
    }
}
