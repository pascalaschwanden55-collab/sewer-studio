using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import;
using AuswertungPro.Next.Infrastructure.Media;

internal static class ModernizerRelinker
{
    public static void RelinkHaltungen(
        Project project,
        string projectFolder,
        IReadOnlyDictionary<string, List<string>> sourceVideos,
        IReadOnlyDictionary<string, List<string>> externalFiles,
        bool dryRun,
        ModernizeReport report)
    {
        foreach (var record in project.Data)
        {
            var haltung = record.GetFieldValue(FieldKeys.HoldingName).Trim();
            if (string.IsNullOrWhiteSpace(haltung))
                continue;

            var san = ProjectPathResolver.SanitizePathSegment(haltung);
            var holdingRoot = ProjectStructure.HaltungVerteiltDir(projectFolder, san);
            var context = new ModernizerRelinkContext(
                holdingRoot,
                projectFolder,
                externalFiles,
                dryRun,
                report,
                FileCopyKind.Haltung);

            RelinkHaltungPathFields(record, context);

            ModernizerMissingVideoRelinker.TryRelinkSingleSourceVideo(
                record,
                haltung,
                holdingRoot,
                projectFolder,
                sourceVideos,
                dryRun,
                report);

            ModernizerPhotoRelinker.RelinkProtocol(record.Protocol, context);
            if (record.VsaFindings is not null)
            {
                foreach (var finding in record.VsaFindings)
                    ModernizerPhotoRelinker.RelinkFindingPhoto(finding, context);
            }
        }
    }

    public static void RelinkSchaechte(
        Project project,
        string projectFolder,
        IReadOnlyDictionary<string, List<string>> externalFiles,
        bool dryRun,
        ModernizeReport report)
    {
        foreach (var schacht in project.SchaechteData)
        {
            var number = FirstNonEmpty(ModernizerProjectKeys.SchachtNumberFields
                .Select(schacht.GetFieldValue)
                .ToArray());
            if (string.IsNullOrWhiteSpace(number))
                continue;

            var san = ProjectPathResolver.SanitizePathSegment(number);
            var root = ProjectStructure.SchachtVerteiltDir(projectFolder, san);
            var context = new ModernizerRelinkContext(
                root,
                projectFolder,
                externalFiles,
                dryRun,
                report,
                FileCopyKind.Schacht);

            RelinkSchachtPathFields(schacht, context);
            ModernizerPhotoRelinker.RelinkProtocol(schacht.Protocol, context);
        }
    }

    private static void RelinkHaltungPathFields(
        HaltungRecord record,
        ModernizerRelinkContext context)
    {
        foreach (var spec in ModernizerRecordFieldSpecs.HaltungPathFields)
        {
            if (spec.IsList)
            {
                ModernizerFieldRelinker.RelinkHaltungFieldList(
                    record,
                    spec.Field,
                    context,
                    spec.Predicate);
                continue;
            }

            ModernizerFieldRelinker.RelinkHaltungField(
                record,
                spec.Field,
                context,
                spec.Predicate);
        }
    }

    private static void RelinkSchachtPathFields(
        SchachtRecord schacht,
        ModernizerRelinkContext context)
    {
        foreach (var spec in ModernizerRecordFieldSpecs.SchachtPathFields)
        {
            ModernizerFieldRelinker.RelinkSchachtField(
                schacht,
                spec.Field,
                context,
                spec.Predicate);
        }
    }

    private static string FirstNonEmpty(params string[] values)
        => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim() ?? "";
}
