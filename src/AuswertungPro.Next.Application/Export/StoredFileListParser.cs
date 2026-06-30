using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace AuswertungPro.Next.Application.Export;

/// <summary>
/// Liest eine gespeicherte Dateiliste aus einem Metadaten-Rohwert.
/// Unterstützt JSON-Array sowie semicolon-getrennte Zeichenkette als Fallback.
/// </summary>
public static class StoredFileListParser
{
    /// <summary>
    /// Parst <paramref name="raw"/> als JSON-Array oder ';'-separierte Liste.
    /// Gibt eine leere Liste zurück, wenn <paramref name="raw"/> leer oder null ist.
    /// </summary>
    public static List<string> Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return new List<string>();

        try
        {
            var list = JsonSerializer.Deserialize<List<string>>(raw);
            return list?.Where(s => !string.IsNullOrWhiteSpace(s))
                        .Select(s => s.Trim())
                        .ToList()
                   ?? new List<string>();
        }
        catch
        {
            // Fallback: Semikolon-getrennte Altform
            return raw.Split(';', StringSplitOptions.RemoveEmptyEntries)
                      .Select(p => p.Trim())
                      .Where(p => p.Length > 0)
                      .ToList();
        }
    }
}
