using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Media;

namespace AuswertungPro.Next.Application.UseCases.BendSuggestions;

/// <summary>Wie ein einzelnes Bild ausgegangen ist.</summary>
public enum BendFrameOutcome
{
    /// <summary>Ausgewertet, kein Bogen gefunden.</summary>
    NoBend = 0,

    /// <summary>Ausgewertet, Bogen gefunden.</summary>
    Detected = 1,

    /// <summary>
    /// Nicht ausgewertet — etwa weil das Bild zu unscharf oder zu dunkel war.
    /// Das ist ausdruecklich KEIN "kein Bogen": "nichts gefunden" und "nichts
    /// gesehen" sind verschiedene Aussagen.
    /// </summary>
    NotAssessed = 2
}

/// <summary>Ergebnis eines einzelnen Bildes.</summary>
public sealed record BendFrameResult(
    BendFrameOutcome Outcome,
    double Confidence = 0.0,
    string? Reason = null)
{
    public static BendFrameResult NoBend { get; } = new(BendFrameOutcome.NoBend);

    public static BendFrameResult Detected(double confidence)
        => new(BendFrameOutcome.Detected, confidence);

    public static BendFrameResult NotAssessed(string? reason)
        => new(BendFrameOutcome.NotAssessed, 0.0, reason);
}

/// <summary>Auftrag fuer den Vorabdurchlauf ueber ein Video.</summary>
public sealed record BendSuggestionScanRequest
{
    public required string VideoPath { get; init; }

    /// <summary>Gepinnter Kandidat — ohne ihn waehlt der Sidecar selbst.</summary>
    public required string CandidateId { get; init; }

    public required string WeightSha256 { get; init; }
}

/// <summary>
/// Die Aussenverbindungen des Durchlaufs. Eingehaengt, damit die Regeln ohne
/// ffmpeg und ohne Sidecar pruefbar bleiben.
/// </summary>
/// <param name="ExtractFrames">Holt die Bildfolge in einem Durchgang.</param>
/// <param name="DetectBendConfidence">
/// Fragt das gepinnte Modell zu einem Bild. Ein technischer Fehler muss
/// geworfen werden und darf nie als "kein Bogen" erscheinen.
/// </param>
/// <param name="ResolveMeter">
/// Optionaler Meterstand je Bild. Fehlt er, fasst der Aggregator ueber die Zeit
/// zusammen — mit der hoeheren Fehlalarmlast, die dabei gemessen wurde.
/// </param>
public sealed record BendSuggestionScanActions(
    Func<CancellationToken, Task<IReadOnlyList<VideoSequenceFrame>>> ExtractFrames,
    Func<VideoSequenceFrame, CancellationToken, Task<BendFrameResult>> DetectBendConfidence,
    Func<VideoSequenceFrame, CancellationToken, Task<(double? Meter, bool IsEstimated)>>? ResolveMeter = null)
{
    /// <summary>Uhr fuer die Laufzeitmessung; im Test ersetzbar.</summary>
    public Func<DateTimeOffset> Now { get; init; } = () => DateTimeOffset.UtcNow;
}

/// <summary>Ergebnis eines Vorabdurchlaufs.</summary>
public sealed record BendSuggestionScanResult(
    bool IsUsable,
    string Reason,
    IReadOnlyList<BendSuggestion> Suggestions,
    int FramesAnalyzed,
    int FramesNotAssessed,
    TimeSpan Duration,
    string CandidateId,
    string WeightSha256,
    double MinConfidence,
    double StrongConfidence);

/// <summary>
/// Vorabdurchlauf eines Videos: Bilder holen, je Bild das gepinnte Modell fragen,
/// Treffer zu Vorschlaegen zusammenfassen.
///
/// Ohne kalibrierten Arbeitspunkt laeuft gar nichts — nicht einmal die
/// Bildextraktion. Damit ist der Pin strukturell erzwungen: Die Kalibrierung ist
/// an Kandidaten-ID und Gewicht-Hash gebunden, und ohne sie gibt es keinen Aufruf,
/// bei dem der Sidecar selbst ein Modell waehlen koennte.
///
/// Die Laufzeit wird mitgemessen und ausgewiesen. Ueber HTTP je Bild ist der
/// Durchlauf deutlich langsamer als ein direkter Modellaufruf; wer die Laufzeit
/// nicht ausweist, bemerkt eine Verschlechterung nie.
/// </summary>
public static class BendSuggestionScanUseCase
{
    public static async Task<BendSuggestionScanResult> ExecuteAsync(
        BendSuggestionScanRequest request,
        BendSuggestionCalibration? calibration,
        BendSuggestionScanActions actions,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        var resolved = BendSuggestionCalibrationPolicy.Resolve(
            calibration, request.CandidateId, request.WeightSha256);
        if (!resolved.IsUsable || resolved.Options is not { } options)
        {
            return new BendSuggestionScanResult(
                false, resolved.Reason, Array.Empty<BendSuggestion>(), 0, 0, TimeSpan.Zero,
                request.CandidateId, request.WeightSha256, 0.0, 0.0);
        }

        var started = actions.Now();
        cancellationToken.ThrowIfCancellationRequested();

        var frames = await actions.ExtractFrames(cancellationToken).ConfigureAwait(false)
            ?? Array.Empty<VideoSequenceFrame>();

        var detections = new List<BendFrameDetection>();
        var notAssessed = 0;
        foreach (var frame in frames)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Ein technischer Fehler wird bewusst nicht gefangen: "nichts gefunden"
            // und "nichts gesehen" sind verschiedene Aussagen.
            var outcome = await actions.DetectBendConfidence(frame, cancellationToken)
                .ConfigureAwait(false);
            if (outcome.Outcome == BendFrameOutcome.NotAssessed)
            {
                // Nicht ausgewertet ist kein "kein Bogen" — es wird gezaehlt und
                // ausgewiesen, damit ein blinder Fleck sichtbar bleibt.
                notAssessed++;
                continue;
            }

            if (outcome.Outcome != BendFrameOutcome.Detected)
                continue;

            var (meter, isEstimated) = actions.ResolveMeter is null
                ? (null, false)
                : await actions.ResolveMeter(frame, cancellationToken).ConfigureAwait(false);
            detections.Add(new BendFrameDetection(
                frame.TimeSeconds, meter, outcome.Confidence, isEstimated));
        }

        var suggestions = BendSuggestionAggregator.Aggregate(detections, options);
        return new BendSuggestionScanResult(
            true,
            string.Empty,
            suggestions,
            frames.Count,
            notAssessed,
            actions.Now() - started,
            request.CandidateId,
            request.WeightSha256,
            options.MinConfidence,
            options.StrongConfidence);
    }
}
