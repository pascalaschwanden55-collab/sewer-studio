using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import;
using AuswertungPro.Next.Infrastructure.Media;

internal static class ModernizerMissingVideoRelinker
{
    public static bool TryRelinkSingleSourceVideo(
        HaltungRecord record,
        string holdingName,
        string holdingRoot,
        string projectFolder,
        IReadOnlyDictionary<string, List<string>> sourceVideos,
        bool dryRun,
        ModernizeReport report)
    {
        if (ModernizerPathResolver.HasAnyFile(holdingRoot, MediaFileTypes.HasVideoExtension))
            return false;

        if (!sourceVideos.TryGetValue(holdingName, out var candidates) || candidates.Count != 1)
            return false;

        var copied = ModernizerFileSystem.CopyFileToDirectory(
            candidates[0],
            Path.Combine(holdingRoot, ModernizerLegacyFolders.HoldingVideo),
            dryRun,
            report,
            FileCopyKind.Haltung);
        if (string.IsNullOrWhiteSpace(copied))
            return false;

        var relative = ProjectPathResolver.MakeRelative(copied, projectFolder);
        if (dryRun)
        {
            report.RelinkedPaths++;
            return true;
        }

        return ModernizerRecordFieldUpdater.ForceSet(record, FieldKeys.Link, relative, report);
    }
}
