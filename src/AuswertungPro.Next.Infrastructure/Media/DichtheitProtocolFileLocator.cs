using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.DataPage;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Media;

/// <summary>
/// Dateibasierte Suche nach verteilten Dichtheitspruefungsprotokollen.
/// Der letzte Haltungsordner bleibt auch bei frei konfigurierten Ueberordnern fest.
/// </summary>
public sealed class DichtheitProtocolFileLocator : IDichtheitProtocolFileLocator
{
    public IReadOnlyList<string> FindPdfPaths(
        HaltungRecord? record,
        string? projectFolder,
        string? configuredRoot)
    {
        if (record is null)
            return Array.Empty<string>();

        var haltung = (record.GetFieldValue(FieldKeys.HoldingName) ?? string.Empty).Trim();
        if (haltung.Length == 0)
            return Array.Empty<string>();

        var roots = BuildSearchRoots(projectFolder, configuredRoot);
        if (roots.Count == 0)
            return Array.Empty<string>();

        var safeHoldingName = ProjectPathResolver.SanitizePathSegment(haltung);
        return roots
            .SelectMany(root => Common.SafeFileEnumeration.EnumerateFilesSafe(
                root,
                "*_DP*.pdf",
                recursive: true))
            .Where(path => string.Equals(
                Path.GetFileName(Path.GetDirectoryName(path)),
                safeHoldingName,
                StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlyList<string> BuildSearchRoots(
        string? projectFolder,
        string? configuredRoot)
    {
        var roots = new List<string>();
        if (!string.IsNullOrWhiteSpace(configuredRoot))
            roots.Add(configuredRoot.Trim());
        if (!string.IsNullOrWhiteSpace(projectFolder))
            roots.Add(Path.Combine(projectFolder.Trim(), "Haltungen_Verteilt"));

        return roots
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
