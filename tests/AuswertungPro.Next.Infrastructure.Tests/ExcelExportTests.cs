using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Export.Excel;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class ExcelExportTests
{
    [Fact]
    public void Export_WritesDataIntoTemplateCopy()
    {
        var root = TestPaths.FindSolutionRoot();
        var templatePath = Path.Combine(root, "Export_Vorlage", "Haltungen.xlsx");
        Assert.True(File.Exists(templatePath), $"Template not found: {templatePath}");

        var outputPath = Path.Combine(Path.GetTempPath(), $"Haltungen_{Guid.NewGuid():N}.xlsx");

        var project = new Project();
        var rec = new HaltungRecord();
        rec.SetFieldValue("Haltungsname", "TEST-1", FieldSource.Manual, userEdited: false);
        rec.SetFieldValue("Strasse", "Testweg", FieldSource.Manual, userEdited: false);
        rec.SetFieldValue("Rohrmaterial", "Beton", FieldSource.Manual, userEdited: false);
        rec.SetFieldValue("DN_mm", "300", FieldSource.Manual, userEdited: false);
        rec.SetFieldValue("Nutzungsart", "Schmutzwasser", FieldSource.Manual, userEdited: false);
        rec.SetFieldValue("Haltungslaenge_m", "12.5", FieldSource.Manual, userEdited: false);
        rec.SetFieldValue("Inspektionsrichtung", "In Fliessrichtung", FieldSource.Manual, userEdited: false);
        rec.SetFieldValue("Primaere_Schaeden", "RISS", FieldSource.Manual, userEdited: false);
        project.Data.Add(rec);

        // Detect header row (template may shift over time)
        int FindHeaderRow(IXLWorksheet ws)
        {
            for (int r = 1; r <= 100; r++)
            {
                var row = ws.Row(r);
                if (!row.CellsUsed().Any()) continue;

                var headerTokens = row.CellsUsed()
                    .Select(c => (c.GetString() ?? "").Trim())
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                var hits = 0;
                if (headerTokens.Contains("Haltungsname (ID)")) hits++;
                if (headerTokens.Contains("Haltungsnahme (ID)")) hits++;
                if (headerTokens.Contains("NR.")) hits++;
                if (headerTokens.Contains("Strasse")) hits++;
                if (headerTokens.Contains("Rohrmaterial")) hits++;

                // Require more than one token to avoid false positives.
                if (hits >= 2) return r;
            }
            return 11;
        }

        using var templateWbProbe = new XLWorkbook(templatePath);
        var templateWsProbe = templateWbProbe.Worksheets.FirstOrDefault(w => string.Equals(w.Name, "Haltungen", StringComparison.OrdinalIgnoreCase))
                              ?? templateWbProbe.Worksheet(1);
        var headerRow = FindHeaderRow(templateWsProbe);
        var startRow = headerRow + 1;

        var svc = new ExcelTemplateExportService();
        var res = svc.ExportToTemplate(project, templatePath, outputPath, headerRow: headerRow, startRow: startRow);
        Assert.True(res.Ok, $"{res.ErrorCode}: {res.ErrorMessage}");

        using var templateWb = new XLWorkbook(templatePath);
        var templateWs = templateWb.Worksheets.FirstOrDefault(w => string.Equals(w.Name, "Haltungen", StringComparison.OrdinalIgnoreCase))
                        ?? templateWb.Worksheet(1);

        using var outWb = new XLWorkbook(outputPath);
        var outWs = outWb.Worksheets.FirstOrDefault(w => string.Equals(w.Name, "Haltungen", StringComparison.OrdinalIgnoreCase))
                     ?? outWb.Worksheet(1);

        var templateHeader = templateWs.Row(headerRow).CellsUsed()
            .Select(c => (c.Address.ColumnNumber, c.GetString()))
            .ToList();
        var outHeader = outWs.Row(headerRow).CellsUsed()
            .Select(c => (c.Address.ColumnNumber, c.GetString()))
            .ToList();

        Assert.Equal(templateHeader, outHeader);

        var headerToCol = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in outWs.Row(headerRow).CellsUsed())
        {
            var key = (c.GetString() ?? "").Trim();
            if (string.IsNullOrWhiteSpace(key))
                continue;

            // Some templates use placeholder numeric values in the header area.
            if (key.All(char.IsDigit))
                continue;

            // Ignore duplicates (e.g., merged cells / repeated headers)
            if (!headerToCol.ContainsKey(key))
                headerToCol[key] = c.Address.ColumnNumber;
        }

        Assert.True(headerToCol.TryGetValue("Haltungsname (ID)", out var hnCol) || headerToCol.TryGetValue("Haltungsnahme (ID)", out hnCol));
        Assert.True(headerToCol.TryGetValue("Strasse", out var strCol));
        Assert.True(headerToCol.TryGetValue("Rohrmaterial", out var matCol));
        Assert.True(headerToCol.TryGetValue("DN mm", out var dnCol));
        Assert.True(headerToCol.TryGetValue("Nutzungsart", out var nuCol));
        Assert.True(headerToCol.TryGetValue("Haltungslänge m", out var lenCol) || headerToCol.TryGetValue("HaltungslÃ¤nge m", out lenCol));
        Assert.True(headerToCol.TryGetValue("Inspektionsrichtung", out var flCol) || headerToCol.TryGetValue("Fliessrichtung", out flCol));

        Assert.Equal("TEST-1", outWs.Cell(startRow, hnCol).GetString());
        Assert.Equal("Testweg", outWs.Cell(startRow, strCol).GetString());
        Assert.Equal("Beton", outWs.Cell(startRow, matCol).GetString());
        Assert.Equal(300d, outWs.Cell(startRow, dnCol).GetDouble(), 3);
        Assert.Equal("Schmutzwasser", outWs.Cell(startRow, nuCol).GetString());
        Assert.Equal(12.5d, outWs.Cell(startRow, lenCol).GetDouble(), 3);
        Assert.Equal("In Fliessrichtung", outWs.Cell(startRow, flCol).GetString());
    }
}
