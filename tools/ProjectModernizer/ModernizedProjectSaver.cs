using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Projects;
using AuswertungPro.Next.Domain.Models;

internal static class ModernizedProjectSaver
{
    public static Result Save(
        Project project,
        ModernizeRequest request,
        IProjectRepository repository,
        ModernizeReport report)
    {
        var canonical = ProjectFileLocator.TargetPath(request.ProjectFolder);
        Directory.CreateDirectory(Path.GetDirectoryName(canonical)!);

        var saveCanonical = repository.Save(project, canonical);
        if (!saveCanonical.Ok)
            return Result.Fail(
                ProjectModernizerExitCodes.SaveFailed.ToString(),
                $"Speichern in Projektdateien fehlgeschlagen: {saveCanonical.ErrorMessage}");

        SaveOriginalProjectFileWhenSafe(project, request, repository, canonical, report);
        ProjectFileLocator.WriteRootPointer(request.ProjectFolder, canonical);
        ModernizeReportWriter.Write(request.ProjectFolder, report);
        return Result.Success();
    }

    private static void SaveOriginalProjectFileWhenSafe(
        Project project,
        ModernizeRequest request,
        IProjectRepository repository,
        string canonical,
        ModernizeReport report)
    {
        if (ModernizerFileSystem.SameFullPath(request.ProjectFile, canonical))
            return;

        if (!ModernizerFileSystem.IsUnder(request.ProjectFile, request.ProjectFolder))
        {
            report.Messages.Add("Externe Projektdatei wurde nicht aktualisiert; gespeichert wurde nur die projektinterne Kopie.");
            return;
        }

        var saveOriginal = repository.Save(project, request.ProjectFile);
        if (!saveOriginal.Ok)
            report.Messages.Add($"Alte Root-Projektdatei nicht aktualisiert: {saveOriginal.ErrorMessage}");
    }
}
