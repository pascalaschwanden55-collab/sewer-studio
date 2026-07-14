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
}
