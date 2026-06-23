using System;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private CodingFrameExtractionService? _codingFrameExtractionService;
    private CodingSnapshotFileCaptureService? _codingSnapshotFileCaptureService;

    private CodingFrameExtractionService CodingFrameExtractionService
        => _codingFrameExtractionService ??= CodingFrameExtractionServiceFactory.Create();

    private CodingSnapshotFileCaptureService CodingSnapshotFileCaptureService
        => _codingSnapshotFileCaptureService ??= CodingSnapshotFileCaptureServiceFactory.Create();

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
        var target = CodingSnapshotTargetPolicy.Build(entry, _videoPath, PlayerClock.NowOffset());
        return CodingSnapshotFileCaptureService.CaptureSnapshot(target, path => TakeSnapshotSafe(path));
    }
}
