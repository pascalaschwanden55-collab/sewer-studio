using System.Globalization;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Infrastructure.Ai;

public sealed class CodingFramePhotoFileStore : ICodingFramePhotoStore
{
    private readonly object _writeGate = new();

    public string? AttachAnalyzedFramePhoto(
        ProtocolEntry entry,
        byte[]? frameBytes,
        string? videoPath = null,
        string? photoRoot = null)
    {
        ArgumentNullException.ThrowIfNull(entry);

        lock (_writeGate)
        {
            var existing = entry.FotoPaths.FirstOrDefault(path =>
                !string.IsNullOrWhiteSpace(path) && File.Exists(path));
            if (!string.IsNullOrWhiteSpace(existing))
                return existing;

            if (frameBytes is null || frameBytes.Length == 0)
                return null;

            var root = ResolvePhotoRoot(videoPath, photoRoot);
            if (root is null)
            {
                // Deepscan 02.10.2026, R3: Frueher still nach %TEMP%\SewerStudio\coding_ai_frames,
                // und der Pfad landete im Befund. Befundfotos gehoeren nie in den Temp-Ordner.
                BestEffort.ReportWarning(
                    "[CodingAiFramePhoto] Kein Video und kein Fotoordner bekannt: Frame nicht gespeichert, "
                    + "weil Befundfotos nie in den Temp-Ordner gehoeren.");
                return null;
            }

            try
            {
                Directory.CreateDirectory(root);

                var path = EnsureUniquePath(Path.Combine(root, BuildFileName(entry)));
                File.WriteAllBytes(path, frameBytes);
                entry.FotoPaths.Add(path);
                return path;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
            {
                BestEffort.ReportWarning(
                    $"[CodingAiFramePhoto] Frame konnte nicht gespeichert werden: {ex.Message}");
                return null;
            }
        }
    }

    /// <summary>Fotoordner neben dem Video; <c>null</c>, wenn weder Video noch Fotoordner bekannt sind.</summary>
    private static string? ResolvePhotoRoot(string? videoPath, string? photoRoot)
    {
        if (!string.IsNullOrWhiteSpace(photoRoot))
            return photoRoot;

        var videoDir = !string.IsNullOrWhiteSpace(videoPath)
            ? Path.GetDirectoryName(videoPath)
            : null;

        return !string.IsNullOrWhiteSpace(videoDir)
            ? Path.Combine(videoDir, "Fotos")
            : null;
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
