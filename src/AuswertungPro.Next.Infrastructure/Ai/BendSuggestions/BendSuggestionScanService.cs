using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Media;
using AuswertungPro.Next.Application.UseCases.BendSuggestions;

namespace AuswertungPro.Next.Infrastructure.Ai.BendSuggestions;

/// <summary>
/// Steckt die geprueften Einzelteile zu einem Vorabdurchlauf zusammen:
/// Kalibrierung lesen, Bilder in einem ffmpeg-Durchgang holen, jedes Bild dem
/// gepinnten Kandidaten vorlegen, Treffer zu Stellen zusammenfassen.
///
/// Die Regeln selbst liegen in der Application-Schicht
/// (<see cref="BendSuggestionScanUseCase"/>, <see cref="BendSuggestionAggregator"/>);
/// hier ist nur die Verdrahtung.
///
/// Ein Meterstand wird derzeit nicht geliefert — dafuer gibt es in C# noch keinen
/// guenstigen OSD-Leser. Die Zusammenfassung faellt dann auf die Zeitregel zurueck,
/// mit der hoeheren Fehlalarmlast, die dabei gemessen wurde (2,8 statt 1,0 je
/// Haltung). Sobald eine Meterquelle vorhanden ist, wird sie hier eingehaengt.
/// </summary>
public sealed class BendSuggestionScanService : IBendSuggestionScanService
{
    private readonly IBendSuggestionCalibrationStore _calibrations;
    private readonly IVideoFrameSequenceExtractor _extractor;
    private readonly Func<BccTestYoloRequest, CancellationToken, Task<BccTestYoloResponse>> _ask;
    private readonly Func<string> _resolveFfmpegPath;
    private readonly Func<string> _resolveWorkRoot;
    private readonly Func<VideoSequenceFrame, CancellationToken, Task<(double? Meter, bool IsEstimated)>>? _resolveMeter;

    public BendSuggestionScanService(
        IBendSuggestionCalibrationStore calibrations,
        IVideoFrameSequenceExtractor extractor,
        Func<BccTestYoloRequest, CancellationToken, Task<BccTestYoloResponse>> ask,
        Func<string> resolveFfmpegPath,
        Func<string> resolveWorkRoot,
        Func<VideoSequenceFrame, CancellationToken, Task<(double? Meter, bool IsEstimated)>>? resolveMeter = null)
    {
        _calibrations = calibrations ?? throw new ArgumentNullException(nameof(calibrations));
        _extractor = extractor ?? throw new ArgumentNullException(nameof(extractor));
        _ask = ask ?? throw new ArgumentNullException(nameof(ask));
        _resolveFfmpegPath = resolveFfmpegPath ?? throw new ArgumentNullException(nameof(resolveFfmpegPath));
        _resolveWorkRoot = resolveWorkRoot ?? throw new ArgumentNullException(nameof(resolveWorkRoot));
        _resolveMeter = resolveMeter;
    }

    public async Task<BendSuggestionScanResult> ScanAsync(
        BendSuggestionScanRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var calibration = _calibrations.TryRead(request.CandidateId);

        // Eigener Ordner je Lauf: Der Extraktor verlangt ein leeres Ziel, damit
        // Bilder eines frueheren Laufs nie mitgezaehlt werden.
        var workDirectory = Path.Combine(_resolveWorkRoot(), Guid.NewGuid().ToString("N"));
        try
        {
            var floor = Math.Min(0.10, calibration?.MinConfidence ?? 0.10);
            var detector = new BendFrameDetector(
                request.CandidateId, request.WeightSha256, floor, _ask);

            var actions = new BendSuggestionScanActions(
                ExtractFrames: token => _extractor.ExtractAsync(
                    new VideoFrameSequenceRequest
                    {
                        FfmpegPath = _resolveFfmpegPath(),
                        VideoPath = request.VideoPath,
                        TargetDirectory = workDirectory
                    },
                    token),
                DetectBendConfidence: async (frame, token) => await detector
                    .DetectAsync(
                        await File.ReadAllBytesAsync(frame.FilePath, token).ConfigureAwait(false),
                        token)
                    .ConfigureAwait(false),
                ResolveMeter: _resolveMeter);

            return await BendSuggestionScanUseCase
                .ExecuteAsync(request, calibration, actions, cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            // Die Bilder sind Zwischenmaterial und duerfen nicht liegen bleiben —
            // ein Neun-Minuten-Video sind rund 550 Dateien.
            try
            {
                if (Directory.Exists(workDirectory))
                    Directory.Delete(workDirectory, recursive: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Aufraeumen darf einen fertigen Durchlauf nie scheitern lassen.
            }
        }
    }
}
