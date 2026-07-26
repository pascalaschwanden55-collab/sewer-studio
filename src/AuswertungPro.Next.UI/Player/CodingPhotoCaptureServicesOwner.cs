using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Player;

public sealed class CodingPhotoCaptureServicesOwner
{
    private readonly Func<CodingPhotoCaptureServices> _createServices;
    private CodingPhotoCaptureServices? _services;

    public CodingPhotoCaptureServicesOwner()
        : this(() => new CodingPhotoCaptureServices())
    {
    }

    public CodingPhotoCaptureServicesOwner(Func<CodingPhotoCaptureServices> createServices)
    {
        ArgumentNullException.ThrowIfNull(createServices);

        _createServices = createServices;
    }

    public CodingPhotoCaptureServices Services
        => _services ??= _createServices();

    public CodingSnapshotFileCaptureService SnapshotFileCaptureService
        => Services.SnapshotFileCaptureService;

    public CodingFrameExtractionService FrameExtractionService
        => Services.FrameExtractionService;
}
