using System;
using System.IO;
using System.Threading;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private CodingFrameExtractionService? _codingFrameExtractionService;

    private CodingFrameExtractionService CodingFrameExtractionService
        => _codingFrameExtractionService ??= new CodingFrameExtractionService();

    private byte[]? TryExtractAnalyzedFrameBytes()
    {
        var sec = CodingAnalyzedFrameTimestampPolicy.Resolve(
            _detectionPendingTimestampSec,
            _codingFrameReadiness.FirstCleanFrameSeconds);
        return TryExtractFrameAtSeconds(sec);
    }

    private byte[]? TryExtractFrameAtSeconds(double? sec)
    {
        return CodingFrameExtractionService.TryExtractFrameAtSeconds(_videoPath, sec);
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
