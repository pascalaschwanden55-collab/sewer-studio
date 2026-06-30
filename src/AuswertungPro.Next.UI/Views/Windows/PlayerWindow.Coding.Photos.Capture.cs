using System;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private CodingSnapshotFileCaptureService CodingSnapshotFileCaptureService
        => _codingPhotoCaptureServicesOwner.SnapshotFileCaptureService;

    private CodingFrameExtractionService CodingFrameExtractionService
        => _codingPhotoCaptureServicesOwner.FrameExtractionService;

    private byte[]? TryExtractAnalyzedFrameBytes()
    {
        var sec = CodingAnalyzedFrameTimestampPolicy.Resolve(
            _liveDetectionController.PendingConfirmationTimestampSeconds,
            _codingFrameReadinessController.FirstCleanFrameSeconds);
        return TryExtractFrameAtSeconds(sec);
    }

    private byte[]? TryExtractFrameAtSeconds(double? sec)
    {
        return CodingFrameExtractionService.TryExtractFrameAtSeconds(_playbackContext.VideoPath, sec);
    }

    private TimeSpan? GetCurrentPlayerTimestamp()
        => _playerTimelineHost.CurrentTime;

    private string? CodingCaptureSnapshot(ProtocolEntry entry)
    {
        var target = CodingSnapshotTargetPolicy.Build(entry, _playbackContext.VideoPath, PlayerClock.NowOffset());
        return CodingSnapshotFileCaptureService.CaptureSnapshot(target, path => TakeSnapshotSafe(path));
    }
}
