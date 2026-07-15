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

    private static IXLRangeRow PositionRow(IXLWorksheet ws, string npk)
    {
        var row = ws.RangeUsed()?.RowsUsed()
            .FirstOrDefault(r => r.Cell(ColNpk).GetString().Contains(npk));
        Assert.NotNull(row);
        return row!;
    }
}
