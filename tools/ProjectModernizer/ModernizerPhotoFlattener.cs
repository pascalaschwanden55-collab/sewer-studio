using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Infrastructure.Import;
using AuswertungPro.Next.Infrastructure.Media;

internal static class ModernizerPhotoFlattener
{
    public static void FlattenProtocolPhotos(ProtocolDocument? protocol, ModernizerFlattenContext context)
    {
        if (protocol is null)
            return;

        FlattenRevisionPhotos(protocol.Original, context);
        FlattenRevisionPhotos(protocol.Current, context);
        foreach (var revision in protocol.History)
            FlattenRevisionPhotos(revision, context);
    }

    public static void FlattenFindingPhoto(VsaFinding finding, ModernizerFlattenContext context)
    {
        var raw = finding.FotoPath?.Trim();
        if (string.IsNullOrWhiteSpace(raw))
            return;

        if (!ModernizerFlattenedPathResolver.TryResolve(
                raw,
                context,
                MediaFileTypes.HasImageExtension,
                (_, source) => ModernizerStructureFiles.BuildCentralPhotoTarget(source, context.ProjectFolder, context.HoldingName),
                StructureMoveKind.CentralPhoto,
                $"VSA-Foto nicht aufgeloest fuer Flatten: {raw}",
                keepDirectChild: false,
                out var rel))
            return;

        if (!ModernizerPathComparison.PathValueEquals(raw, rel))
        {
            if (!context.DryRun)
                finding.FotoPath = rel;
            context.Report.RelinkedPaths++;
        }
    }

    private static void FlattenRevisionPhotos(ProtocolRevision revision, ModernizerFlattenContext context)
    {
        foreach (var entry in revision.Entries)
        {
            if (entry.FotoPaths is null || entry.FotoPaths.Count == 0)
                continue;

            var changed = false;
            for (var i = 0; i < entry.FotoPaths.Count; i++)
            {
                var raw = entry.FotoPaths[i]?.Trim();
                if (string.IsNullOrWhiteSpace(raw))
                    continue;

                if (!ModernizerFlattenedPathResolver.TryResolve(
                        raw,
                        context,
                        MediaFileTypes.HasImageExtension,
                        (_, source) => ModernizerStructureFiles.BuildCentralPhotoTarget(source, context.ProjectFolder, context.HoldingName),
                        StructureMoveKind.CentralPhoto,
                        $"Foto nicht aufgeloest fuer Flatten: {raw}",
                        keepDirectChild: false,
                        out var rel))
                    continue;

                if (!ModernizerPathComparison.PathValueEquals(raw, rel))
                {
                    if (!context.DryRun)
                        entry.FotoPaths[i] = rel;
                    changed = true;
                }
            }

            if (!context.DryRun && ModernizerPathComparison.DeduplicatePathValuesInPlace(entry.FotoPaths))
                changed = true;
            if (changed)
                context.Report.RelinkedPaths++;
        }
    }
}
