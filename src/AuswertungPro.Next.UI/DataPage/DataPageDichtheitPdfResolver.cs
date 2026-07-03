using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.DataPage;

/// <summary>
/// Findet die verteilten Dichtheitspruefungsprotokolle einer Haltung
/// (Haltungen_Verteilt\&lt;H&gt;\*_DP*.pdf) — neueste zuerst (Datumspraefix im Namen).
/// Grundlage fuer den Kontextmenuepunkt "Dichtheitspruefung (PDF) oeffnen".
/// </summary>
public static class DataPageDichtheitPdfResolver
{
    public static IReadOnlyList<string> Resolve(HaltungRecord? record, string? projectFolder)
    {
        if (record is null || string.IsNullOrWhiteSpace(projectFolder))
            return Array.Empty<string>();

        var haltung = (record.GetFieldValue(FieldKeys.HoldingName) ?? "").Trim();
        if (haltung.Length == 0)
            return Array.Empty<string>();

        var san = ProjectPathResolver.SanitizePathSegment(haltung);
        var dir = Path.Combine(projectFolder, "Haltungen_Verteilt", san);
        if (!Directory.Exists(dir))
            return Array.Empty<string>();

        try
        {
            // *_DP.pdf inkl. Duplikat-Suffixe (*_DP_01.pdf); Datumspraefix sortiert neueste nach vorne.
            return Directory.EnumerateFiles(dir, "*_DP*.pdf", SearchOption.TopDirectoryOnly)
                .OrderByDescending(p => Path.GetFileName(p), StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }
}
