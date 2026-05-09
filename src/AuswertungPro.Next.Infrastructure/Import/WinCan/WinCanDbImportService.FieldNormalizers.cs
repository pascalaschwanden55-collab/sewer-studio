using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;

using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Import.WinCan;

// WinCanDbImportService Field-Normalizer: Setzt Werte auf HaltungRecord/
// SchachtRecord mit Manual-Source-Schutz, normalisiert Materialien/
// Nutzungsarten/Datums-Strings, parst SQLite-Doubles tolerant.
// Aus dem Hauptdatei extrahiert (Slice 19b).
public sealed partial class WinCanDbImportService
{
    private static void ApplyField(HaltungRecord record, string field, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        record.SetFieldValue(field, value.Trim(), FieldSource.Legacy, userEdited: false);
    }

    private static bool ApplyImportedField(HaltungRecord record, string field, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var before = record.GetFieldValue(field);
        record.SetFieldValue(field, value.Trim(), FieldSource.Legacy, userEdited: false);
        var after = record.GetFieldValue(field);
        return !string.Equals(before, after, StringComparison.OrdinalIgnoreCase);
    }

    private static void MergeImportedCandidate(HaltungRecord target, HaltungRecord source)
    {
        var mergeFields = new[]
        {
            "Datum_Jahr",
            "Haltungslaenge_m",
            "DN_mm",
            "Rohrmaterial",
            "Inspektionsrichtung",
            "Bemerkungen",
            "Link"
        };

        foreach (var field in mergeFields)
        {
            var current = target.GetFieldValue(field);
            if (!string.IsNullOrWhiteSpace(current))
                continue;

            var incoming = source.GetFieldValue(field);
            if (string.IsNullOrWhiteSpace(incoming))
                continue;

            target.SetFieldValue(field, incoming, FieldSource.Legacy, userEdited: false);
        }
    }

    private static void SetSchachtField(SchachtRecord record, string field, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        if (record.Fields.TryGetValue(field, out var existing) && !string.IsNullOrWhiteSpace(existing))
            return;

        record.SetFieldValue(field, value.Trim());
    }

    private static string? NormalizeNumber(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var text = raw.Trim();
        if (double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var val) ||
            double.TryParse(text, NumberStyles.Any, CultureInfo.CurrentCulture, out val))
        {
            if (Math.Abs(val - Math.Round(val)) < 0.01)
                return ((int)Math.Round(val)).ToString(CultureInfo.InvariantCulture);
            return val.ToString("0.##", CultureInfo.InvariantCulture);
        }

        return text;
    }

    private static string? NormalizeDate(string? yearText, string? rawDate)
    {
        if (!string.IsNullOrWhiteSpace(yearText))
            return yearText.Trim();

        var dt = ParseSqliteDate(rawDate);
        if (dt.HasValue)
            return dt.Value.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);

        return null;
    }

    private static string? NormalizeMaterial(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        // Take only the first line – WinCan DB sometimes appends cleaning info
        // like "Zement\nGereinigt    Ja" into the material field.
        var t = raw.Split('\n')[0].Trim();
        // Strip trailing non-material tokens (e.g. "Gereinigt Ja")
        t = Regex.Replace(t, @"(?i)\s*(gereinigt|nicht\s*gereinigt|verschmutzt)\s*(ja|nein)?\s*$", "").Trim();

        return string.IsNullOrWhiteSpace(t) ? null : t;
    }

    private static string? NormalizeUsage(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var t = raw.Trim();
        var lower = t.ToLowerInvariant();

        // Filter out non-usage values that sometimes end up in the Usage field
        // (e.g. cleaning status, material info, yes/no flags)
        if (lower is "gereinigt" or "nicht gereinigt" or "verschmutzt"
            or "ja" or "nein" or "yes" or "no"
            or "-" or "--" or "n/a" or "k.a.")
            return null;

        // Full-text matches (e.g. "Schmutzabwasser", "Regenwasser", "Mischabwasser")
        if (lower.Contains("regen"))
            return "Regenwasser";
        if (lower.Contains("schmutz"))
            return "Schmutzwasser";
        if (lower.Contains("misch"))
            return "Mischabwasser";

        // DWA-M150 / ISYBAU / VSA codes
        if (lower is "s" or "ks" or "sw") return "Schmutzwasser";
        if (lower is "r" or "kr" or "rw") return "Regenwasser";
        if (lower is "m" or "km" or "mw") return "Mischabwasser";

        // Schweizer VSA-Codes (E=Entwaesserung, H=Hausentwaesserung,
        // F=Fremdwasser, Z=Zufluss) und andere unbekannte Kurzformen
        // sind keine Standard-Nutzungsarten - nicht uebernehmen.
        if (t.Length <= 2)
            return null;

        return t;
    }

    private static string? NormalizeInspectionDir(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var t = raw.Trim();
        if (t == "1")
            return "In Fliessrichtung";
        if (t == "2")
            return "Gegen Fliessrichtung";

        return t;
    }

    private static string? NormalizeAccessible(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var t = raw.Trim().ToLowerInvariant();
        if (t is "1" or "true" or "ja" or "yes")
            return "offen";
        if (t is "0" or "false" or "nein" or "no")
            return "abgeschlossen";

        return raw.Trim();
    }

    private static double? SafeReadDouble(SqliteDataReader r, int col)
    {
        if (r.IsDBNull(col)) return null;
        var val = r.GetValue(col);
        if (val is double d) return d;
        if (val is decimal dec) return (double)dec;
        if (val is float f) return f;
        return double.TryParse(val?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var p) ? p : null;
    }
}
