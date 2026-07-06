using AuswertungPro.Next.Application.Common;

internal static class ModernizerFlattenedPathResolver
{
    public static bool TryResolve(
        string raw,
        ModernizerFlattenContext context,
        Func<string, bool> predicate,
        Func<string, string, string> buildTarget,
        StructureMoveKind moveKind,
        string unresolvedMessage,
        bool keepDirectChild,
        out string relative)
    {
        relative = "";
        if (ModernizerPathMap.TryGet(raw, context.PathMap, out relative))
            return true;

        var source = ModernizerStructureFileResolver.ResolveExistingFile(raw, context.ProjectFolder, predicate);
        if (source is null)
        {
            context.Report.UnresolvedPaths++;
            context.Report.Messages.Add(unresolvedMessage);
            return false;
        }

        if (keepDirectChild && ModernizerPathComparison.IsDirectChildFile(source, context.HoldingRoot))
        {
            relative = ProjectPathResolver.MakeRelative(source, context.ProjectFolder);
            ModernizerPathMap.Add(context.PathMap, raw, source, relative);
            return true;
        }

        var target = buildTarget(raw, source);
        var moved = ModernizerStructureFileMover.MoveOrCopyStructureFile(
            source,
            target,
            context.MoveRoot,
            context.DryRun,
            context.Report,
            moveKind);
        if (moved is null)
            return false;

        relative = ProjectPathResolver.MakeRelative(moved, context.ProjectFolder);
        ModernizerPathMap.Add(context.PathMap, raw, source, relative);
        return true;
    }
}
