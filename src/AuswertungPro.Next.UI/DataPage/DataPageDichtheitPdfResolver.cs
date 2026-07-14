using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.DataPage;

/// <summary>
/// Findet die verteilten Dichtheitspruefungsprotokolle einer Haltung
/// auch unter konfigurierten Ueberordnern — neueste zuerst (Datumspraefix im Namen).
/// Grundlage fuer den Kontextmenuepunkt "Dichtheitspruefung (PDF) oeffnen".
/// </summary>
public static class DataPageDichtheitPdfResolver
{
    public static IReadOnlyList<string> Resolve(HaltungRecord? record, string? projectFolder)
        => Resolve(record, projectFolder, configuredRoot: null);

    public static IReadOnlyList<string> Resolve(
        HaltungRecord? record,
        string? projectFolder,
        string? configuredRoot)
    {
        if (record is null)
            return Array.Empty<string>();

        var haltung = (record.GetFieldValue(FieldKeys.HoldingName) ?? "").Trim();
        if (haltung.Length == 0)
            return Array.Empty<string>();

        var roots = new List<string>();
        if (!string.IsNullOrWhiteSpace(configuredRoot))
            roots.Add(configuredRoot.Trim());
        if (!string.IsNullOrWhiteSpace(projectFolder))
            roots.Add(Path.Combine(projectFolder.Trim(), "Haltungen_Verteilt"));
        if (roots.Count == 0)
            return Array.Empty<string>();

        var san = ProjectPathResolver.SanitizePathSegment(haltung);

        // Der letzte Haltungsordner bleibt auch beim konfigurierten Baum fest. Deshalb
        // reicht eine sichere rekursive Suche nach DP-Dateien in genau diesem Ordner.
        return roots
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .SelectMany(root => SafeFileEnumeration.EnumerateFilesSafe(root, "*_DP*.pdf", recursive: true))
            .Where(path => string.Equals(
                Path.GetFileName(Path.GetDirectoryName(path)),
                san,
                StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
