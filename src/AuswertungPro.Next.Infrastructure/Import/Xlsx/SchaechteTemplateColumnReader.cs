using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClosedXML.Excel;

namespace AuswertungPro.Next.Infrastructure.Import.Xlsx;

/// <summary>
/// Liest die Spaltenkoepfe aus der Schaechte-Excel-Vorlage.
/// Kapselt das ClosedXML-IO damit der ViewModel kein ClosedXML benoetigt.
/// </summary>
public static class SchaechteTemplateColumnReader
{
    // Zeilennummer der Kopfzeile in der Vorlage (1-basiert)
    private const int HeaderRow = 12;

    /// <summary>
    /// Gibt den Pfad zur Schaechte.xlsx Vorlage zurueck,
    /// oder string.Empty wenn keine Vorlage gefunden.
    /// </summary>
    public static string ResolveTemplatePath()
    {
        var exportDir = Path.Combine(AppContext.BaseDirectory, "Export_Vorlage");
        if (!Directory.Exists(exportDir))
            return string.Empty;

        var exact = Path.Combine(exportDir, "Schaechte.xlsx");
        if (File.Exists(exact))
            return exact;

        // Fallback: Datei im Verzeichnis die "ch" und "te" enthaelt (Schachte-Varianten)
        var fallback = Directory
            .GetFiles(exportDir, "*.xlsx")
            .FirstOrDefault(f =>
                Path.GetFileName(f).Contains("ch", StringComparison.OrdinalIgnoreCase) &&
                Path.GetFileName(f).Contains("te", StringComparison.OrdinalIgnoreCase));

        return fallback ?? string.Empty;
    }

    /// <summary>
    /// Liest alle nicht-leeren Spaltenkoepfe aus Zeile 12 des Arbeitsblatts "Schaechte"
    /// (Fallback: erstes Arbeitsblatt). Duplikate werden uebersprungen.
    /// Gibt eine leere Liste zurueck wenn die Datei nicht existiert.
    /// </summary>
    public static IReadOnlyList<string> ReadColumns(string templatePath)
    {
        if (string.IsNullOrWhiteSpace(templatePath) || !File.Exists(templatePath))
            return Array.Empty<string>();

        using var wb = new XLWorkbook(templatePath);
        var ws = wb.Worksheets
                     .FirstOrDefault(w => string.Equals(w.Name, "Schaechte", StringComparison.OrdinalIgnoreCase))
                 ?? wb.Worksheet(1);

        var lastHeaderCell = ws.Row(HeaderRow).LastCellUsed();
        var lastCol = lastHeaderCell?.Address.ColumnNumber ?? 1;

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<string>();

        for (var c = 1; c <= lastCol; c++)
        {
            var header = ws.Cell(HeaderRow, c).GetString()?.Trim();
            if (!string.IsNullOrWhiteSpace(header) && seen.Add(header))
                result.Add(header);
        }

        return result;
    }
}
