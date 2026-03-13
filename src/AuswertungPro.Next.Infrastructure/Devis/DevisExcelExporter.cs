using ClosedXML.Excel;
using AuswertungPro.Next.Domain.Models.Devis;

namespace AuswertungPro.Next.Infrastructure.Devis;

public sealed class DevisExcelExporter
{
    public void Export(Eigendevis devis, string outputPath)
    {
        using var workbook = new XLWorkbook();
        CreateSheet(workbook, "Eigendevis ohne Preis", devis, showPreise: false);
        CreateSheet(workbook, "Eigendevis mit KV", devis, showPreise: true);
        workbook.SaveAs(outputPath);
    }

    public void ExportBeide(DevisErgebnis ergebnis, string outputFolder)
    {
        var bm = Path.Combine(outputFolder, "Eigendevis_Baumeister.xlsx");
        var rl = Path.Combine(outputFolder, "Eigendevis_Rohrleitungsbau.xlsx");
        Export(ergebnis.Baumeister, bm);
        Export(ergebnis.Rohrleitungsbau, rl);
    }

    private static void CreateSheet(XLWorkbook workbook, string name, Eigendevis devis, bool showPreise)
    {
        var ws = workbook.Worksheets.Add(name);

        int row = 2;
        ws.Cell(row, 1).Value = devis.Titel;
        ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Cell(row, 1).Style.Font.FontSize = 14;

        row += 2;
        ws.Cell(row, 1).Value = "Baustelle:";
        ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Cell(row, 4).Value = devis.Baustelle;
        row++;
        ws.Cell(row, 4).Value = devis.Zone;
        row++;
        ws.Cell(row, 1).Value = "Gewerk:";
        ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Cell(row, 4).Value = devis.Gewerk.ToString();

        row += 2;
        // Header
        WriteHeader(ws, row);
        StyleHeaderRow(ws, row);
        row += 2;

        foreach (var gruppe in devis.Hauptgruppen)
        {
            // Gruppenheader
            ws.Cell(row, 1).Value = gruppe.Nummer;
            ws.Cell(row, 4).Value = gruppe.Bezeichnung;
            ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row, 4).Style.Font.Bold = true;
            row += 2;

            // Abschnitte
            foreach (var abschnitt in gruppe.Abschnitte)
            {
                ws.Cell(row, 4).Value = abschnitt.Bezeichnung;
                ws.Cell(row, 4).Style.Font.Italic = true;
                row++;

                foreach (var pos in abschnitt.Positionen)
                {
                    WritePosition(ws, ref row, pos, showPreise);
                }

                // Abschnitt-Subtotal
                ws.Cell(row, 4).Value = $"Subtotal {abschnitt.Bezeichnung}";
                ws.Cell(row, 4).Style.Font.Italic = true;
                if (showPreise)
                {
                    ws.Cell(row, 10).Value = (double)abschnitt.Total;
                    ws.Cell(row, 10).Style.Font.Italic = true;
                }
                row += 2;
            }

            // Einzelpositionen (nicht in Abschnitten)
            foreach (var pos in gruppe.Positionen)
            {
                WritePosition(ws, ref row, pos, showPreise);
            }

            // Gruppen-Total
            ws.Cell(row, 1).Value = gruppe.Nummer;
            ws.Cell(row, 4).Value = $"Total {gruppe.Bezeichnung}";
            ws.Cell(row, 4).Style.Font.Bold = true;
            if (showPreise)
            {
                ws.Cell(row, 10).Value = (double)gruppe.Total;
                ws.Cell(row, 10).Style.Font.Bold = true;
            }
            row += 2;
        }

        // Gesamttotal
        row++;
        ws.Cell(row, 4).Value = "Gesamttotal exkl. MWSt.";
        ws.Cell(row, 4).Style.Font.Bold = true;
        if (showPreise)
        {
            ws.Cell(row, 10).Value = (double)devis.GesamttotalExklMwst;
            ws.Cell(row, 10).Style.Font.Bold = true;
        }
        row += 2;

        ws.Cell(row, 4).Value = "+ MWSt.";
        ws.Cell(row, 6).Value = (double)devis.MwstSatz;
        ws.Cell(row, 6).Style.NumberFormat.Format = "0.0%";
        if (showPreise)
            ws.Cell(row, 10).Value = (double)devis.MwstBetrag;
        row += 2;

        ws.Cell(row, 4).Value = "Gesamttotal inkl. MWSt.";
        ws.Cell(row, 4).Style.Font.Bold = true;
        ws.Cell(row, 4).Style.Font.FontSize = 12;
        if (showPreise)
        {
            ws.Cell(row, 10).Value = (double)devis.GesamttotalInklMwst;
            ws.Cell(row, 10).Style.Font.Bold = true;
            ws.Cell(row, 10).Style.Font.FontSize = 12;
        }

        // Column widths
        ws.Column(1).Width = 12;
        ws.Column(2).Width = 8;
        ws.Column(3).Width = 8;
        ws.Column(4).Width = 60;
        ws.Column(5).Width = 8;
        ws.Column(6).Width = 10;
        ws.Column(7).Width = 4;
        ws.Column(8).Width = 12;
        ws.Column(9).Width = 4;
        ws.Column(10).Width = 16;

        // Number format for price columns
        ws.Column(8).Style.NumberFormat.Format = "#,##0.00";
        ws.Column(10).Style.NumberFormat.Format = "#,##0.00";
    }

    private static void WriteHeader(IXLWorksheet ws, int row)
    {
        ws.Cell(row, 1).Value = "Pos";
        ws.Cell(row, 2).Value = "Unter";
        ws.Cell(row, 3).Value = "Einzel";
        ws.Cell(row, 4).Value = "Bezeichnung";
        ws.Cell(row, 5).Value = "Einheit";
        ws.Cell(row, 6).Value = "Menge";
        ws.Cell(row, 8).Value = "EP";
        ws.Cell(row, 10).Value = "Betrag";
    }

    private static void StyleHeaderRow(IXLWorksheet ws, int row)
    {
        for (int c = 1; c <= 10; c++)
        {
            ws.Cell(row, c).Style.Font.Bold = true;
            ws.Cell(row, c).Style.Border.BottomBorder = XLBorderStyleValues.Thin;
        }
    }

    private static void WritePosition(IXLWorksheet ws, ref int row, DevisPosition pos, bool showPreise)
    {
        ws.Cell(row, 1).Value = pos.Hauptposition;
        ws.Cell(row, 2).Value = pos.Unterposition;
        ws.Cell(row, 3).Value = pos.Einzelposition;
        ws.Cell(row, 4).Value = pos.Bezeichnung;
        ws.Cell(row, 5).Value = pos.Einheit;
        ws.Cell(row, 6).Value = (double)pos.Menge;

        if (showPreise)
        {
            ws.Cell(row, 8).Value = (double)pos.Einheitspreis;
            ws.Cell(row, 10).Value = (double)pos.Betrag;
        }

        // Description lines
        if (!string.IsNullOrEmpty(pos.Beschreibung))
        {
            foreach (var zeile in pos.Beschreibung.Split('\n'))
            {
                row++;
                ws.Cell(row, 4).Value = zeile.Trim();
                ws.Cell(row, 4).Style.Font.Italic = true;
            }
        }

        row += 2;
    }
}
