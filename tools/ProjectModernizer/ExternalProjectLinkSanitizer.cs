using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import;
using AuswertungPro.Next.Infrastructure.Projects;

internal static class ExternalProjectLinkSanitizer
{
    public static void SanitizeMetadataLinks(Project project, string projectFolder, bool dryRun, ModernizeReport report)
    {
        foreach (var key in ModernizerProjectKeys.LogoPathMetadataKeys)
        {
            if (!project.Metadata.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
                continue;
            if (!ExternalPathDetector.IsExternalAbsolutePath(value, projectFolder))
                continue;

            var copied = TryCopyMetadataAssetIntoProject(value, projectFolder, dryRun, report);
            if (!dryRun)
                project.Metadata[key] = copied ?? "";
            report.MetadataUpdated++;
            report.ExternalLinksRemoved++;
            report.Messages.Add(copied is null
                ? $"{key}: externer/verlorener Pfad entfernt ({value})"
                : $"{key}: ins Projekt kopiert ({copied})");
        }

        if (project.Metadata.TryGetValue(ModernizerProjectKeys.ImportSource, out var importQuelle)
            && ExternalPathDetector.IsExternalAbsolutePath(importQuelle, projectFolder))
        {
            if (!dryRun)
                project.Metadata[ModernizerProjectKeys.ImportSource] = ProjectStructure.Importdateien;
            report.MetadataUpdated++;
            report.ExternalLinksRemoved++;
            report.Messages.Add($"{ModernizerProjectKeys.ImportSource} auf projektinternen Ordner gesetzt.");
        }

        if (project.Metadata.TryGetValue(ModernizerProjectKeys.ImportSourceHistory, out var historie)
            && ExternalPathDetector.ContainsExternalDrivePath(historie, projectFolder))
        {
            if (!dryRun)
                project.Metadata[ModernizerProjectKeys.ImportSourceHistory] =
                    ModernizerProjectKeys.ModernizedImportSourceHistory;
            report.MetadataUpdated++;
            report.ExternalLinksRemoved++;
            report.Messages.Add($"{ModernizerProjectKeys.ImportSourceHistory} von externen Pfaden bereinigt.");
        }
    }

    private static string? TryCopyMetadataAssetIntoProject(
        string value,
        string projectFolder,
        bool dryRun,
        ModernizeReport report)
    {
        try
        {
            if (!File.Exists(value))
                return null;

            var target = Path.Combine(projectFolder, ProjectStructure.Projektdateien, ModernizerProjectKeys.LogosFolder, Path.GetFileName(value));
            var copied = ModernizerFileSystem.CopyFileExact(value, target, dryRun, report, FileCopyKind.Import);
            return string.IsNullOrWhiteSpace(copied)
                ? null
                : ProjectPathResolver.MakeRelative(copied, projectFolder);
        }
        catch
        {
            return null;
        }
    }

}
