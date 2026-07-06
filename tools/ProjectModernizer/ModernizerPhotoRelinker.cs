using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Infrastructure.Media;

internal static class ModernizerPhotoRelinker
{
    public static void RelinkProtocol(
        ProtocolDocument? protocol,
        ModernizerRelinkContext context)
    {
        if (protocol is null)
            return;

        RelinkRevision(protocol.Original, context);
        RelinkRevision(protocol.Current, context);
        foreach (var rev in protocol.History)
            RelinkRevision(rev, context);
    }

    public static void RelinkFindingPhoto(
        VsaFinding finding,
        ModernizerRelinkContext context)
    {
        if (string.IsNullOrWhiteSpace(finding.FotoPath))
            return;

        if (ModernizerPathResolver.TryResolveOrCopyModernPath(
                finding.FotoPath,
                context.Root,
                context.ProjectFolder,
                MediaFileTypes.HasImageExtension,
                context.ExternalFiles,
                context.DryRun,
                context.Report,
                context.CopyKind,
                out var rel))
        {
            if (!context.DryRun)
                finding.FotoPath = rel;
            context.Report.RelinkedPaths++;
        }
        else
        {
            context.Report.UnresolvedPaths++;
            context.Report.Messages.Add($"VSA-Foto nicht aufgeloest: {finding.FotoPath}");
        }
    }

    private static void RelinkRevision(
        ProtocolRevision revision,
        ModernizerRelinkContext context)
    {
        foreach (var entry in revision.Entries)
        {
            for (var i = 0; i < entry.FotoPaths.Count; i++)
            {
                var raw = entry.FotoPaths[i];
                if (string.IsNullOrWhiteSpace(raw))
                    continue;

                if (ModernizerPathResolver.TryResolveOrCopyModernPath(
                        raw,
                        context.Root,
                        context.ProjectFolder,
                        MediaFileTypes.HasImageExtension,
                        context.ExternalFiles,
                        context.DryRun,
                        context.Report,
                        context.CopyKind,
                        out var rel))
                {
                    if (!context.DryRun)
                        entry.FotoPaths[i] = rel;
                    context.Report.RelinkedPaths++;
                }
                else
                {
                    context.Report.UnresolvedPaths++;
                    context.Report.Messages.Add($"Foto nicht aufgeloest: {raw}");
                }
            }
        }
    }
}
