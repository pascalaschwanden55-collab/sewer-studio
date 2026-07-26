using System;

namespace AuswertungPro.Next.UI.Ai.Coding;

public sealed class CodingPhotoCaptureServices
{
    private readonly Func<CodingFrameExtractionService> _createFrameExtractionService;
    private readonly Func<CodingSnapshotFileCaptureService> _createSnapshotFileCaptureService;
    private CodingFrameExtractionService? _frameExtractionService;
    private CodingSnapshotFileCaptureService? _snapshotFileCaptureService;

    public CodingPhotoCaptureServices()
        : this(
            CodingFrameExtractionServiceFactory.Create,
            CodingSnapshotFileCaptureServiceFactory.Create)
    {
    }

    public CodingPhotoCaptureServices(
        Func<CodingFrameExtractionService> createFrameExtractionService,
        Func<CodingSnapshotFileCaptureService> createSnapshotFileCaptureService)
    {
        _createFrameExtractionService = createFrameExtractionService;
        _createSnapshotFileCaptureService = createSnapshotFileCaptureService;
    }

    public CodingFrameExtractionService FrameExtractionService
        => _frameExtractionService ??= _createFrameExtractionService();

    public CodingSnapshotFileCaptureService SnapshotFileCaptureService
        => _snapshotFileCaptureService ??= _createSnapshotFileCaptureService();
}
