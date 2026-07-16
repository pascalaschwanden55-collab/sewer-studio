using AuswertungPro.Next.Application.Projects;
using AuswertungPro.Next.Infrastructure.Projects;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

public static class ProjectDropPathResolver
{
    private static readonly IProjectDropPathResolver Default = new ProjectDropFilePathResolver();

    internal static IProjectDropPathResolver CompatibilityService
        => Default;

    public static string? ResolveProjectFile(string path)
    {
        return CompatibilityService.ResolveProjectFile(path);
    }
}
