using System.Diagnostics;
using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.Infrastructure.Common;

public sealed class SafeShellOpenService : ISafeShellOpenService
{
    private static readonly HashSet<string> AllowedFileExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".png", ".jpg", ".jpeg", ".bmp", ".webp", ".tif", ".tiff",
        ".mp4", ".mpg", ".mpeg", ".avi", ".mov", ".mkv",
        ".xlsx", ".csv", ".txt", ".json"
    };

    public bool TryOpen(string? path, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(path))
        {
            error = "Pfad fehlt.";
            return false;
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(path);
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }

        if (Directory.Exists(fullPath))
            return Start(fullPath, out error);

        if (!File.Exists(fullPath))
        {
            error = "Datei nicht gefunden.";
            return false;
        }

        var extension = Path.GetExtension(fullPath);
        if (!AllowedFileExtensions.Contains(extension))
        {
            error = $"Dateityp nicht zum direkten Oeffnen freigegeben: {extension}";
            return false;
        }

        return Start(fullPath, out error);
    }

    private static bool Start(string fullPath, out string? error)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = fullPath,
                UseShellExecute = true
            });
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }
}
