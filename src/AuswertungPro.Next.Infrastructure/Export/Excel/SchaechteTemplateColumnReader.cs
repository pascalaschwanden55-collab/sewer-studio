using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClosedXML.Excel;

namespace AuswertungPro.Next.Infrastructure.Export.Excel;

public sealed record SchaechteTemplateColumnReadResult(string TemplatePath, IReadOnlyList<string> Columns)
{
    public bool TemplateFound => !string.IsNullOrWhiteSpace(TemplatePath);
}

public static class SchaechteTemplateColumnReader
{
    private const int HeaderRow = 12;

    public static SchaechteTemplateColumnReadResult LoadFromExportDirectory(string baseDirectory)
    {
        var templatePath = ResolveTemplatePath(baseDirectory);
        if (string.IsNullOrWhiteSpace(templatePath) || !File.Exists(templatePath))
            return new SchaechteTemplateColumnReadResult(string.Empty, Array.Empty<string>());

        return new SchaechteTemplateColumnReadResult(templatePath, ReadColumns(templatePath));
    }

    public static IReadOnlyList<string> ReadColumns(string templatePath)
    {
        using var workbook = new XLWorkbook(templatePath);
        var worksheet = workbook.Worksheets.FirstOrDefault(w => string.Equals(w.Name, "Schaechte", StringComparison.OrdinalIgnoreCase))
                        ?? workbook.Worksheet(1);

        var columns = new List<string>();
        var lastHeaderCell = worksheet.Row(HeaderRow).LastCellUsed();
        var lastColumn = lastHeaderCell?.Address.ColumnNumber ?? 1;

        for (var column = 1; column <= lastColumn; column++)
        {
            var header = worksheet.Cell(HeaderRow, column).GetString()?.Trim();
            if (!IsUsableHeader(header))
                continue;

            header = header!.Trim();
            if (!columns.Contains(header))
                columns.Add(header);
        }

        SwapColumnOrder(columns, "Funktion", "Schachtnummer");
        return columns;
    }

    private static string ResolveTemplatePath(string baseDirectory)
    {
        if (string.IsNullOrWhiteSpace(baseDirectory))
            return string.Empty;

        var exportDirectory = Path.Combine(baseDirectory, "Export_Vorlage");
        if (!Directory.Exists(exportDirectory))
            return string.Empty;

        var exact = Path.Combine(exportDirectory, "Schaechte.xlsx");
        if (File.Exists(exact))
            return exact;

        return Directory
            .GetFiles(exportDirectory, "*.xlsx")
            .FirstOrDefault(path =>
                Path.GetFileName(path).Contains("ch", StringComparison.OrdinalIgnoreCase) &&
                Path.GetFileName(path).Contains("te", StringComparison.OrdinalIgnoreCase))
            ?? string.Empty;
    }

    private static void SwapColumnOrder(List<string> columns, string firstColumnName, string secondColumnName)
    {
        if (columns.Count == 0)
            return;

        var first = columns.FirstOrDefault(x => x.Equals(firstColumnName, StringComparison.OrdinalIgnoreCase));
        var second = columns.FirstOrDefault(x => x.Equals(secondColumnName, StringComparison.OrdinalIgnoreCase));
        if (first is null || second is null)
            return;

        var firstIndex = columns.IndexOf(first);
        var secondIndex = columns.IndexOf(second);
        if (firstIndex < 0 || secondIndex < 0 || firstIndex == secondIndex)
            return;

        columns[firstIndex] = second;
        columns[secondIndex] = first;
    }

    private static bool IsUsableHeader(string? header)
    {
        if (string.IsNullOrWhiteSpace(header))
            return false;

        return !header.Trim().All(char.IsDigit);
    }
}
