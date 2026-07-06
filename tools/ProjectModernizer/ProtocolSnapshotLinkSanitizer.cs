using System.Text.RegularExpressions;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Infrastructure.Import;

internal static class ProtocolSnapshotLinkSanitizer
{
    private static readonly Regex ExternalPhotoPathRegex = new(
        @"[A-Za-z]:(?:\\\\|\\|/)[^""\]\r\n;]*?\.(?:jpg|jpeg|png|bmp|gif|tif|tiff|webp)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static void SanitizeProtocolChangeSnapshots(Project project, string projectFolder, bool dryRun, ModernizeReport report)
    {
        foreach (var record in project.Data)
        {
            var haltung = record.GetFieldValue(FieldKeys.HoldingName).Trim();
            if (string.IsNullOrWhiteSpace(haltung) || record.Protocol is null)
                continue;

            var san = ProjectPathResolver.SanitizePathSegment(haltung);
            SanitizeRevisionSnapshots(record.Protocol.Original, projectFolder, san, dryRun, report);
            SanitizeRevisionSnapshots(record.Protocol.Current, projectFolder, san, dryRun, report);
            foreach (var revision in record.Protocol.History)
                SanitizeRevisionSnapshots(revision, projectFolder, san, dryRun, report);
        }
    }

    private static void SanitizeRevisionSnapshots(
        ProtocolRevision revision,
        string projectFolder,
        string san,
        bool dryRun,
        ModernizeReport report)
    {
        foreach (var change in revision.Changes)
        {
            change.Before = SanitizeSnapshot(change.Before, projectFolder, san, dryRun, report);
            change.After = SanitizeSnapshot(change.After, projectFolder, san, dryRun, report);
        }
    }

    private static string? SanitizeSnapshot(
        string? snapshot,
        string projectFolder,
        string san,
        bool dryRun,
        ModernizeReport report)
    {
        if (string.IsNullOrWhiteSpace(snapshot) || !ExternalPathDetector.ContainsExternalDrivePath(snapshot, projectFolder))
            return snapshot;

        var replacements = 0;
        var updated = ExternalPhotoPathRegex.Replace(
            snapshot,
            match =>
            {
                var replacement = ResolveProjectRelativeSnapshotPath(match.Value, projectFolder, san);
                if (replacement is null)
                {
                    replacements++;
                    report.UnresolvedPaths++;
                    report.Messages.Add($"Snapshot-Foto nicht gefunden: {match.Value}");
                    return "";
                }

                if (string.Equals(replacement, match.Value, StringComparison.Ordinal))
                    return match.Value;

                replacements++;
                return replacement;
            });

        if (replacements == 0)
            return snapshot;

        report.ExternalLinksRemoved += replacements;
        report.SnapshotLinksRemoved += replacements;
        if (dryRun)
            return snapshot;

        return updated;
    }

    private static string? ResolveProjectRelativeSnapshotPath(string raw, string projectFolder, string san)
    {
        var normalized = raw.Replace(@"\\", @"\").Replace('/', Path.DirectorySeparatorChar);
        var fileName = Path.GetFileName(normalized);
        if (string.IsNullOrWhiteSpace(fileName))
            return null;

        var central = Path.Combine(ProjectStructure.FotosHaltungDir(projectFolder, san), fileName);
        if (File.Exists(central))
            return ProjectPathResolver.MakeRelative(central, projectFolder);

        var fotosRoot = Path.Combine(projectFolder, ProjectStructure.Fotos, ProjectStructure.FotosHaltungen);
        if (Directory.Exists(fotosRoot))
        {
            var matches = ModernizerFileSystem.EnumerateFilesSafe(fotosRoot)
                .Where(p => string.Equals(Path.GetFileName(p), fileName, StringComparison.OrdinalIgnoreCase))
                .Take(2)
                .ToList();
            if (matches.Count == 1)
                return ProjectPathResolver.MakeRelative(matches[0], projectFolder);
        }

        return null;
    }
}
