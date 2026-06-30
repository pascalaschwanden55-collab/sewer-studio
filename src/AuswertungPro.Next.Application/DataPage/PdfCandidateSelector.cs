using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace AuswertungPro.Next.Application.DataPage;

/// <summary>
/// Reine Auswahl-/Token-/Parse-Logik fuer die Protokoll-PDF-Suche (kein Dateisystem-Zugriff).
/// Aus <c>UI.DataPage.DataPageProtocolPathResolver</c> extrahiert, damit unit-testbar
/// (verhaltensneutral; die UI-Klasse delegiert ihre puren Methoden hierher).
/// </summary>
public static class PdfCandidateSelector
{
    /// <summary>
    /// Suchtoken einer Haltung aus deren (rohem) Namen: sanitisierter Name plus Rohname,
    /// dedupliziert. Leer bei leerem Namen.
    /// </summary>
    public static IReadOnlyList<string> BuildHoldingTokens(string? holdingName)
    {
        var holdingRaw = (holdingName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(holdingRaw))
            return Array.Empty<string>();

        var sanitized = AuswertungPro.Next.Application.Common.ProjectPathResolver.SanitizePathSegment(holdingRaw);
        return new[] { sanitized, holdingRaw }
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Waehlt aus mehreren PDF-Kandidaten den besten: bevorzugt einen Treffer mit
    /// Suffix "_&lt;token&gt;.pdf", sonst den lexikografisch letzten Dateinamen.
    /// (<see cref="Path.GetFileName(string?)"/> ist eine reine String-Operation.)
    /// </summary>
    public static string? PickBest(IEnumerable<string> candidates, IReadOnlyList<string> holdingTokens)
    {
        var list = candidates
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (list.Count == 0)
            return null;

        foreach (var token in holdingTokens)
        {
            var expectedSuffix = "_" + token + ".pdf";
            var exact = list
                .Where(path => Path.GetFileName(path).EndsWith(expectedSuffix, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (exact.Count > 0)
                return exact[0];
        }

        return list
            .OrderByDescending(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
            .First();
    }

    /// <summary>
    /// Parst die in den Projekt-Metadaten gespeicherte PDF-Liste (JSON-Array; faellt
    /// auf Semikolon-Trennung zurueck). Leer bei leerer Eingabe.
    /// </summary>
    public static IReadOnlyList<string> ParseStoredPathList(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return Array.Empty<string>();

        try
        {
            var parsed = JsonSerializer.Deserialize<List<string>>(raw);
            if (parsed is null)
                return Array.Empty<string>();

            return parsed
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .ToList();
        }
        catch
        {
            return raw.Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => x.Length > 0)
                .ToList();
        }
    }
}
