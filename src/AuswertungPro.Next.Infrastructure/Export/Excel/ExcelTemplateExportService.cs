using System.Globalization;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using ClosedXML.Excel;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Export;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Export.Excel;

public sealed class ExcelTemplateExportService : IExcelExportService
{
    public Result ExportToTemplate(Project project, string templatePath, string outputPath, int headerRow, int startRow)
    {
        try
        {
            if (project is null) throw new ArgumentNullException(nameof(project));
            if (string.IsNullOrWhiteSpace(templatePath)) throw new ArgumentException("Template-Pfad fehlt.", nameof(templatePath));
            if (!File.Exists(templatePath)) throw new FileNotFoundException("Excel-Vorlage nicht gefunden.", templatePath);
            if (string.IsNullOrWhiteSpace(outputPath)) throw new ArgumentException("Output-Pfad fehlt.", nameof(outputPath));

            if (headerRow <= 0) headerRow = 11;
            if (startRow <= 0) startRow = 12;

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

            using var wb = new XLWorkbook(templatePath);
            var ws = wb.Worksheets.FirstOrDefault(w => string.Equals(w.Name, "Haltungen", StringComparison.OrdinalIgnoreCase))
                     ?? wb.Worksheet(1);

            var headerToCol = ReadHeaderColumns(ws, headerRow);

            var alias = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Haltungsnahme (ID)"] = "Haltungsname",
                ["Fliessrichtung"] = "Inspektionsrichtung"
            };

            var fieldToCol = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in headerToCol)
            {
                var header = kv.Key.Trim();
                var normalizedHeader = NormalizeHeader(header);

                if (alias.TryGetValue(header, out var fieldFromAlias))
                {
                    fieldToCol[fieldFromAlias] = kv.Value;
                    continue;
                }

                var match = FieldCatalog.Definitions.FirstOrDefault(d =>
                    string.Equals(NormalizeHeader(d.Value.Label), normalizedHeader, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(NormalizeHeader(d.Key), normalizedHeader, StringComparison.OrdinalIgnoreCase));

                if (!string.IsNullOrWhiteSpace(match.Key))
                    fieldToCol[match.Key] = kv.Value;
            }

            if (fieldToCol.Count == 0)
                throw new InvalidOperationException("Keine passenden Spalten im Excel-Template gefunden (Header-Zeile pruefen).");

            var lastUsedRow = ws.LastRowUsed()?.RowNumber() ?? startRow;
            if (lastUsedRow < startRow) lastUsedRow = startRow;

            foreach (var col in fieldToCol.Values.Distinct())
                ws.Range(startRow, col, lastUsedRow, col).Clear(XLClearOptions.Contents);

            var ordered = project.Data
                .OrderBy(r => TryInt(r.GetFieldValue("NR")) ?? int.MaxValue)
                .ThenBy(r => r.GetFieldValue("Haltungsname") ?? "")
                .ToList();

            int row = startRow;
            int runningNr = 1;

            foreach (var rec in ordered)
            {
                if (fieldToCol.TryGetValue("NR", out var nrCol))
                {
                    var nr = (rec.GetFieldValue("NR") ?? "").Trim();
                    if (string.IsNullOrWhiteSpace(nr)) nr = runningNr.ToString(CultureInfo.InvariantCulture);
                    ws.Cell(row, nrCol).Value = nr;
                }

                foreach (var field in FieldCatalog.ColumnOrder)
                {
                    if (!fieldToCol.TryGetValue(field, out var col))
                        continue;

                    var def = FieldCatalog.Get(field);
                    var value = rec.GetFieldValue(field);

                    if (def.Type == FieldType.Decimal || def.Type == FieldType.Int)
                    {
                        WriteNumber(ws, row, fieldToCol, field, value);
                        continue;
                    }

                    WriteText(ws, row, fieldToCol, field, value);

                    if (def.Type == FieldType.Multiline)
                        ws.Cell(row, col).Style.Alignment.WrapText = true;
                }

                row++;
                runningNr++;
            }

            wb.SaveAs(outputPath);
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Fail("EXP-EXCEL", ex.Message);
        }
    }

    public Result ExportSchaechteToTemplate(Project project, string templatePath, string outputPath, int headerRow, int startRow)
    {
        try
        {
            if (project is null) throw new ArgumentNullException(nameof(project));
            if (string.IsNullOrWhiteSpace(templatePath)) throw new ArgumentException("Template-Pfad fehlt.", nameof(templatePath));
            if (!File.Exists(templatePath)) throw new FileNotFoundException("Excel-Vorlage nicht gefunden.", templatePath);
            if (string.IsNullOrWhiteSpace(outputPath)) throw new ArgumentException("Output-Pfad fehlt.", nameof(outputPath));

            if (headerRow <= 0) headerRow = 11;
            if (startRow <= 0) startRow = 12;

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

            using var wb = new XLWorkbook(templatePath);
            var ws = wb.Worksheets.FirstOrDefault(w => string.Equals(w.Name, "Schaechte", StringComparison.OrdinalIgnoreCase))
                     ?? wb.Worksheet(1);

            var headerToCol = ReadHeaderColumns(ws, headerRow);
            if (headerToCol.Count == 0)
                throw new InvalidOperationException("Keine Spalten in der Schacht-Vorlage gefunden (Header-Zeile pruefen).");

            var lastUsedRow = ws.LastRowUsed()?.RowNumber() ?? startRow;
            if (lastUsedRow < startRow) lastUsedRow = startRow;

            foreach (var col in headerToCol.Values.Distinct())
                ws.Range(startRow, col, lastUsedRow, col).Clear(XLClearOptions.Contents);

            var row = startRow;
            foreach (var rec in project.SchaechteData)
            {
                foreach (var pair in headerToCol)
                {
                    var header = pair.Key;
                    var col = pair.Value;
                    var value = rec.GetFieldValue(header);

                    if (!TryWriteNumber(ws, row, col, value))
                        ws.Cell(row, col).Value = (value ?? "").Trim();
                }

                row++;
            }

            wb.SaveAs(outputPath);
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Fail("EXP-EXCEL-SCHACHT", ex.Message);
        }
    }

    private static void WriteText(IXLWorksheet ws, int row, Dictionary<string, int> fieldToCol, string field, string? value)
    {
        if (!fieldToCol.TryGetValue(field, out var col)) return;
        ws.Cell(row, col).Value = (value ?? "").Trim();
    }

    private static void WriteNumber(IXLWorksheet ws, int row, Dictionary<string, int> fieldToCol, string field, string? value)
    {
        if (!fieldToCol.TryGetValue(field, out var col)) return;

        var s = (value ?? "").Trim();
        if (string.IsNullOrWhiteSpace(s))
        {
            ws.Cell(row, col).Value = "";
            return;
        }

        s = s.Replace("'", "").Replace(",", ".");
        if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
            ws.Cell(row, col).Value = d;
        else
            ws.Cell(row, col).Value = (value ?? "").Trim();
    }

    private static bool TryWriteNumber(IXLWorksheet ws, int row, int col, string? value)
    {
        var s = (value ?? "").Trim();
        if (string.IsNullOrWhiteSpace(s))
            return false;

        s = s.Replace("'", "").Replace(",", ".");
        if (!double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
            return false;

        ws.Cell(row, col).Value = d;
        return true;
    }

    private static int? TryInt(string? s)
        => int.TryParse((s ?? "").Trim(), out var v) ? v : null;

    private static Dictionary<string, int> ReadHeaderColumns(IXLWorksheet ws, int headerRow)
    {
        var headerToCol = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var lastHeaderCell = ws.Row(headerRow).LastCellUsed();
        var lastCol = lastHeaderCell?.Address.ColumnNumber ?? 1;

        for (int c = 1; c <= lastCol; c++)
        {
            var h = ws.Cell(headerRow, c).GetString()?.Trim();
            if (!string.IsNullOrWhiteSpace(h) && !headerToCol.ContainsKey(h))
                headerToCol[h] = c;
        }

        return headerToCol;
    }

    private static string NormalizeHeader(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "";

        var s = text.Trim();

        s = s.Replace("ä", "ae").Replace("ö", "oe").Replace("ü", "ue").Replace("ß", "ss");
        s = s.Replace("Ä", "Ae").Replace("Ö", "Oe").Replace("Ü", "Ue");

        s = s.Replace("Ã¤", "ae").Replace("Ã¶", "oe").Replace("Ã¼", "ue").Replace("ÃŸ", "ss");
        s = s.Replace("Ã„", "Ae").Replace("Ã–", "Oe").Replace("Ãœ", "Ue");
        s = s.Replace("Â", "");

        return s.ToLowerInvariant();
    }
}
