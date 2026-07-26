using System;
using System.Threading.Tasks;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private CodingSnapshotFileCaptureService CodingSnapshotFileCaptureService
        => _codingPhotoCaptureServicesOwner.SnapshotFileCaptureService;

    private CodingFrameExtractionService CodingFrameExtractionService
        => _codingPhotoCaptureServicesOwner.FrameExtractionService;

    private Task<byte[]?> TryExtractAnalyzedFrameBytesAsync()
    {
        var sec = CodingAnalyzedFrameTimestampPolicy.Resolve(
            _liveDetectionController.PendingConfirmationTimestampSeconds,
            _codingFrameReadinessController.FirstCleanFrameSeconds);
        return TryExtractFrameAtSecondsAsync(sec);
    }

    private Task<byte[]?> TryExtractFrameAtSecondsAsync(double? sec)
    {
        return CodingFrameExtractionService.TryExtractFrameAtSecondsAsync(_playbackContext.VideoPath, sec);
    }

    private TimeSpan? GetCurrentPlayerTimestamp()
        => _playerTimelineHost.CurrentTime;

    private string? CodingCaptureSnapshot(ProtocolEntry entry)
    {
        var target = CodingSnapshotTargetPolicy.Build(entry, _playbackContext.VideoPath, PlayerClock.NowOffset());
        return CodingSnapshotFileCaptureService.CaptureSnapshot(target, path => TakeSnapshotSafe(path));
    }
}
