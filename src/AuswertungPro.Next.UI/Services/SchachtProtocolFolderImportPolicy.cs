using System.Globalization;
using System.IO;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Import;

namespace AuswertungPro.Next.UI.Services;

internal sealed record SchachtProtocolFolderCandidate(
    string PdfPath,
    SchachtProtocolParseResult ParseResult);

/// <summary>
/// Kleine, UI-freie Regeln fuer den Schachtprotokoll-Ordnerimport. Die Klasse
/// haelt rekursive Dateisuche und Mehrfachfund-Auswahl aus dem ViewModel heraus.
/// </summary>
internal static class SchachtProtocolFolderImportPolicy
{
    internal static IReadOnlyList<string> FindPdfFiles(
        string sourceFolder,
        string projectDistributionFolder,
        ICollection<string>? skippedDirectories = null)
        => FindPdfFiles(
            sourceFolder,
            new[] { projectDistributionFolder },
            skippedDirectories);

    internal static IReadOnlyList<string> FindPdfFiles(
        string sourceFolder,
        IEnumerable<string> excludedFolders,
        ICollection<string>? skippedDirectories = null)
    {
        var exclusions = excludedFolders
            .Where(folder => !string.IsNullOrWhiteSpace(folder))
            // Wird das Projektziel selbst ausgewaehlt, darf es nicht zugleich
            // aus der Suche entfernt werden.
            .Where(folder => !IsSameOrBelow(sourceFolder, folder))
            .ToArray();

        return
            SafeFileEnumeration.EnumerateFilesSafe(
                    sourceFolder,
                    "*.pdf",
                    recursive: true,
                    skippedDirectories)
                .Where(path => exclusions.All(folder => !IsSameOrBelow(path, folder)))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
    }

    /// <summary>
    /// Bei mehreren Protokollen desselben Schachts wird fuer die Stammdaten das
    /// neueste Protokolldatum verwendet. Alle PDFs bleiben trotzdem verteilt.
    /// </summary>
    internal static IReadOnlyList<SchachtProtocolFolderCandidate> SelectCurrentPerShaft(
        IEnumerable<SchachtProtocolFolderCandidate> candidates)
        => candidates
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate.ParseResult.Schachtnummer))
            .GroupBy(
                candidate => candidate.ParseResult.Schachtnummer!.Trim(),
                StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderByDescending(candidate => ParseProtocolDate(candidate.ParseResult.Datum, candidate.PdfPath))
                .ThenBy(candidate => candidate.PdfPath, StringComparer.OrdinalIgnoreCase)
                .First())
            .OrderBy(candidate => candidate.ParseResult.Schachtnummer, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    internal static string BuildFolderImportSummary(
        int sourcePdfCount,
        int preparedPdfCount,
        int created,
        int updated,
        int skippedOlderPdfCandidates,
        int skippedDirectoryCount,
        IReadOnlyList<string> failures)
    {
        var lines = new List<string>
        {
            $"Gefundene PDF-Dateien: {sourcePdfCount}",
            $"Eingelesene Schachtprotokolle: {preparedPdfCount}",
            $"Schaechte neu angelegt: {created}",
            $"Schaechte aktualisiert: {updated}"
        };

        if (skippedOlderPdfCandidates > 0)
        {
            lines.Add(
                $"Aeltere PDF-Kandidaten uebersprungen: {skippedOlderPdfCandidates} " +
                "(sie bleiben erhalten; Stammdaten stammen aus dem neuesten Protokoll)");
        }
        if (skippedDirectoryCount > 0)
            lines.Add($"Nicht lesbare Unterordner uebersprungen: {skippedDirectoryCount}");
        if (failures.Count > 0)
        {
            lines.Add($"Fehler: {failures.Count}");
            lines.AddRange(failures.Take(8).Select(failure => $"- {failure}"));
            if (failures.Count > 8)
                lines.Add($"- ... und {failures.Count - 8} weitere");
        }

        return string.Join(Environment.NewLine, lines);
    }

    internal static string? ResolveCanonicalShaftFolder(
        string pdfPath,
        params string[] distributionRoots)
        => ResolveCanonicalShaftFolder(
            pdfPath,
            parsedShaftNumber: null,
            existingShaftNumbers: Array.Empty<string>(),
            distributionRoots);

    internal static string? ResolveCanonicalShaftFolder(
        string pdfPath,
        string? parsedShaftNumber,
        IEnumerable<string>? existingShaftNumbers,
        params string[] distributionRoots)
    {
        var parentFolder = Path.GetDirectoryName(pdfPath);
        if (string.IsNullOrWhiteSpace(parentFolder))
            return null;

        var knownFolderNames = BuildFolderNameSet(existingShaftNumbers);
        var parsedFolderNames = BuildFolderNameSet(
            string.IsNullOrWhiteSpace(parsedShaftNumber)
                ? Array.Empty<string>()
                : new[] { parsedShaftNumber });

        foreach (var root in distributionRoots)
        {
            var ancestors = GetAncestorsBelowRoot(parentFolder, root);
            if (ancestors is null)
                continue;
            if (ancestors.Count == 0)
                return null;

            var knownMatch = ancestors.FirstOrDefault(knownFolderNames.Contains);
            if (!string.IsNullOrWhiteSpace(knownMatch))
                return knownMatch;

            var parsedMatch = ancestors.FirstOrDefault(parsedFolderNames.Contains);
            if (!string.IsNullOrWhiteSpace(parsedMatch))
                return parsedMatch;

            var renovationIndex = ancestors.FindIndex(LooksLikeRenovationFolder);
            if (renovationIndex >= 0 && renovationIndex + 1 < ancestors.Count)
                return ancestors[renovationIndex + 1];

            // Ohne Projekt- oder PDF-Abgleich ist nur ein Objektordner eindeutig,
            // der direkt unter der bekannten Verteilwurzel liegt.
            if (ancestors.Count == 1 && !LooksLikeRenovationFolder(ancestors[0]))
                return ancestors[0];

            return null;
        }

        return Path.GetFileName(Path.TrimEndingDirectorySeparator(parentFolder));
    }

    private static HashSet<string> BuildFolderNameSet(IEnumerable<string>? values)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (values is null)
            return result;

        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
                continue;

            var trimmed = value.Trim();
            result.Add(trimmed);
            result.Add(ProjectPathResolver.SanitizePathSegment(trimmed));
        }

        return result;
    }

    private static List<string>? GetAncestorsBelowRoot(string parentFolder, string root)
    {
        try
        {
            var fullRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
            var current = Path.TrimEndingDirectorySeparator(Path.GetFullPath(parentFolder));
            if (!IsSameOrBelow(current, fullRoot))
                return null;

            var ancestors = new List<string>();
            while (!PathsEqual(current, fullRoot))
            {
                var folderName = Path.GetFileName(current);
                if (string.IsNullOrWhiteSpace(folderName))
                    return null;

                ancestors.Add(folderName);
                var next = Path.GetDirectoryName(current);
                if (string.IsNullOrWhiteSpace(next) || PathsEqual(next, current))
                    return null;
                current = next;
            }

            return ancestors;
        }
        catch
        {
            return null;
        }
    }

    private static bool LooksLikeRenovationFolder(string folderName)
        => folderName.Contains("_Saniert ", StringComparison.OrdinalIgnoreCase)
           || folderName.EndsWith("_Saniert", StringComparison.OrdinalIgnoreCase)
           || folderName.StartsWith("Saniert ", StringComparison.OrdinalIgnoreCase)
           || string.Equals(folderName, "Saniert", StringComparison.OrdinalIgnoreCase);

    private static DateTime ParseProtocolDate(string? rawDate, string pdfPath)
    {
        var formats = new[] { "dd.MM.yyyy", "dd.MM.yy", "yyyy-MM-dd", "yyyyMMdd" };
        if (!string.IsNullOrWhiteSpace(rawDate)
            && DateTime.TryParseExact(
                rawDate.Trim(),
                formats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsed))
        {
            return parsed;
        }

        var fileName = Path.GetFileNameWithoutExtension(pdfPath);
        var stamp = fileName.Length >= 8 ? fileName[..8] : string.Empty;
        return DateTime.TryParseExact(
            stamp,
            "yyyyMMdd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out parsed)
            ? parsed
            : DateTime.MinValue;
    }

    internal static bool IsSameOrBelow(string path, string folder)
    {
        try
        {
            var fullPath = Path.GetFullPath(path);
            var fullFolder = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder));
            if (string.Equals(fullPath, fullFolder, StringComparison.OrdinalIgnoreCase))
                return true;

            var prefix = fullFolder + Path.DirectorySeparatorChar;
            return fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static bool PathsEqual(string left, string right)
    {
        try
        {
            return string.Equals(
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(left)),
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)),
                StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}
