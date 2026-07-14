using System;
using System.IO;
using AuswertungPro.Next.Application.DataPage;

namespace AuswertungPro.Next.Infrastructure.Diagnostics;

/// <summary>
/// Schreibt Video-Startfehler best-effort in den lokalen Protokollordner.
/// </summary>
public sealed class VideoStartErrorLogFileWriter : IVideoStartErrorLogWriter
{
    private readonly string _baseDirectory;
    private readonly Func<DateTime> _now;

    public VideoStartErrorLogFileWriter(
        string? baseDirectory = null,
        Func<DateTime>? now = null)
    {
        _baseDirectory = string.IsNullOrWhiteSpace(baseDirectory)
            ? AppDomain.CurrentDomain.BaseDirectory
            : baseDirectory;
        _now = now ?? (static () => DateTime.Now);
    }

    public string? TryWrite(Exception exception, string videoPath)
    {
        try
        {
            var timestamp = _now();
            var logsDirectory = Path.Combine(_baseDirectory, "logs");
            Directory.CreateDirectory(logsDirectory);

            var fileName = $"video_start_error_{timestamp:yyyyMMdd_HHmmss}_{MakeSafeVideoName(videoPath)}.txt";
            var logPath = Path.Combine(logsDirectory, fileName);
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
        foreach (var character in Path.GetInvalidFileNameChars())
            safeName = safeName.Replace(character, '_');

        return string.IsNullOrWhiteSpace(safeName) ? "video" : safeName;
    }
}
