using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using AuswertungPro.Next.Domain.Models;
using ClosedXML.Excel;

namespace AuswertungPro.Next.Infrastructure.Costs;

/// <summary>
/// Baut aus den aggregierten NPK-Positionen ein formatiertes Excel-Leistungsverzeichnis
/// mit zwei Reitern:
///  - "Zum Ausfüllen": Einheitspreis-Spalte leer (gelb markiert), Total/Zwischentotale/Gesamt
///    als echte Excel-Formeln — die ausschreibende Firma trägt nur ihre Preise ein.
///  - "Kalkulation (intern)": dieselbe Struktur mit den eigenen Schätzpreisen.
/// Gruppiert nach NPK-Kapitel mit Zwischentotalen, Gesamttotal, MwSt und Total inkl.
/// </summary>
public static class NpkLeistungsverzeichnisExcelExporter
{
    private const int ColNpk = 1;
    private const int ColPosition = 2;
    private const int ColDn = 3;
    private const int ColMenge = 4;
    private const int ColEinheit = 5;
    private const int ColEp = 6;
    private const int ColTotal = 7;
    private const int ColHaltungen = 8;

    private const string MoneyFormat = "#,##0.00";

    private static readonly XLColor HeaderFill = XLColor.FromHtml("#1E293B");
    private static readonly XLColor ChapterFill = XLColor.FromHtml("#E2E8F0");
    private static readonly XLColor FillMeColor = XLColor.FromHtml("#FEF9C3"); // hellgelb: hier ausfuellen
    private static readonly XLColor TotalFill = XLColor.FromHtml("#DBEAFE");
    private static readonly XLColor InternWarn = XLColor.FromHtml("#B91C1C");

    public static byte[] BuildWorkbook(
        IReadOnlyList<AggregatedPosition> positions,
        string currency = "CHF",
        decimal vatRate = 0.081m,
        string projectName = "",
        decimal excludedPauschaleTotal = 0m,
        int excludedPauschaleCount = 0)
    {
        var cur = string.IsNullOrWhiteSpace(currency) ? "CHF" : currency.Trim();
        var list = positions ?? new List<AggregatedPosition>();

        using var wb = new XLWorkbook();
        WriteSheet(wb.AddWorksheet("Zum Ausfüllen"), list, cur, vatRate, projectName, excludedPauschaleTotal, excludedPauschaleCount, withPrices: false);
        WriteSheet(wb.AddWorksheet("Kalkulation (intern)"), list, cur, vatRate, projectName, excludedPauschaleTotal, excludedPauschaleCount, withPrices: true);

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static void WriteSheet(
        IXLWorksheet ws,
        IReadOnlyList<AggregatedPosition> positions,
        string cur,
        decimal vatRate,
        string projectName,
        decimal excludedPauschaleTotal,
        int excludedPauschaleCount,
        bool withPrices)
    {
        // ── Kopfbereich ─────────────────────────────────────────────
        ws.Cell(1, ColNpk).Value = "NPK-Leistungsverzeichnis";
        ws.Range(1, ColNpk, 1, ColHaltungen).Merge();
        ws.Cell(1, ColNpk).Style.Font.Bold = true;
        ws.Cell(1, ColNpk).Style.Font.FontSize = 15;

        ws.Cell(2, ColNpk).Value = string.IsNullOrWhiteSpace(projectName) ? "Projekt:" : $"Projekt: {projectName}";
        ws.Range(2, ColNpk, 2, ColHaltungen).Merge();
        ws.Cell(2, ColNpk).Style.Font.Bold = true;

        ws.Cell(3, ColNpk).Value = withPrices
            ? "Kalkulation — INTERN, nicht an Firmen versenden"
            : "Firma: _______________________________          Datum: ______________";
        ws.Range(3, ColNpk, 3, ColHaltungen).Merge();
        if (withPrices)
            ws.Cell(3, ColNpk).Style.Font.FontColor = InternWarn;

        // ── Spaltenüberschriften ────────────────────────────────────
        const int headerRow = 5;
        string[] headers = { "NPK", "Position", "DN", "Menge", "Einheit", $"EP {cur}", $"Total {cur}", "Haltungen" };
        for (var c = 0; c < headers.Length; c++)
        {
            var cell = ws.Cell(headerRow, c + 1);
            cell.Value = headers[c];
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = HeaderFill;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        var r = headerRow + 1;
        var subtotalRows = new List<int>();

        foreach (var chapter in positions
                     .GroupBy(p => p.Chapter ?? "")
                     .OrderBy(g => ProjectPositionAggregator.ChapterOrder(g.Key)))
        {
            var chapterTitle = NpkLeistungsverzeichnisExporter.ChapterTitle(chapter.Key);

            // Kapitel-Titelzeile
            ws.Cell(r, ColPosition).Value = chapterTitle;
            ws.Range(r, ColNpk, r, ColHaltungen).Merge();
            ws.Cell(r, ColPosition).Style.Font.Bold = true;
            ws.Range(r, ColNpk, r, ColHaltungen).Style.Fill.BackgroundColor = ChapterFill;
            r++;

            var firstDataRow = r;
            foreach (var p in chapter)
            {
                ws.Cell(r, ColNpk).Style.NumberFormat.Format = "@"; // Text, damit "612.113" nicht zur Zahl wird
                ws.Cell(r, ColNpk).Value = p.NpkCode ?? "";
                ws.Cell(r, ColPosition).Value = AppendPriceHint(p.Text, p.PriceHint);
                if (p.Dn is int dn)
                    ws.Cell(r, ColDn).Value = dn;
                ws.Cell(r, ColMenge).Value = p.TotalQty;
                ws.Cell(r, ColMenge).Style.NumberFormat.Format = "#,##0.###";
                ws.Cell(r, ColEinheit).Value = p.Unit ?? "";

                var epCell = ws.Cell(r, ColEp);
                var totalCell = ws.Cell(r, ColTotal);
                epCell.Style.NumberFormat.Format = MoneyFormat;
                totalCell.Style.NumberFormat.Format = MoneyFormat;

                if (withPrices && !p.IsVariablePrice && p.UnitPrice is decimal ep)
                {
                    epCell.Value = ep;
                    totalCell.FormulaA1 = LineTotalFormula(r);
                }
                else if (withPrices)
                {
                    // Variabler Preis (mehrere DN/Preise): kein einzelner EP — unser aggregiertes Total als Wert.
                    totalCell.Value = Round2(p.TotalNet);
                }
                else
                {
                    // Ausfüll-Reiter: EP leer und gelb, Total als Formel (rechnet, sobald die Firma tippt).
                    epCell.Style.Fill.BackgroundColor = FillMeColor;
                    totalCell.FormulaA1 = LineTotalFormula(r);
                }

                if (p.HoldingCount > 0)
                    ws.Cell(r, ColHaltungen).Value = p.HoldingCount;
                r++;
            }
            var lastDataRow = r - 1;

            // Zwischentotal
            ws.Cell(r, ColPosition).Value = $"Zwischentotal {chapterTitle}";
            ws.Cell(r, ColPosition).Style.Font.Bold = true;
            var subCell = ws.Cell(r, ColTotal);
            subCell.Style.Font.Bold = true;
            subCell.Style.NumberFormat.Format = MoneyFormat;
            if (lastDataRow >= firstDataRow)
                subCell.FormulaA1 = $"=SUM({Col(ColTotal)}{firstDataRow}:{Col(ColTotal)}{lastDataRow})";
            else
                subCell.Value = 0;
            subtotalRows.Add(r);
            r++;
        }

        r++; // Leerzeile

        // ── Gesamttotal ─────────────────────────────────────────────
        ws.Cell(r, ColPosition).Value = "TOTAL (exkl. MwSt.)";
        ws.Cell(r, ColPosition).Style.Font.Bold = true;
        var grandCell = ws.Cell(r, ColTotal);
        grandCell.Style.Font.Bold = true;
        grandCell.Style.NumberFormat.Format = MoneyFormat;
        grandCell.Style.Fill.BackgroundColor = TotalFill;
        grandCell.FormulaA1 = subtotalRows.Count > 0
            ? "=" + string.Join("+", subtotalRows.Select(sr => $"{Col(ColTotal)}{sr}"))
            : "=0";
        var grandRow = r;
        r++;

        if (excludedPauschaleTotal > 0m)
        {
            var countText = excludedPauschaleCount > 0 ? $" ({excludedPauschaleCount} Haltung(en))" : "";
            ws.Cell(r, ColPosition).Value = $"Nicht enthaltene Pauschalkosten{countText}";
            ws.Cell(r, ColTotal).Value = Round2(excludedPauschaleTotal);
            ws.Cell(r, ColTotal).Style.NumberFormat.Format = MoneyFormat;
            r++;
        }

        // ── MwSt + Total inkl. ──────────────────────────────────────
        ws.Cell(r, ColPosition).Value = $"MwSt ({(vatRate * 100m).ToString("0.#", CultureInfo.InvariantCulture)} %)";
        var mwstCell = ws.Cell(r, ColTotal);
        mwstCell.Style.NumberFormat.Format = MoneyFormat;
        mwstCell.FormulaA1 = $"={Col(ColTotal)}{grandRow}*{vatRate.ToString(CultureInfo.InvariantCulture)}";
        var mwstRow = r;
        r++;

        ws.Cell(r, ColPosition).Value = "TOTAL (inkl. MwSt.)";
        ws.Cell(r, ColPosition).Style.Font.Bold = true;
        var inclCell = ws.Cell(r, ColTotal);
        inclCell.Style.Font.Bold = true;
        inclCell.Style.NumberFormat.Format = MoneyFormat;
        inclCell.Style.Fill.BackgroundColor = TotalFill;
        inclCell.FormulaA1 = $"={Col(ColTotal)}{grandRow}+{Col(ColTotal)}{mwstRow}";

        // ── Spaltenbreiten + Fixierung ──────────────────────────────
        ws.Column(ColNpk).Width = 12;
        ws.Column(ColPosition).Width = 46;
        ws.Column(ColDn).Width = 6;
        ws.Column(ColMenge).Width = 10;
        ws.Column(ColEinheit).Width = 9;
        ws.Column(ColEp).Width = 12;
        ws.Column(ColTotal).Width = 14;
        ws.Column(ColHaltungen).Width = 10;
        ws.SheetView.FreezeRows(headerRow);
    }

    private static string LineTotalFormula(int row) => $"={Col(ColMenge)}{row}*{Col(ColEp)}{row}";

    private static string Col(int col) => XLHelper.GetColumnLetterFromNumber(col);

    private static decimal Round2(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static string AppendPriceHint(string? text, string? priceHint)
    {
        var t = (text ?? "").Trim();
        var h = (priceHint ?? "").Trim();
        if (h.Length == 0) return t;
        if (t.Length == 0) return h;
        return t.IndexOf(h, StringComparison.OrdinalIgnoreCase) >= 0 ? t : $"{t} ({h})";
    }
}
