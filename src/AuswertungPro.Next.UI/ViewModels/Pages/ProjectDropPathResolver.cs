using System;
using System.IO;
using System.Linq;
using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

public static class ProjectDropPathResolver
{
    public static string? ResolveProjectFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        if (File.Exists(path))
            return string.Equals(Path.GetExtension(path), ".json", StringComparison.OrdinalIgnoreCase)
                ? path
                : null;

        if (!Directory.Exists(path))
            return null;

        var located = ProjectFileLocator.Locate(path);
        if (located is not null)
            return located;

        var jsonFiles = Directory.GetFiles(path, "*.json", SearchOption.TopDirectoryOnly);
        var namedProject = jsonFiles.FirstOrDefault(f =>
            Path.GetFileName(f).Contains("projekt", StringComparison.OrdinalIgnoreCase));
        if (namedProject is not null)
            return namedProject;

        return jsonFiles.Length == 1 ? jsonFiles[0] : null;
    }
}
