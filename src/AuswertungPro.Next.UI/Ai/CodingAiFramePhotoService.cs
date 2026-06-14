using System.Diagnostics;
using System.Globalization;
using System.IO;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.UI.Ai;

public static class CodingAiFramePhotoService
{
    public static string? AttachAnalyzedFramePhoto(
        ProtocolEntry entry,
        byte[]? frameBytes,
        string? videoPath = null,
        string? photoRoot = null)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var existing = entry.FotoPaths.FirstOrDefault(path =>
            !string.IsNullOrWhiteSpace(path) && File.Exists(path));
        if (!string.IsNullOrWhiteSpace(existing))
            return existing;

        if (frameBytes is null || frameBytes.Length == 0)
            return null;

        try
        {
            var root = ResolvePhotoRoot(videoPath, photoRoot);
            Directory.CreateDirectory(root);

            var path = EnsureUniquePath(Path.Combine(root, BuildFileName(entry)));
            File.WriteAllBytes(path, frameBytes);
            entry.FotoPaths.Add(path);
            return path;
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is NotSupportedException || ex is ArgumentException)
        {
            Debug.WriteLine($"[CodingAiFramePhoto] Frame konnte nicht gespeichert werden: {ex.Message}");
            return null;
        }
    }

    private static string ResolvePhotoRoot(string? videoPath, string? photoRoot)
    {
        if (!string.IsNullOrWhiteSpace(photoRoot))
            return photoRoot;

        var videoDir = !string.IsNullOrWhiteSpace(videoPath)
            ? Path.GetDirectoryName(videoPath)
            : null;

        return !string.IsNullOrWhiteSpace(videoDir)
            ? Path.Combine(videoDir, "Fotos")
            : Path.Combine(Path.GetTempPath(), "SewerStudio", "coding_ai_frames");
    }

    private static string BuildFileName(ProtocolEntry entry)
    {
        var code = MakeSafeFileName(string.IsNullOrWhiteSpace(entry.Code) ? "KI" : entry.Code);
        var meter = entry.MeterStart?.ToString("F2", CultureInfo.InvariantCulture) ?? "unknown";
        var time = entry.Zeit.HasValue
            ? entry.Zeit.Value.ToString(@"hh\-mm\-ss\-fff", CultureInfo.InvariantCulture)
            : DateTimeOffset.Now.ToString("HHmmssfff", CultureInfo.InvariantCulture);

        return $"{code}_{meter}m_{time}_{entry.EntryId:N}_ai.png";
    }

    private static string MakeSafeFileName(string value)
    {
        var safe = value.Trim();
        foreach (var ch in Path.GetInvalidFileNameChars())
            safe = safe.Replace(ch, '_');

        return string.IsNullOrWhiteSpace(safe) ? "KI" : safe;
    }

    private static string EnsureUniquePath(string path)
    {
        if (!File.Exists(path))
            return path;

        var dir = Path.GetDirectoryName(path) ?? "";
        var name = Path.GetFileNameWithoutExtension(path);
        var ext = Path.GetExtension(path);
        for (var i = 1; i < 10_000; i++)
        {
            var candidate = Path.Combine(dir, $"{name}_{i}{ext}");
            if (!File.Exists(candidate))
                return candidate;
        }

        return Path.Combine(dir, $"{name}_{Guid.NewGuid():N}{ext}");
    }
}
