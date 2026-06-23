using System;
using System.IO;
using System.Threading;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Shared;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private byte[]? TryExtractAnalyzedFrameBytes()
    {
        var sec = CodingAnalyzedFrameTimestampPolicy.Resolve(
            _detectionPendingTimestampSec,
            _codingFrameReadiness.FirstCleanFrameSeconds);
        return TryExtractFrameAtSeconds(sec);
    }

    private byte[]? TryExtractFrameAtSeconds(double? sec)
    {
        if (sec is null || sec.Value < 0 || string.IsNullOrWhiteSpace(_videoPath))
            return null;

        try
        {
            var ffmpeg = FfmpegLocator.ResolveFfmpeg();
            if (string.IsNullOrWhiteSpace(ffmpeg))
                return null;

            return VideoFrameExtractor.TryExtractFramePngAsync(
                ffmpeg, _videoPath, TimeSpan.FromSeconds(sec.Value), CancellationToken.None)
                .GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Foto] ffmpeg-Frame-Extraktion fehlgeschlagen: {ex.Message}");
            return null;
        }
    }

    private TimeSpan? GetCurrentPlayerTimestamp()
    {
        if (_player == null || _player.Time < 0)
            return null;

        return TimeSpan.FromMilliseconds(_player.Time);
    }

    private string? CodingCaptureSnapshot(ProtocolEntry entry)
    {
        try
        {
            var target = CodingSnapshotTargetPolicy.Build(entry, _videoPath, PlayerClock.NowOffset());
            Directory.CreateDirectory(target.PhotoDirectory);

            TakeSnapshotSafe(target.FilePath);

            for (var i = 0; i < 20; i++)
            {
                Thread.Sleep(50);
                if (File.Exists(target.FilePath) && new FileInfo(target.FilePath).Length > 100)
                    return target.FilePath;
            }

            return File.Exists(target.FilePath) ? target.FilePath : null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Snapshot-Fehler: {ex.Message}");
            return null;
        }
    }
}
