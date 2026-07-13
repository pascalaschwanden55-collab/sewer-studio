using System.Diagnostics;
using System.IO;

namespace AuswertungPro.Next.UI.Services;

public static class SafeShellOpen
{
    private static readonly HashSet<string> AllowedFileExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".png", ".jpg", ".jpeg", ".bmp", ".webp", ".tif", ".tiff",
        ".mp4", ".mpg", ".mpeg", ".avi", ".mov", ".mkv",
        ".xlsx", ".csv", ".txt", ".json"
    };

    public static bool TryOpen(string? path, out string? error)
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

        var ext = Path.GetExtension(fullPath);
        if (!AllowedFileExtensions.Contains(ext))
        {
            error = $"Dateityp nicht zum direkten Oeffnen freigegeben: {ext}";
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
