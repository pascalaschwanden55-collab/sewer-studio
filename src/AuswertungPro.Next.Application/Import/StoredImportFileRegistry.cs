using System.Text.Json;

namespace AuswertungPro.Next.Application.Import;

/// <summary>
/// Verwaltet die JSON-serialisierten Dateilisten (relative Pfade) im Projekt-Metadaten-Dictionary.
/// Enthaelt ausschliesslich Serialisierung/Deserialisierung – kein Datei-IO.
/// </summary>
public static class StoredImportFileRegistry
{
    /// <summary>
    /// Liest die bereits gespeicherte Dateiliste fuer den angegebenen Metadaten-Schluessel.
    /// </summary>
    public static List<string> Load(IDictionary<string, string> metadata, string metadataKey)
    {
        if (!metadata.TryGetValue(metadataKey, out var raw) || string.IsNullOrWhiteSpace(raw))
            return new List<string>();

        try
        {
            var list = JsonSerializer.Deserialize<List<string>>(raw);
            return list?.Where(si => !string.IsNullOrWhiteSpace(si))
                        .Select(si => si.Trim())
                        .ToList()
                   ?? new List<string>();
        }
        catch
        {
            // Fallback: aelteres Semikolon-Format
            return raw.Split(';', StringSplitOptions.RemoveEmptyEntries)
                      .Select(p => p.Trim())
                      .Where(p => p.Length > 0)
                      .ToList();
        }
    }

    /// <summary>
    /// Schreibt die aktualisierten relativen Pfade zurueck in den Metadaten-Schluessel.
    /// Fuegt <paramref name="relativePaths"/> zur bestehenden Liste hinzu (keine Dopplungen).
    /// </summary>
    public static void Save(
        IDictionary<string, string> metadata,
        string metadataKey,
        IEnumerable<string> relativePaths)
    {
        var existing = Load(metadata, metadataKey);
        foreach (var p in relativePaths)
        {
            if (!existing.Contains(p, StringComparer.OrdinalIgnoreCase))
                existing.Add(p);
        }
        metadata[metadataKey] = JsonSerializer.Serialize(existing);
    }
}
