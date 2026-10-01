using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Projects;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Infrastructure.Media;

namespace AuswertungPro.Next.Infrastructure.Projects;

public sealed class ProjectPhotoReferenceNormalizationService : IProjectPhotoReferenceNormalizer
{
    public int Normalize(Project? project, string? projectFilePath)
    {
        if (project is null || string.IsNullOrWhiteSpace(projectFilePath))
            return 0;

        var projectRoot = ProjectFileLocator.ProjectRootFromFile(projectFilePath)
                          ?? Path.GetDirectoryName(projectFilePath);
        if (string.IsNullOrWhiteSpace(projectRoot))
            return 0;

        var changed = 0;
        foreach (var record in project.Data)
        {
            var holding = record.GetFieldValue(FieldKeys.HoldingName);
            var holdingSan = ProjectPathResolver.SanitizePathSegment(holding ?? string.Empty);
            if (string.IsNullOrWhiteSpace(holdingSan))
                continue;

            changed += NormalizeVsaFindingPhotos(record, projectRoot, holdingSan);
            if (record.Protocol is not null)
                changed += NormalizeProtocolPhotos(record.Protocol, projectRoot, holdingSan);
        }

        if (changed > 0)
            project.Dirty = true;

        return changed;
    }

    private static int NormalizeVsaFindingPhotos(HaltungRecord record, string projectRoot, string holdingSan)
    {
        var changed = 0;
        foreach (var finding in record.VsaFindings)
        {
            var normalized = TryResolveCentralPhoto(finding.FotoPath, projectRoot, holdingSan);
            if (string.IsNullOrWhiteSpace(normalized)
                || string.Equals(finding.FotoPath, normalized, StringComparison.OrdinalIgnoreCase))
                continue;

            finding.FotoPath = normalized;
            changed++;
        }

        return changed;
    }

    private static int NormalizeProtocolPhotos(ProtocolDocument protocol, string projectRoot, string holdingSan)
    {
        var changed = 0;
        changed += NormalizeRevisionPhotos(protocol.Original, projectRoot, holdingSan);
        changed += NormalizeRevisionPhotos(protocol.Current, projectRoot, holdingSan);
        foreach (var revision in protocol.History)
            changed += NormalizeRevisionPhotos(revision, projectRoot, holdingSan);
        return changed;
    }

    private static int NormalizeRevisionPhotos(ProtocolRevision revision, string projectRoot, string holdingSan)
    {
        var changed = 0;
        foreach (var entry in revision.Entries)
        {
            for (var i = 0; i < entry.FotoPaths.Count; i++)
            {
                var normalized = TryResolveCentralPhoto(entry.FotoPaths[i], projectRoot, holdingSan);
                if (string.IsNullOrWhiteSpace(normalized)
                    || string.Equals(entry.FotoPaths[i], normalized, StringComparison.OrdinalIgnoreCase))
                    continue;

                entry.FotoPaths[i] = normalized;
                changed++;
            }

            changed += DeduplicatePhotoPaths(entry.FotoPaths);
        }

        return changed;
    }

    private static string? TryResolveCentralPhoto(string? rawPath, string projectRoot, string holdingSan)
    {
        if (string.IsNullOrWhiteSpace(rawPath))
            return null;
        if (ProjectPathResolver.IsRelative(rawPath) && !ProjectPathResolver.IsSafeRelativeProjectPath(rawPath))
            return null;

        var fileName = Path.GetFileName(rawPath.Replace('/', Path.DirectorySeparatorChar));
        if (string.IsNullOrWhiteSpace(fileName) || !MediaFileTypes.HasImageExtension(fileName))
            return null;

        var central = Path.Combine(projectRoot, "Fotos", "Haltungen", holdingSan, fileName);
        if (!File.Exists(central))
        {
            var renamed = FindUniqueRenamedHoldingPhoto(projectRoot, holdingSan, fileName);
            if (string.IsNullOrWhiteSpace(renamed))
                return null;

            return DarfUmstellen(rawPath, renamed, projectRoot)
                ? ProjectPathResolver.MakeRelative(renamed, projectRoot)
                : null;
        }

        return DarfUmstellen(rawPath, central, projectRoot)
            ? ProjectPathResolver.MakeRelative(central, projectRoot)
            : null;
    }

    /// <summary>
    /// Ein Dateiname ist kein Identitaetsnachweis. Zeigt der bestehende Verweis noch auf
    /// eine vorhandene, ANDERE Datei, wird nur bei belegt gleichem Inhalt umgestellt -
    /// sonst zeigte ein Befund nach dem Laden ein fremdes Bild (F3, 17.09.2026). Ein
    /// nicht mehr aufloesbarer Verweis bleibt der Reparaturfall dieses Dienstes.
    /// </summary>
    private static bool DarfUmstellen(string rawPath, string ziel, string projectRoot)
    {
        var bestehend = BestehendeDatei(rawPath, projectRoot);
        if (bestehend is null)
            return true;
        if (string.Equals(bestehend, Path.GetFullPath(ziel), StringComparison.OrdinalIgnoreCase))
            return true;

        try
        {
            return FileContentComparer.FilesEqual(bestehend, ziel);
        }
        catch
        {
            // Nicht lesbar heisst nicht "gleich": im Zweifel den bestehenden Verweis lassen.
            return false;
        }
    }

    private static string? BestehendeDatei(string rawPath, string projectRoot)
    {
        try
        {
            var pfad = rawPath.Trim().Replace('/', Path.DirectorySeparatorChar);
            var voll = Path.IsPathRooted(pfad)
                ? Path.GetFullPath(pfad)
                : Path.GetFullPath(Path.Combine(projectRoot, pfad));
            return File.Exists(voll) ? voll : null;
        }
        catch
        {
            return null;
        }
    }

    private static string? FindUniqueRenamedHoldingPhoto(string projectRoot, string holdingSan, string staleFileName)
    {
        var suffix = TryExtractTrailingPhotoSuffix(staleFileName);
        if (string.IsNullOrWhiteSpace(suffix))
            return null;

        var photoDir = Path.Combine(projectRoot, "Fotos", "Haltungen", holdingSan);
        if (!Directory.Exists(photoDir))
            return null;

        try
        {
            var matches = Directory.EnumerateFiles(photoDir, "*" + suffix, SearchOption.TopDirectoryOnly)
                .Where(path => Path.GetFileName(path).EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                .Take(2)
                .ToList();
            return matches.Count == 1 ? matches[0] : null;
        }
        catch
        {
            return null;
        }
    }

    private static string? TryExtractTrailingPhotoSuffix(string fileName)
    {
        var stem = Path.GetFileNameWithoutExtension(fileName);
        var ext = Path.GetExtension(fileName);
        if (string.IsNullOrWhiteSpace(stem) || string.IsNullOrWhiteSpace(ext))
            return null;

        var idx = stem.LastIndexOf('_');
        if (idx < 0 || idx == stem.Length - 1)
            return null;

        var number = stem[(idx + 1)..];
        if (number.Length == 0 || number.Any(ch => ch < '0' || ch > '9'))
            return null;

        return "_" + number + ext;
    }

    private static int DeduplicatePhotoPaths(IList<string> paths)
    {
        var removed = 0;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = paths.Count - 1; i >= 0; i--)
        {
            var key = NormalizePhotoPathKey(paths[i]);
            if (string.IsNullOrWhiteSpace(key))
                continue;
            if (seen.Add(key))
                continue;

            paths.RemoveAt(i);
            removed++;
        }

        return removed;
    }

    private static string NormalizePhotoPathKey(string path)
        => (path ?? string.Empty).Replace('\\', '/').Trim();
}
