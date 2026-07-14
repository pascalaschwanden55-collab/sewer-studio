using System;
using AuswertungPro.Next.Infrastructure.Diagnostics;

namespace AuswertungPro.Next.UI.DataPage;

/// <summary>
/// Schreibt Diagnoseinformationen, wenn der Video-Player nicht gestartet werden kann.
/// Gekapselt ausserhalb des ViewModels, damit der Fehlerpfad testbar bleibt.
/// </summary>
public static class DataPageVideoStartErrorLogWriter
{
    public static string? TryWrite(Exception exception, string videoPath, string? baseDirectory = null, DateTime? now = null)
        => new VideoStartErrorLogFileWriter(
                baseDirectory,
                now.HasValue ? () => now.Value : null)
            .TryWrite(exception, videoPath);
}
