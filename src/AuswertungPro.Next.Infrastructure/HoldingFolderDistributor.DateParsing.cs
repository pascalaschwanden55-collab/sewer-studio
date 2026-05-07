using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using AuswertungPro.Next.Infrastructure.Import.Pdf;

namespace AuswertungPro.Next.Infrastructure;

/// <summary>
/// HoldingFolderDistributor — Date-Parsing-Helpers (partial class).
///
/// Refactor 2026-05-07 (Etappe 2, Charge R5): konsolidiert alle
/// Datums-bezogenen Helper-Methoden. Die Date-Regex-Felder bleiben in
/// HoldingFolderDistributor.Regex.cs, da sie als statische Felder der
/// gleichen partial class direkt zugaenglich sind.
/// </summary>
public static partial class HoldingFolderDistributor
{
    /// <summary>
    /// Parst einen Datums-String in den ueblichen Schweizer/europaeischen Formaten:
    /// dd.MM.yyyy, dd.MM.yy, dd/MM/yyyy, dd/MM/yy, dd-MM-yyyy, dd-MM-yy, yyyy-MM-dd.
    /// </summary>
    private static bool TryParseDateString(string value, out DateTime date)
    {
        return DateTime.TryParseExact(
            value,
            new[] { "dd.MM.yyyy", "dd.MM.yy", "dd/MM/yyyy", "dd/MM/yy", "dd-MM-yyyy", "dd-MM-yy", "yyyy-MM-dd" },
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out date);
    }

    /// <summary>
    /// Extrahiert das Inspektionsdatum aus PDF-Form-Field-Entries.
    /// Bevorzugt explizit beschriftete Felder, faellt sonst auf das erste
    /// parsbare Datum zurueck.
    /// </summary>
    private static DateTime? TryExtractDateFromFormEntries(IReadOnlyList<PdfFormFieldEntry> entries)
    {
        var dateRx = FormEntryDateRx;

        // Prefer labeled date fields.
        foreach (var entry in entries)
        {
            var label = BuildFormEntryLabel(entry);
            if (!ContainsDateLabel(label))
                continue;

            var m = dateRx.Match(entry.Value);
            if (m.Success && TryParseDateString(m.Groups["d"].Value, out var parsed))
                return parsed;
        }

        // Fallback: first parseable date from any value.
        foreach (var entry in entries)
        {
            var m = dateRx.Match(entry.Value);
            if (m.Success && TryParseDateString(m.Groups["d"].Value, out var parsed))
                return parsed;
        }

        return null;
    }

    /// <summary>
    /// Sucht das Inspektionsdatum in Haltungsinspektions-PDF-Text. Drei
    /// Prioritaetsstufen: Header-Zeile, Inspektions-Label, beliebiges Datum
    /// (mit Schutz gegen Gedruckt-/erstellt-Zeilen und Plausibilitaets-
    /// Validation 2000-2030).
    /// </summary>
    private static DateTime? TryFindInspectionDate(string text)
    {
        var dateRx = InspectionDateRx;
        var lines = text.Replace("\r\n", "\n").Split('\n');

        // Priority 1: Find date in header line
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (line.Contains("Haltungsinspektion", StringComparison.OrdinalIgnoreCase)
                || line.Contains("Haltungsbilder", StringComparison.OrdinalIgnoreCase))
            {
                var mHeader = dateRx.Match(line);
                if (mHeader.Success && TryParseDateString(mHeader.Groups[1].Value, out var dh))
                    return dh;
            }
        }

        // Priority 2: Find date near Inspektionsdatum or similar
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (line.Contains("Gedruckt", StringComparison.OrdinalIgnoreCase))
                continue;

            if (!line.Contains("Insp", StringComparison.OrdinalIgnoreCase) &&
                !line.Contains("Inspekt", StringComparison.OrdinalIgnoreCase) &&
                !line.Contains("Datum", StringComparison.OrdinalIgnoreCase) &&
                !line.Contains("Aufnahme", StringComparison.OrdinalIgnoreCase))
                continue;

            var m = dateRx.Match(line);
            if (m.Success && TryParseDateString(m.Groups[1].Value, out var d1))
                return d1;

            var prev = FindNearbyDate(lines, i - 1, -1, 3, dateRx);
            if (prev is not null) return prev;
            var next = FindNearbyDate(lines, i + 1, 1, 3, dateRx);
            if (next is not null) return next;
        }

        // Priority 3: Any date, but skip Gedruckt lines
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (line.Contains("Gedruckt", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("erstellt", StringComparison.OrdinalIgnoreCase))
                continue;

            var any = dateRx.Match(line);
            if (any.Success && TryParseDateString(any.Groups[1].Value, out var d2))
            {
                // Validate reasonable date range (2000-2030)
                if (d2.Year >= 2000 && d2.Year <= 2030)
                    return d2;
            }
        }

        return null;
    }

    /// <summary>
    /// Sucht ein Datum in einem Schacht-PDF-Text. Bevorzugt mit "Datum:"-
    /// Label, faellt sonst auf das erste generische Datum zurueck (mit
    /// Foto-Zeilen-Filter).
    /// </summary>
    private static DateTime? TryFindSchachtDate(string text)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n');
        var labeledDateRx = LabeledDateRx;
        foreach (var line in lines)
        {
            var m = labeledDateRx.Match(line);
            if (!m.Success)
                continue;

            if (TryParseDateString(m.Groups["date"].Value, out var d))
                return d;
        }

        var genericDateRx = GenericDateRx;
        foreach (var line in lines)
        {
            if (line.Contains("Foto", StringComparison.OrdinalIgnoreCase))
                continue;

            var m = genericDateRx.Match(line);
            if (!m.Success)
                continue;

            if (TryParseDateString(m.Groups["date"].Value, out var d))
                return d;
        }

        return null;
    }

    /// <summary>
    /// Sucht ab einem Start-Index in beide Richtungen (step=+/-1) bis zu
    /// maxLines Zeilen weit nach einem parsbaren Datum.
    /// </summary>
    private static DateTime? FindNearbyDate(string[] lines, int startIndex, int step, int maxLines, Regex dateRx)
    {
        if (startIndex < 0 || startIndex >= lines.Length) return null;
        var checkedLines = 0;
        for (var i = startIndex; i >= 0 && i < lines.Length && checkedLines < maxLines; i += step)
        {
            var line = lines[i];
            checkedLines++;
            if (string.IsNullOrWhiteSpace(line)) continue;
            var m = dateRx.Match(line);
            if (m.Success && TryParseDateString(m.Groups[1].Value, out var d))
                return d;
        }
        return null;
    }
}
