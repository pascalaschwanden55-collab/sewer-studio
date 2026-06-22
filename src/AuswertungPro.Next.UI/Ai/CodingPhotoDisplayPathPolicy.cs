using System.IO;

namespace AuswertungPro.Next.UI.Ai;

public static class CodingPhotoDisplayPathPolicy
{
    public static IReadOnlyList<string> BuildDisplayPhotoPaths(
        string? evidencePreviewPath,
        IEnumerable<string> photoPaths,
        Func<string, bool> fileExists)
    {
        var displayPaths = new List<string>();
        if (!string.IsNullOrWhiteSpace(evidencePreviewPath) && fileExists(evidencePreviewPath))
            displayPaths.Add(evidencePreviewPath);

        foreach (var photoPath in photoPaths)
        {
            if (!string.IsNullOrWhiteSpace(photoPath)
                && !displayPaths.Contains(photoPath, StringComparer.OrdinalIgnoreCase))
            {
                displayPaths.Add(photoPath);
            }
        }

        return displayPaths;
    }

    public static string? ResolveExistingPath(
        string photoPath,
        string projectFolder,
        Func<string, bool> fileExists)
    {
        if (string.IsNullOrWhiteSpace(photoPath))
            return null;

        if (Path.IsPathRooted(photoPath) && fileExists(photoPath))
            return photoPath;

        var projectPath = Path.Combine(projectFolder, photoPath);
        return fileExists(projectPath) ? projectPath : null;
    }
}
