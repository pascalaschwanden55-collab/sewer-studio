using System;
using System.IO;

namespace AuswertungPro.Next.UI.DataPage;

/// <summary>
/// Schreibt Diagnoseinformationen, wenn der Video-Player nicht gestartet werden kann.
/// Gekapselt ausserhalb des ViewModels, damit der Fehlerpfad testbar bleibt.
/// </summary>
public static class DataPageVideoStartErrorLogWriter
{
    public static string? TryWrite(Exception exception, string videoPath, string? baseDirectory = null, DateTime? now = null)
    {
        try
        {
            var timestamp = now ?? DateTime.Now;
            var root = string.IsNullOrWhiteSpace(baseDirectory)
                ? AppDomain.CurrentDomain.BaseDirectory
                : baseDirectory;
            var logsDir = Path.Combine(root, "logs");
            Directory.CreateDirectory(logsDir);

            var safeName = MakeSafeVideoName(videoPath);
            var file = $"video_start_error_{timestamp:yyyyMMdd_HHmmss}_{safeName}.txt";
            var logPath = Path.Combine(logsDir, file);

            var content =
                $"Time: {timestamp:O}{Environment.NewLine}" +
                $"VideoPath: {videoPath}{Environment.NewLine}" +
                $"Exception:{Environment.NewLine}{exception}{Environment.NewLine}";
            File.WriteAllText(logPath, content);
            return logPath;
        }
        catch
        {
            return null;
        }
    }

    private static string MakeSafeVideoName(string videoPath)
    {
        var safeName = Path.GetFileNameWithoutExtension(videoPath);
        foreach (var c in Path.GetInvalidFileNameChars())
            safeName = safeName.Replace(c, '_');

        return string.IsNullOrWhiteSpace(safeName) ? "video" : safeName;
    }
}
