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
    /// Ermittelt den Namen der laufenden Nummernspalte aus Spaltenliste oder Records.
    ///
    /// Reihenfolge: zuerst ein Feld, das WIRKLICH "NR" heisst (auch "NR." oder "Nr."),
    /// erst danach der alte weiche Vergleich "Name enthaelt irgendwo nr". Das schuetzt
    /// echte Datenfelder wie "Innen-Nr" oder "Kontroll-Nr" davor, beim Durchnummerieren
    /// mit 1, 2, 3 ueberschrieben zu werden. Bei mehreren Kandidaten, die sich nur in der
    /// Gross-/Kleinschreibung unterscheiden, gewinnt die historische Schreibweise "NR".
    /// Gibt null zurueck, wenn kein Nummernfeld gefunden wurde.
    /// </summary>
    public static string? ResolveNrColumnName(IEnumerable<string> columns, IEnumerable<SchachtRecord> records)
    {
        var columnNames = columns as IReadOnlyList<string> ?? columns.ToList();

        var exactColumn = PickRunningNumberName(columnNames);
        if (exactColumn is not null)
            return exactColumn;

        var looseColumn = columnNames.FirstOrDefault(ContainsNr);
        if (!string.IsNullOrWhiteSpace(looseColumn))
            return looseColumn;

        var recordKeys = records
            .SelectMany(r => r.Fields.Keys)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return PickRunningNumberName(recordKeys) ?? recordKeys.FirstOrDefault(ContainsNr);
    }

    private static string? PickRunningNumberName(IReadOnlyList<string> names)
    {
        var candidates = names.Where(IsRunningNumberName).ToList();
        if (candidates.Count == 0)
            return null;

        return candidates.FirstOrDefault(n => NormalizeNumberName(n) == "NR") ?? candidates[0];
    }

    private static bool IsRunningNumberName(string? name)
        => string.Equals(NormalizeNumberName(name), "NR", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeNumberName(string? name)
        => (name ?? string.Empty).Trim().TrimEnd('.').Trim();

    private static bool ContainsNr(string? name)
        => (name ?? string.Empty).Contains("nr", StringComparison.OrdinalIgnoreCase);

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
