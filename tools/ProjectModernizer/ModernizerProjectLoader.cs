using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Projects;

internal static class ModernizerProjectLoader
{
    public static Result<ModernizerProjectContext> Load(ModernizeOptions options, IProjectRepository repository)
    {
        var projectFolder = Path.GetFullPath(options.ProjectFolder);
        var projectFile = options.ProjectFile is not null
            ? Path.GetFullPath(options.ProjectFile)
            : FindProjectFile(projectFolder);

        if (string.IsNullOrWhiteSpace(projectFile) || !File.Exists(projectFile))
            return Result<ModernizerProjectContext>.Fail(
                ProjectModernizerExitCodes.MissingProjectFile.ToString(),
                "Projektdatei nicht gefunden.");

        var sourceFolder = string.IsNullOrWhiteSpace(options.SourceFolder)
            ? null
            : Path.GetFullPath(options.SourceFolder!);

        var loaded = repository.Load(projectFile);
        if (!loaded.Ok || loaded.Value is null)
            return Result<ModernizerProjectContext>.Fail(
                ProjectModernizerExitCodes.LoadFailed.ToString(),
                $"Projekt konnte nicht geladen werden: {loaded.ErrorMessage}");

        return Result<ModernizerProjectContext>.Success(new ModernizerProjectContext(
            loaded.Value,
            new ModernizeRequest(projectFolder, projectFile, sourceFolder, options.DryRun, options.FlattenOnly)));
    }

    public static string? FindProjectFile(string projectFolder)
    {
        var located = ProjectFileLocator.Locate(projectFolder);
        if (!string.IsNullOrWhiteSpace(located))
            return located;

        return Directory.Exists(projectFolder)
            ? Directory.GetFiles(projectFolder, "*.json", SearchOption.TopDirectoryOnly)
                .OrderByDescending(p => new FileInfo(p).LastWriteTimeUtc)
                .FirstOrDefault()
            : null;
    }
}
