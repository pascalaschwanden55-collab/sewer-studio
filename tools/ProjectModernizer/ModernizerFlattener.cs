using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import;
using AuswertungPro.Next.Infrastructure.Media;

internal static class ModernizerFlattener
{
    public static void FlattenHaltungenVerteilt(Project project, string projectFolder, bool dryRun, ModernizeReport report)
    {
        var haltungenRoot = Path.Combine(projectFolder, ProjectStructure.HaltungenVerteilt);
        if (!Directory.Exists(haltungenRoot))
            return;

        var pathMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var processed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var record in project.Data)
        {
            var haltung = record.GetFieldValue(FieldKeys.HoldingName).Trim();
            if (string.IsNullOrWhiteSpace(haltung))
                continue;

            var san = ProjectPathResolver.SanitizePathSegment(haltung);
            var holdingRoot = ProjectStructure.HaltungVerteiltDir(projectFolder, san);
            processed.Add(Path.GetFullPath(holdingRoot));

            var context = new ModernizerFlattenContext(
                holdingRoot,
                projectFolder,
                san,
                ModernizerFileNaming.ResolveDateStamp(record),
                pathMap,
                dryRun,
                report);

            FlattenRecordPathFields(record, context);
            ModernizerPhotoFlattener.FlattenProtocolPhotos(record.Protocol, context);
            if (record.VsaFindings is not null)
            {
                foreach (var finding in record.VsaFindings)
                    ModernizerPhotoFlattener.FlattenFindingPhoto(finding, context);
            }

            if (Directory.Exists(holdingRoot))
            {
                FlattenLooseFilesInHoldingSubfolders(context);
                RemoveEmptyLegacySubfolders(context);
            }
        }

        foreach (var holdingRoot in Directory.GetDirectories(haltungenRoot))
        {
            var full = Path.GetFullPath(holdingRoot);
            if (processed.Contains(full))
                continue;

            var san = Path.GetFileName(holdingRoot);
            var context = new ModernizerFlattenContext(
                holdingRoot,
                projectFolder,
                san,
                ModernizerFileNaming.ResolveDateStampFromFolder(holdingRoot),
                pathMap,
                dryRun,
                report);

            FlattenLooseFilesInHoldingSubfolders(context);
            RemoveEmptyLegacySubfolders(context);
        }
    }

    private static void FlattenLooseFilesInHoldingSubfolders(ModernizerFlattenContext context)
    {
        foreach (var file in ModernizerFileSystem.EnumerateFilesSafe(context.HoldingRoot).ToList())
        {
            if (ModernizerPathComparison.PathEquals(Path.GetDirectoryName(file) ?? "", context.HoldingRoot))
                continue;

            if (MediaFileTypes.HasImageExtension(file))
            {
                var target = ModernizerStructureFiles.BuildCentralPhotoTarget(file, context.ProjectFolder, context.HoldingName);
                ModernizerStructureFileMover.MoveOrCopyStructureFile(
                    file,
                    target,
                    context.MoveRoot,
                    context.DryRun,
                    context.Report,
                    StructureMoveKind.CentralPhoto);
                continue;
            }

            if (ModernizerStructureFiles.IsPdf(file) || MediaFileTypes.HasVideoExtension(file))
            {
                var target = ModernizerStructureFiles.BuildFlatLooseTarget(
                    file,
                    context.HoldingRoot,
                    context.HoldingName,
                    context.DateStamp);
                ModernizerStructureFileMover.MoveOrCopyStructureFile(
                    file,
                    target,
                    context.MoveRoot,
                    context.DryRun,
                    context.Report,
                    StructureMoveKind.FlatMedia);
            }
        }
    }

    private static void RemoveEmptyLegacySubfolders(ModernizerFlattenContext context)
    {
        foreach (var name in ModernizerLegacyFolders.HoldingSubfolders)
        {
            var dir = Path.Combine(context.HoldingRoot, name);
            if (!ModernizerEmptyDirectoryCleaner.TryDeleteDirectoryTreeIfEmpty(dir, context.DryRun))
                continue;

            context.Report.FoldersRemoved++;
        }
    }

    private static void FlattenRecordPathFields(HaltungRecord record, ModernizerFlattenContext context)
    {
        foreach (var spec in ModernizerRecordFieldSpecs.HaltungPathFields)
        {
            if (spec.IsList)
            {
                ModernizerFieldFlattener.FlattenRecordFieldList(record, spec.Field, context, spec.Predicate);
                continue;
            }

            ModernizerFieldFlattener.FlattenRecordField(record, spec.Field, context, spec.Predicate);
        }
    }
}
