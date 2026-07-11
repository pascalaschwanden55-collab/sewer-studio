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
///
/// Optik im Abwasser-Uri-Stil (gleiche Bildsprache wie die AWU-Haltungsprotokolle):
/// Logo + Absenderblock im Kopf, dezente Grau-Blau-Akzente (#7A8A94), A4-Druckformat
/// hochkant mit wiederholter Kopfzeile und Fusszeile "Seite X von Y".
/// </summary>
public static class NpkLeistungsverzeichnisExcelExporter
{
    private const int ColNpk = 1;
    // D/16-Praxisnummer (heutige Ausgabe) direkt neben der Revisions-Nummer,
    // damit das LV mit echten Unternehmer-Offerten vergleichbar ist.
    private const int ColNpkD16 = 2;
    private const int ColPosition = 3;
    private const int ColDn = 4;
    private const int ColMenge = 5;
    private const int ColEinheit = 6;
    private const int ColEp = 7;
    private const int ColTotal = 8;
    private const int ColHaltungen = 9;

    private const string MoneyFormat = "#,##0.00";

    // Abwasser-Uri-Bildsprache (identisch zu den AWU-Protokoll-PDFs, ProtocolPdfExporter):
    private static readonly XLColor AwuAccent = XLColor.FromHtml("#7A8A94");     // Akzent grau-blau
    private static readonly XLColor AwuAccentLight = XLColor.FromHtml("#F2F4F5"); // Akzent hell
    private static readonly XLColor AwuText = XLColor.FromHtml("#1F2937");        // Haupttext
    private static readonly XLColor AwuTitle = XLColor.FromHtml("#111827");       // Titel
    private static readonly XLColor AwuMuted = XLColor.FromHtml("#4A5568");       // Absender/Nebentext
    private static readonly XLColor AwuBorder = XLColor.FromHtml("#D1D5DB");      // Linien
    private static readonly XLColor AwuRowBorder = XLColor.FromHtml("#E5E7EB");   // Zeilenlinien
    private static readonly XLColor AwuZebra = XLColor.FromHtml("#FAFBFC");       // Zebra-Zeilen
    private static readonly XLColor FillMeColor = XLColor.FromHtml("#FEF9C3");    // hellgelb: hier ausfuellen
    private static readonly XLColor InternWarn = XLColor.FromHtml("#B91C1C");

    // Gleicher Absenderblock wie die AWU-Haltungsprotokolle (HaltungsprotokollPdfOptions).
    private const string SenderLine1 = "Abwasser Uri — Zentrale Dienste — Giessenstrasse 46 — 6460 Altdorf";
    private const string SenderLine2 = "info@abwasser-uri.ch — T 041 875 00 90";

    public static byte[] BuildWorkbook(
        IReadOnlyList<AggregatedPosition> positions,
        string currency = "CHF",
        decimal vatRate = 0.081m,
        string projectName = "",
        decimal excludedPauschaleTotal = 0m,
        int excludedPauschaleCount = 0,
        string? logoPathAbs = null)
    {
        var cur = string.IsNullOrWhiteSpace(currency) ? "CHF" : currency.Trim();
        var list = positions ?? new List<AggregatedPosition>();
        var logo = ResolveLogoPath(logoPathAbs);

        using var wb = new XLWorkbook();
        WriteSheet(wb.AddWorksheet("Zum Ausfüllen"), list, cur, vatRate, projectName, excludedPauschaleTotal, excludedPauschaleCount, withPrices: false, logo);
        WriteSheet(wb.AddWorksheet("Kalkulation (intern)"), list, cur, vatRate, projectName, excludedPauschaleTotal, excludedPauschaleCount, withPrices: true, logo);

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
        bool withPrices,
        string? logoPath)
    {
        // ── Kopfbereich im AWU-Stil: Logo links, Absender rechts, Titel, Trennlinie ──
        // Zeilen 1-2: Logo (ueber beide Zeilen) + Absenderblock rechts.
        ws.Row(1).Height = 24;
        ws.Row(2).Height = 14;
        if (logoPath is not null)
        {
            try
            {
                var pic = ws.AddPicture(logoPath);
                var targetH = 44; // Pixel, passt in Zeile 1-2
                pic.WithSize(Math.Max(1, (int)(pic.Width * targetH / (double)Math.Max(1, pic.Height))), targetH);
                pic.MoveTo(ws.Cell(1, ColNpk), 2, 2);
            }
            catch { /* Logo optional — Export darf nie am Bild scheitern */ }
        }

        ws.Cell(1, ColDn).Value = SenderLine1;
        ws.Range(1, ColDn, 1, ColHaltungen).Merge();
        ws.Cell(1, ColDn).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        ws.Cell(1, ColDn).Style.Font.FontSize = 8;
        ws.Cell(1, ColDn).Style.Font.FontColor = AwuMuted;

        ws.Cell(2, ColDn).Value = SenderLine2;
        ws.Range(2, ColDn, 2, ColHaltungen).Merge();
        ws.Cell(2, ColDn).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        ws.Cell(2, ColDn).Style.Font.FontSize = 8;
        ws.Cell(2, ColDn).Style.Font.FontColor = AwuMuted;

        // Duenne Trennlinie unter dem Absender (wie im Protokoll-Kopf).
        ws.Range(2, ColNpk, 2, ColHaltungen).Style.Border.BottomBorder = XLBorderStyleValues.Thin;
        ws.Range(2, ColNpk, 2, ColHaltungen).Style.Border.BottomBorderColor = AwuBorder;

        // Zeile 3: Titel; Zeile 4: Projekt + Datum.
        ws.Row(3).Height = 22;
        ws.Cell(3, ColNpk).Value = "NPK-135-Leistungsverzeichnis";
        ws.Range(3, ColNpk, 3, ColHaltungen).Merge();
        ws.Cell(3, ColNpk).Style.Font.Bold = true;
        ws.Cell(3, ColNpk).Style.Font.FontSize = 15;
        ws.Cell(3, ColNpk).Style.Font.FontColor = AwuTitle;

        ws.Cell(4, ColNpk).Value = string.IsNullOrWhiteSpace(projectName)
            ? "Sanierungsmassnahmen Kanalisation"
            : $"Sanierungsmassnahmen Kanalisation — Projekt: {projectName}";
        ws.Range(4, ColNpk, 4, ColEinheit).Merge();
        ws.Cell(4, ColNpk).Style.Font.FontColor = AwuMuted;
        ws.Cell(4, ColEp).Value = $"Erstellt: {DateTime.Now:dd.MM.yyyy}";
        ws.Range(4, ColEp, 4, ColHaltungen).Merge();
        ws.Cell(4, ColEp).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        ws.Cell(4, ColEp).Style.Font.FontColor = AwuMuted;
        ws.Cell(4, ColEp).Style.Font.FontSize = 9;

        // Zeile 5: Firma-Zeile (Ausfuellen) bzw. INTERN-Warnung.
        ws.Cell(5, ColNpk).Value = withPrices
            ? "Kalkulation — INTERN, nicht an Firmen versenden"
            : "Firma: _______________________________          Datum: ______________";
        ws.Range(5, ColNpk, 5, ColHaltungen).Merge();
        if (withPrices)
        {
            ws.Cell(5, ColNpk).Style.Font.FontColor = InternWarn;
            ws.Cell(5, ColNpk).Style.Font.Bold = true;
        }

        // ── Spaltenüberschriften (AWU-Akzentbalken) ────────────────
        const int headerRow = 7;
        string[] headers = { "NPK", "NPK D/16", "Position", "DN", "Menge", "Einheit", $"EP {cur}", $"Total {cur}", "Haltungen" };
        for (var c = 0; c < headers.Length; c++)
        {
            var cell = ws.Cell(headerRow, c + 1);
            cell.Value = headers[c];
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = AwuAccent;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }
        ws.Row(headerRow).Height = 18;

        var r = headerRow + 1;
        var subtotalRows = new List<int>();

        foreach (var chapter in positions
                     .GroupBy(p => p.Chapter ?? "")
                     .OrderBy(g => ProjectPositionAggregator.ChapterOrder(g.Key)))
        {
            var chapterTitle = NpkLeistungsverzeichnisExporter.ChapterTitle(chapter.Key);

            // Kapitel-Titelzeile: heller AWU-Balken mit Akzent-Unterkante.
            ws.Cell(r, ColPosition).Value = chapterTitle;
            ws.Range(r, ColNpk, r, ColHaltungen).Merge();
            ws.Cell(r, ColPosition).Style.Font.Bold = true;
            ws.Cell(r, ColPosition).Style.Font.FontColor = AwuTitle;
            ws.Range(r, ColNpk, r, ColHaltungen).Style.Fill.BackgroundColor = AwuAccentLight;
            ws.Range(r, ColNpk, r, ColHaltungen).Style.Border.BottomBorder = XLBorderStyleValues.Thin;
            ws.Range(r, ColNpk, r, ColHaltungen).Style.Border.BottomBorderColor = AwuAccent;
            r++;

            var firstDataRow = r;
            var zebra = false;
            foreach (var p in chapter)
            {
                var rowRange = ws.Range(r, ColNpk, r, ColHaltungen);
                if (zebra)
                    rowRange.Style.Fill.BackgroundColor = AwuZebra;
                rowRange.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
                rowRange.Style.Border.BottomBorderColor = AwuRowBorder;
                rowRange.Style.Font.FontColor = AwuText;
                zebra = !zebra;

                ws.Cell(r, ColNpk).Style.NumberFormat.Format = "@"; // Text, damit "612.113" nicht zur Zahl wird
                ws.Cell(r, ColNpk).Value = p.NpkCode ?? "";
                ws.Cell(r, ColNpkD16).Style.NumberFormat.Format = "@";
                ws.Cell(r, ColNpkD16).Value = p.NpkCodeD16 ?? "";
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

            // Zwischentotal: fett, Akzent-Oberkante (Titelsumme wie in den Fretz/AWU-Offerten).
            ws.Cell(r, ColPosition).Value = $"Zwischentotal {chapterTitle}";
            ws.Cell(r, ColPosition).Style.Font.Bold = true;
            ws.Cell(r, ColPosition).Style.Font.FontColor = AwuTitle;
            var subCell = ws.Cell(r, ColTotal);
            subCell.Style.Font.Bold = true;
            subCell.Style.NumberFormat.Format = MoneyFormat;
            ws.Range(r, ColNpk, r, ColHaltungen).Style.Border.TopBorder = XLBorderStyleValues.Thin;
            ws.Range(r, ColNpk, r, ColHaltungen).Style.Border.TopBorderColor = AwuAccent;
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
        ws.Range(r, ColNpk, r, ColHaltungen).Style.Fill.BackgroundColor = AwuAccentLight;
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

        // Schlusszeile im vollen AWU-Akzent (weiss auf grau-blau) — klarer Abschluss des LV.
        ws.Cell(r, ColPosition).Value = "TOTAL (inkl. MwSt.)";
        ws.Cell(r, ColPosition).Style.Font.Bold = true;
        ws.Cell(r, ColPosition).Style.Font.FontColor = XLColor.White;
        var inclCell = ws.Cell(r, ColTotal);
        inclCell.Style.Font.Bold = true;
        inclCell.Style.Font.FontColor = XLColor.White;
        inclCell.Style.NumberFormat.Format = MoneyFormat;
        ws.Range(r, ColNpk, r, ColHaltungen).Style.Fill.BackgroundColor = AwuAccent;
        inclCell.FormulaA1 = $"={Col(ColTotal)}{grandRow}+{Col(ColTotal)}{mwstRow}";
        var lastRow = r;

        // ── Spaltenbreiten + Fixierung ──────────────────────────────
        ws.Column(ColNpk).Width = 11;
        ws.Column(ColNpkD16).Width = 13;
        ws.Column(ColPosition).Width = 44;
        ws.Column(ColDn).Width = 6;
        ws.Column(ColMenge).Width = 10;
        ws.Column(ColEinheit).Width = 8;
        ws.Column(ColEp).Width = 12;
        ws.Column(ColTotal).Width = 14;
        ws.Column(ColHaltungen).Width = 9;
        ws.SheetView.FreezeRows(headerRow);

        // ── A4-Druckformat (hochkant, 1 Seite breit, Kopfzeile wiederholen) ──
        var setup = ws.PageSetup;
        setup.PaperSize = XLPaperSize.A4Paper;
        setup.PageOrientation = XLPageOrientation.Portrait;
        setup.FitToPages(1, 0); // 1 Seite breit, beliebig hoch
        setup.Margins.SetTop(0.6).SetBottom(0.6).SetLeft(0.5).SetRight(0.5);
        setup.CenterHorizontally = true;
        setup.SetRowsToRepeatAtTop(headerRow, headerRow);
        setup.PrintAreas.Clear();
        setup.PrintAreas.Add(1, ColNpk, lastRow, ColHaltungen);
        setup.Footer.Left.AddText("Abwasser Uri — NPK-135-Leistungsverzeichnis");
        setup.Footer.Right.AddText("Seite ");
        setup.Footer.Right.AddText(XLHFPredefinedText.PageNumber, XLHFOccurrence.AllPages);
        setup.Footer.Right.AddText(" von ");
        setup.Footer.Right.AddText(XLHFPredefinedText.NumberOfPages, XLHFOccurrence.AllPages);
    }

    // Logo wie bei den AWU-Protokollen: explizit uebergeben oder Standard-Ablage der App.
    private static string? ResolveLogoPath(string? logoPathAbs)
    {
        if (!string.IsNullOrWhiteSpace(logoPathAbs) && File.Exists(logoPathAbs))
            return logoPathAbs;
        var appLogo = Path.Combine(AppContext.BaseDirectory, "Assets", "Brand", "abwasser-uri-logo.png");
        return File.Exists(appLogo) ? appLogo : null;
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
