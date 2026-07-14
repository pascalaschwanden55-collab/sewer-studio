using System;
using System.Threading;
using AuswertungPro.Next.Application.Projects;
using AuswertungPro.Next.Infrastructure.Projects;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

public static class ProjectDropPathResolver
{
    private static IProjectDropPathResolver _current = new ProjectDropFilePathResolver();

    internal static IProjectDropPathResolver CompatibilityService
        => Volatile.Read(ref _current);

    internal static void Use(IProjectDropPathResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        Volatile.Write(ref _current, resolver);
    }

    public static string? ResolveProjectFile(string path)
    {
        return CompatibilityService.ResolveProjectFile(path);
    }
}
