using System.IO;

namespace AuswertungPro.Next.UI.Ai.Vsa;

public sealed record VsaCodeExplorerPhotoPreview(
    string? Photo1Path,
    bool ShowPhoto1Placeholder,
    string? Photo2Path,
    bool ShowPhoto2Placeholder);

public static class VsaCodeExplorerPhotoPreviewPlanner
{
    public static VsaCodeExplorerPhotoPreview Plan(IReadOnlyList<string> photoPaths)
        => Plan(photoPaths, File.Exists);

    public static VsaCodeExplorerPhotoPreview Plan(
        IReadOnlyList<string> photoPaths,
        Func<string, bool> fileExists)
    {
        ArgumentNullException.ThrowIfNull(photoPaths);
        ArgumentNullException.ThrowIfNull(fileExists);

        var photo1Path = ResolveExistingPhotoPath(photoPaths, 0, fileExists);
        var photo2Path = ResolveExistingPhotoPath(photoPaths, 1, fileExists);

        return new VsaCodeExplorerPhotoPreview(
            Photo1Path: photo1Path,
            ShowPhoto1Placeholder: photo1Path is null,
            Photo2Path: photo2Path,
            ShowPhoto2Placeholder: photo2Path is null);
    }

    private static string? ResolveExistingPhotoPath(
        IReadOnlyList<string> photoPaths,
        int index,
        Func<string, bool> fileExists)
    {
        if (photoPaths.Count <= index)
            return null;

        var path = photoPaths[index];
        return string.IsNullOrEmpty(path) || !fileExists(path) ? null : path;
    }
}
