namespace AuswertungPro.Next.Application.Import;

/// <summary>
/// Verwaltet die Import-Quellen-Historie im Projekt-Metadaten-Dictionary.
/// Schreibt ImportQuelle, ImportQuellTyp und ImportQuellenHistorie (max. 20 Eintraege).
/// </summary>
public static class ImportSourceHistoryService
{
    private const string HistoryKey = "ImportQuellenHistorie";
    private const int MaxHistoryEntries = 20;

    /// <summary>
    /// Traegt die aktuelle Import-Quelle in die Metadaten ein und haengt einen
    /// Eintrag an die Historie an.
    /// </summary>
    /// <param name="metadata">Metadaten-Dictionary des aktiven Projekts.</param>
    /// <param name="sourcePath">Pfad zur Import-Quelle (Ordner oder Datei).</param>
    /// <param name="importType">Import-Typ-Label (z.B. "PDF", "XTF", "WinCan").</param>
    public static void Track(
        IDictionary<string, string> metadata,
        string sourcePath,
        string importType)
    {
        var timestamp = DateTime.Now.ToString(
            "yyyy-MM-dd HH:mm",
            System.Globalization.CultureInfo.InvariantCulture);
        var entry = $"{timestamp} | {importType} | {sourcePath}";

        // Letzte Import-Quelle (Direktzugriff)
        metadata["ImportQuelle"] = sourcePath;
        metadata["ImportQuellTyp"] = importType;

        // Historie anfuegen (max. MaxHistoryEntries Eintraege)
        var existing = metadata.TryGetValue(HistoryKey, out var h) ? h : "";
        var lines = existing
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .ToList();
        lines.Add(entry);
        if (lines.Count > MaxHistoryEntries)
            lines = lines.Skip(lines.Count - MaxHistoryEntries).ToList();
        metadata[HistoryKey] = string.Join("\n", lines);
    }

    /// <summary>
    /// Gibt die gespeicherten Historie-Eintraege zurueck (aelteste zuerst).
    /// </summary>
    public static IReadOnlyList<string> GetHistory(IDictionary<string, string> metadata)
    {
        if (!metadata.TryGetValue(HistoryKey, out var raw) || string.IsNullOrWhiteSpace(raw))
            return Array.Empty<string>();
        return raw.Split('\n', StringSplitOptions.RemoveEmptyEntries);
    }
}
