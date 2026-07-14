using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Projects;

namespace AuswertungPro.Next.Infrastructure.Projects;

public sealed class ProjectDropFilePathResolver : IProjectDropPathResolver
{
    public string? ResolveProjectFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        if (File.Exists(path))
        {
            return string.Equals(Path.GetExtension(path), ".json", StringComparison.OrdinalIgnoreCase)
                ? path
                : null;
        }

        if (!Directory.Exists(path))
            return null;

        var located = LocateProjectFile(path);
        if (located is not null)
            return located;

        var jsonFiles = Directory.GetFiles(path, "*.json", SearchOption.TopDirectoryOnly);
        var namedProject = jsonFiles.FirstOrDefault(file =>
            Path.GetFileName(file).Contains("projekt", StringComparison.OrdinalIgnoreCase));
        if (namedProject is not null)
            return namedProject;

        return jsonFiles.Length == 1 ? jsonFiles[0] : null;
    }

    private static string? LocateProjectFile(string projectFolder)
    {
        var inSubfolder = Path.Combine(
            projectFolder,
            ProjectFileLocator.ProjektdateienDir,
            ProjectFileLocator.ProjectFileName);
        if (File.Exists(inSubfolder))
            return inSubfolder;

        var inRoot = Path.Combine(projectFolder, ProjectFileLocator.ProjectFileName);
        return File.Exists(inRoot) ? inRoot : null;
    }
}
