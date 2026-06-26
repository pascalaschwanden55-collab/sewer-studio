using System;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private CodingPhotoCaptureServices? _codingPhotoCaptureServices;

    private CodingPhotoCaptureServices CodingPhotoCaptureServices
        => _codingPhotoCaptureServices ??= new CodingPhotoCaptureServices();

    private CodingSnapshotFileCaptureService CodingSnapshotFileCaptureService
        => CodingPhotoCaptureServices.SnapshotFileCaptureService;

    private CodingFrameExtractionService CodingFrameExtractionService
        => CodingPhotoCaptureServices.FrameExtractionService;

    private byte[]? TryExtractAnalyzedFrameBytes()
    {
        var sec = CodingAnalyzedFrameTimestampPolicy.Resolve(
            _detectionConfirmationBuffer.TimestampSeconds,
            _codingFrameReadinessController.FirstCleanFrameSeconds);
        return TryExtractFrameAtSeconds(sec);
    }

    private byte[]? TryExtractFrameAtSeconds(double? sec)
    {
        return CodingFrameExtractionService.TryExtractFrameAtSeconds(_videoPath, sec);
    }

    private TimeSpan? GetCurrentPlayerTimestamp()
        => _playerTimelineHost.CurrentTime;

    private string? CodingCaptureSnapshot(ProtocolEntry entry)
    {
        var target = CodingSnapshotTargetPolicy.Build(entry, _videoPath, PlayerClock.NowOffset());
        return CodingSnapshotFileCaptureService.CaptureSnapshot(target, path => TakeSnapshotSafe(path));
    }
}
