using System;
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.DataPage;

/// <summary>
/// Reine Logik fuer Schacht-Datensatz-Operationen: Schluessel-Normalisierung,
/// Feldzuordnung, Nummernfeld-Erkennung, Suche und Ergebnis-Infotext.
/// Kein WPF, kein IO.
/// </summary>
public static class SchaechteFieldLogic
{
    /// <summary>
    /// Normalisiert einen Spaltenschluessel: Kleinbuchstaben + Umlaut-Expansion.
    /// Beispiel: "Prüfungsresultat" → "pruefungsresultat"
    /// </summary>
    public static string NormalizeKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        return value
            .Trim()
            .ToLowerInvariant()
            .Replace("ä", "ae", StringComparison.Ordinal)
            .Replace("ö", "oe", StringComparison.Ordinal)
            .Replace("ü", "ue", StringComparison.Ordinal)
            .Replace("ß", "ss", StringComparison.Ordinal)
            // Mojibake-Varianten (Latin-1 doppelt dekodiert)
            // ToLowerInvariant laeuft davor; daher muessen auch Mojibake-Muster
            // kleingeschrieben verglichen werden.
            .Replace("ã¤", "ae", StringComparison.Ordinal)
            .Replace("ã¶", "oe", StringComparison.Ordinal)
            .Replace("ã¼", "ue", StringComparison.Ordinal)
            .Replace("ãÿ", "ss", StringComparison.Ordinal)
            .Replace("ãƒâ¤", "ae", StringComparison.Ordinal)
            .Replace("ãƒâ¶", "oe", StringComparison.Ordinal)
            .Replace("ãƒâ¼", "ue", StringComparison.Ordinal)
            .Replace("ãƒå¸", "ss", StringComparison.Ordinal);
    }

    /// <summary>
    /// Liest den Feldwert eines logischen Schacht-Felds aus einem Record.
    /// <para>Unterstuetzte logische Felder: "sanieren", "pruefungsresultat",
    /// "referenzpruefung", "ausgefuehrt_durch".</para>
    /// Gibt "" zurueck wenn kein passendes Feld gefunden wird.
    /// </summary>
    public static string ResolveFieldValue(SchachtRecord record, string logicalField)
    {
        foreach (var kvp in record.Fields)
        {
            var n = NormalizeKey(kvp.Key);
            if (logicalField == "sanieren" && n.Contains("sanieren", StringComparison.Ordinal))
                return kvp.Value ?? "";
            if (logicalField == "pruefungsresultat" &&
                (n.Contains("pruefung", StringComparison.Ordinal) ||
                 n.Contains("dichtheit", StringComparison.Ordinal) ||
                 n.Contains("dichtigkeit", StringComparison.Ordinal)))
                return kvp.Value ?? "";
            if (logicalField == "referenzpruefung" &&
                n.Contains("referenz", StringComparison.Ordinal) &&
                n.Contains("pruefung", StringComparison.Ordinal))
                return kvp.Value ?? "";
            if (logicalField == "ausgefuehrt_durch" &&
                (n.Contains("ausgefuehrt", StringComparison.Ordinal) ||
                 n.Contains("ausgefuhrt", StringComparison.Ordinal)) &&
                n.Contains("durch", StringComparison.Ordinal))
                return kvp.Value ?? "";
        }

        return "";
    }

    /// <summary>
    /// Ermittelt den Namen der Nummernfeld-Spalte aus Spaltenliste oder Records.
    /// Prueft ob der Spaltenname "NR" oder "Nr" enthaelt.
    /// Gibt null zurueck wenn kein Nummernfeld gefunden.
    /// </summary>
    public static string? ResolveNrColumnName(IEnumerable<string> columns, IEnumerable<SchachtRecord> records)
    {
        var fromColumns = columns.FirstOrDefault(c =>
            c.Contains("NR", StringComparison.OrdinalIgnoreCase) ||
            c.Contains("Nr", StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(fromColumns))
            return fromColumns;

        var fromRecord = records
            .SelectMany(r => r.Fields.Keys)
            .FirstOrDefault(c =>
                c.Contains("NR", StringComparison.OrdinalIgnoreCase) ||
                c.Contains("Nr", StringComparison.OrdinalIgnoreCase));
        return fromRecord;
    }

    /// <summary>
    /// Prueft ob ein Schacht-Record den Suchbegriff enthaelt (Schluessel oder Wert).
    /// Gibt true zurueck wenn searchText leer ist.
    /// </summary>
    public static bool MatchesSearch(SchachtRecord record, string searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
            return true;

        var term = searchText.Trim();
        if (term.Length == 0)
            return true;

        return record.Fields.Any(kvp =>
            (!string.IsNullOrWhiteSpace(kvp.Key) &&
             kvp.Key.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrWhiteSpace(kvp.Value) &&
             kvp.Value.Contains(term, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>
    /// Erstellt den Such-Ergebnistext ("N von M Schaechten") oder "" bei leerem Suchbegriff.
    /// </summary>
    public static string BuildSearchResultInfo(int visibleCount, int totalCount, string searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
            return string.Empty;
        return $"{visibleCount} von {totalCount} Schaechten";
    }
}
