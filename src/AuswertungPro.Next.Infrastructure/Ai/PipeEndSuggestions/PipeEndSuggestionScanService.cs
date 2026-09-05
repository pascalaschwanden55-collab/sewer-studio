using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Media;
using AuswertungPro.Next.Application.UseCases.PipeEndSuggestions;

namespace AuswertungPro.Next.Infrastructure.Ai.PipeEndSuggestions;

/// <summary>
/// Steckt die geprueften Einzelteile zu einem Vorabdurchlauf zusammen: Bilder in
/// einem ffmpeg-Durchgang holen (1 Bild je Sekunde wie in der Abnahme), jedes
/// Bild den gepinnten Lernstufen vorlegen, je Klasse die staerkste Stelle nennen.
///
/// Die Regeln selbst liegen in der Application-Schicht
/// (<see cref="PipeEndSuggestionScanUseCase"/>, <see cref="PipeEndSuggestionRule"/>);
/// hier ist nur die Verdrahtung.
/// </summary>
public sealed class PipeEndSuggestionScanService : IPipeEndSuggestionScanService
{
    /// <summary>Abtastrate der Abnahme (lernstufe_vorschlagspruefung.py, --fps 1).</summary>
    public const double FramesPerSecond = 1.0;

    private readonly IVideoFrameSequenceExtractor _extractor;
    private readonly LernstufeFrameScorer _scorer;
    private readonly Func<string> _resolveFfmpegPath;
    private readonly Func<string> _resolveWorkRoot;
    private readonly IReadOnlyList<PipeEndLernstufePin> _pins;

    public PipeEndSuggestionScanService(
        IVideoFrameSequenceExtractor extractor,
        Func<LernstufeRequest, CancellationToken, Task<LernstufeResponse>> ask,
        Func<string> resolveFfmpegPath,
        Func<string> resolveWorkRoot,
        IReadOnlyList<PipeEndLernstufePin>? pins = null)
    {
        _extractor = extractor ?? throw new ArgumentNullException(nameof(extractor));
        _scorer = new LernstufeFrameScorer(ask ?? throw new ArgumentNullException(nameof(ask)));
        _resolveFfmpegPath = resolveFfmpegPath ?? throw new ArgumentNullException(nameof(resolveFfmpegPath));
        _resolveWorkRoot = resolveWorkRoot ?? throw new ArgumentNullException(nameof(resolveWorkRoot));
        _pins = pins ?? PipeEndLernstufePins.All;
    }

    public async Task<PipeEndScanResult> ScanAsync(
        PipeEndScanRequest request,
        CancellationToken cancellationToken,
        IProgress<PipeEndScanProgress>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Eigener Ordner je Lauf: Der Extraktor verlangt ein leeres Ziel, damit
        // Bilder eines frueheren Laufs nie mitgezaehlt werden.
        var workDirectory = Path.Combine(_resolveWorkRoot(), Guid.NewGuid().ToString("N"));
        try
        {
            var actions = new PipeEndScanActions(
                ExtractFrames: token => _extractor.ExtractAsync(
                    new VideoFrameSequenceRequest
                    {
                        FfmpegPath = _resolveFfmpegPath(),
                        VideoPath = request.VideoPath,
                        TargetDirectory = workDirectory,
                        FramesPerSecond = FramesPerSecond
                    },
                    token),
                Score: async (frame, pin, token) => await _scorer
                    .ScoreAsync(
                        await File.ReadAllBytesAsync(frame.FilePath, token).ConfigureAwait(false),
                        pin,
                        token)
                    .ConfigureAwait(false))
            {
                ReportProgress = progress is null
                    ? null
                    : (kind, verarbeitet, gesamt) => progress.Report(
                        new PipeEndScanProgress(kind, verarbeitet, gesamt))
            };

            return await PipeEndSuggestionScanUseCase
                .ExecuteAsync(request, _pins, actions, cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            // Die Bilder sind Zwischenmaterial und duerfen nicht liegen bleiben.
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
