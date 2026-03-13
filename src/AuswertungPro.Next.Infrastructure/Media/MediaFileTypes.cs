using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AuswertungPro.Next.Infrastructure.Media;

public static class MediaFileTypes
{
    public static readonly string[] VideoExtensions =
    {
        ".mp2", ".mpg", ".mpeg", ".mp4", ".avi", ".mov", ".wmv", ".mkv"
    };

    public static readonly string[] ImageExtensions =
    {
        ".jpg", ".jpeg", ".png", ".bmp", ".webp", ".tif", ".tiff"
    };

    public static readonly string VideoDialogFilter =
        $"Video ({BuildDialogGlob(VideoExtensions)})|{BuildDialogGlob(VideoExtensions)}|Alle Dateien|*.*";

    public static bool HasVideoExtension(string? pathOrExtension)
        => HasKnownExtension(pathOrExtension, VideoExtensions);

    public static bool HasImageExtension(string? pathOrExtension)
        => HasKnownExtension(pathOrExtension, ImageExtensions);

    private static bool HasKnownExtension(string? pathOrExtension, IReadOnlyCollection<string> extensions)
    {
        if (string.IsNullOrWhiteSpace(pathOrExtension))
            return false;

        var ext = pathOrExtension.Trim();
        if (!ext.StartsWith(".", StringComparison.Ordinal))
            ext = Path.GetExtension(ext);

        return !string.IsNullOrWhiteSpace(ext)
               && extensions.Contains(ext, StringComparer.OrdinalIgnoreCase);
    }

    private static string BuildDialogGlob(IEnumerable<string> extensions)
        => string.Join(";", extensions.Select(ext => $"*{ext}"));
}
