using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Media;

namespace AuswertungPro.Next.Application.UseCases.PipeEndSuggestions;

/// <summary>Auftrag fuer den Vorabdurchlauf ueber ein Video.</summary>
public sealed record PipeEndScanRequest
{
    public required string VideoPath { get; init; }
}

/// <summary>
/// Die Aussenverbindungen des Durchlaufs. Eingehaengt, damit die Regeln ohne
/// ffmpeg und ohne Sidecar pruefbar bleiben.
/// </summary>
/// <param name="ExtractFrames">Holt die Bildfolge in einem Durchgang (1 Bild je Sekunde wie in der Abnahme).</param>
/// <param name="Score">
/// Fragt die gepinnte Lernstufe zu einem Bild und liefert die Konfidenz fuer
/// das ganze Bild. Ein technischer Fehler muss geworfen werden und darf nie als
/// "kein Treffer" erscheinen.
/// </param>
public sealed record PipeEndScanActions(
    Func<CancellationToken, Task<IReadOnlyList<VideoSequenceFrame>>> ExtractFrames,
    Func<VideoSequenceFrame, PipeEndLernstufePin, CancellationToken, Task<double>> Score)
{
    /// <summary>Uhr fuer die Laufzeitmessung; im Test ersetzbar.</summary>
    public Func<DateTimeOffset> Now { get; init; } = () => DateTimeOffset.UtcNow;

    /// <summary>Fortschritt je Klasse (Klasse, verarbeitet, gesamt); optional, rein nach aussen.</summary>
    public Action<PipeEndKind, int, int>? ReportProgress { get; init; }
}

/// <summary>Ergebnis eines Vorabdurchlaufs: hoechstens ein Vorschlag je Klasse.</summary>
public sealed record PipeEndScanResult(
    IReadOnlyList<PipeEndSuggestion> Suggestions,
    int FramesAnalyzed,
    TimeSpan Duration,
    IReadOnlyList<PipeEndLernstufePin> Pins);

/// <summary>
/// Vorabdurchlauf fuer Rohranfang und Rohrende: Bilder einmal holen, je
/// gepinnter Lernstufe jedes Bild fragen, je Klasse die staerkste Stelle
/// vorschlagen.
///
/// Die Klassen laufen NACHEINANDER ueber alle Bilder, nicht je Bild abwechselnd:
/// Beide Lernstufen teilen sich im Sidecar denselben Modellplatz (YOLO_TEST),
/// und ein Wechsel je Bild wuerde das Gewicht bei jedem Bild neu laden.
/// </summary>
public static class PipeEndSuggestionScanUseCase
{
    public static async Task<PipeEndScanResult> ExecuteAsync(
        PipeEndScanRequest request,
        IReadOnlyList<PipeEndLernstufePin> pins,
        PipeEndScanActions actions,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(pins);
        ArgumentNullException.ThrowIfNull(actions);

        var started = actions.Now();
        cancellationToken.ThrowIfCancellationRequested();

        var frames = await actions.ExtractFrames(cancellationToken).ConfigureAwait(false)
            ?? Array.Empty<VideoSequenceFrame>();

        var suggestions = new List<PipeEndSuggestion>(pins.Count);
        foreach (var pin in pins)
        {
            var scores = new List<PipeEndFrameScore>(frames.Count);
            var processed = 0;
            foreach (var frame in frames)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Ein technischer Fehler wird bewusst nicht gefangen: "nichts gefunden"
                // und "nichts gesehen" sind verschiedene Aussagen.
                var confidence = await actions.Score(frame, pin, cancellationToken).ConfigureAwait(false);
                scores.Add(new PipeEndFrameScore(frame.TimeSeconds, confidence));
                processed++;
                actions.ReportProgress?.Invoke(pin.Kind, processed, frames.Count);
            }

            var suggestion = PipeEndSuggestionRule.Strongest(
                scores, pin.Kind, PipeEndRuleOptions.ForKind(pin.Kind));
            if (suggestion is not null)
                suggestions.Add(suggestion);
        }

        return new PipeEndScanResult(
            suggestions,
            frames.Count,
            actions.Now() - started,
            pins);
    }
}
