using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import;

internal static class ModernizerWorkflow
{
    public static ModernizeReport Run(Project project, ModernizeRequest request)
    {
        var report = new ModernizeReport();

        if (!request.DryRun)
        {
            ProjectStructure.EnsureCreated(request.ProjectFolder);
            ModernizerPreparation.CreateProjectBackup(request.ProjectFile, request.ProjectFolder, report);
        }
        else
        {
            report.Messages.Add("Dry-Run: Es werden keine Dateien geschrieben.");
        }

        ModernizerPreparation.EnsureCurrentFolders(request.ProjectFolder, request.DryRun, report);
        if (!request.FlattenOnly)
            ImportLegacySources(project, request, report);
        else
            report.Messages.Add("Flatten-only: Es wird nicht neu importiert, nur Haltungen_Verteilt wird bereinigt.");

        ModernizerFlattener.FlattenHaltungenVerteilt(project, request.ProjectFolder, request.DryRun, report);
        ExternalProjectLinkSanitizer.SanitizeMetadataLinks(project, request.ProjectFolder, request.DryRun, report);
        ProtocolSnapshotLinkSanitizer.SanitizeProtocolChangeSnapshots(project, request.ProjectFolder, request.DryRun, report);

        return report;
    }

    private static void ImportLegacySources(Project project, ModernizeRequest request, ModernizeReport report)
    {
        ModernizerPreparation.CopyLegacyImports(request.ProjectFolder, request.DryRun, report);
        ModernizerPreparation.CopyPlanFolders(request.ProjectFolder, request.SourceFolder, request.DryRun, report);
        ModernizerPreparation.CopyLegacyTree(request.ProjectFolder, ModernizerLegacyFolders.Haltungen, ProjectStructure.HaltungenVerteilt, request.DryRun, report);
        CopyLegacySchachtTrees(request, report);
        ModernizerPreparation.CopyTopLevelFotos(request.ProjectFolder, request.DryRun, report);

        var sourceVideos = ModernizerSourceIndexBuilder.BuildSourceVideoIndex(request.SourceFolder);
        var externalFiles = ModernizerSourceIndexBuilder.BuildExternalFileIndex(request.ProjectFolder, request.SourceFolder);
        ModernizerRelinker.RelinkHaltungen(project, request.ProjectFolder, sourceVideos, externalFiles, request.DryRun, report);
        ModernizerRelinker.RelinkSchaechte(project, request.ProjectFolder, externalFiles, request.DryRun, report);
        ModernizerMetadataUpdater.UpdateStoredImportMetadata(project, request.DryRun, report);
    }

    private static void CopyLegacySchachtTrees(ModernizeRequest request, ModernizeReport report)
    {
        foreach (var legacyFolderName in ModernizerLegacyFolders.SchachtFolders)
            ModernizerPreparation.CopyLegacyTree(
                request.ProjectFolder,
                legacyFolderName,
                ProjectStructure.SchaechteVerteilt,
                request.DryRun,
                report);
    }
}
