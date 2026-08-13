using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Media;

namespace AuswertungPro.Next.Application.UseCases.PipeEndpoints;

/// <summary>Wie ein einzelnes Bild ausgegangen ist.</summary>
public enum PipeEndpointFrameOutcome
{
    /// <summary>Ausgewertet; die Konfidenz gilt.</summary>
    Assessed = 0,

    /// <summary>
    /// Nicht ausgewertet — etwa unlesbares Bild. Ausdruecklich KEIN "nicht
    /// sichtbar": "nichts gefunden" und "nichts gesehen" sind verschiedene
    /// Aussagen, und nur die zweite darf spaeter still verschwinden.
    /// </summary>
    NotAssessed = 1
}

/// <summary>Ergebnis eines Bildes fuer genau eine Klasse.</summary>
public sealed record PipeEndpointFrameResult(
    PipeEndpointFrameOutcome Outcome,
    double Confidence = 0.0,
    string? Reason = null)
{
    public static PipeEndpointFrameResult Assessed(double confidence) => new(PipeEndpointFrameOutcome.Assessed, confidence);

    public static PipeEndpointFrameResult NotAssessed(string? reason) => new(PipeEndpointFrameOutcome.NotAssessed, 0.0, reason);
}

/// <summary>Eine angeheftete, freigegebene Klasse.</summary>
/// <param name="Klasse">Klassenname wie in der Freigabe, etwa "rohranfang".</param>
/// <param name="WeightSha256">Erwarteter Gewicht-Hash; der Sidecar prueft ihn erneut.</param>
/// <param name="Precision">Gemessene Precision der Abnahme, nur zur Anzeige.</param>
/// <param name="Recall">Gemessener Recall der Abnahme, nur zur Anzeige.</param>
public sealed record PipeEndpointClass(string Klasse, string WeightSha256, double Precision, double Recall);

/// <summary>Auftrag fuer den Vorabdurchlauf ueber ein Video.</summary>
public sealed record PipeEndpointScanRequest
{
    public required string VideoPath { get; init; }

    /// <summary>Die angehefteten Klassen. Ohne sie laeuft nichts.</summary>
    public required IReadOnlyList<PipeEndpointClass> Classes { get; init; }

    /// <summary>Arbeitspunkt der Abnahme. Wird nicht aus Daten nachjustiert.</summary>
    public double MinConfidence { get; init; } = 0.50;
}

/// <summary>
/// Die Aussenverbindungen. Eingehaengt, damit die Regeln ohne ffmpeg und ohne
/// Sidecar pruefbar bleiben.
/// </summary>
/// <param name="ClassifyFrame">
/// Fragt eine angeheftete Klasse zu einem Bild. Ein technischer Fehler muss
/// GEWORFEN werden und darf nie als "nicht sichtbar" erscheinen.
/// </param>
public sealed record PipeEndpointScanActions(
    Func<CancellationToken, Task<IReadOnlyList<VideoSequenceFrame>>> ExtractFrames,
    Func<VideoSequenceFrame, PipeEndpointClass, CancellationToken, Task<PipeEndpointFrameResult>> ClassifyFrame)
{
    public Func<DateTimeOffset> Now { get; init; } = () => DateTimeOffset.UtcNow;

    /// <summary>Fortschritt (verarbeitet, gesamt); optional.</summary>
    public Action<int, int>? ReportProgress { get; init; }
}

/// <summary>Ein Vorschlag: die staerkste Stelle einer Klasse im Video.</summary>
public sealed record PipeEndpointSuggestion(
    string Klasse,
    double TimeSeconds,
    double Confidence,
    int FrameIndex,
    double Precision,
    double Recall);

/// <summary>Ergebnis eines Vorabdurchlaufs.</summary>
/// <param name="NotFound">
/// Klassen ohne Vorschlag. Das heisst "nicht gefunden", nicht "nicht vorhanden":
/// In rund jedem fuenften Video ist gar keine Einfahrt zu sehen, weil die
/// Aufnahme schon im Rohr startet.
/// </param>
public sealed record PipeEndpointScanResult(
    bool IsUsable,
    string Reason,
    IReadOnlyList<PipeEndpointSuggestion> Suggestions,
    IReadOnlyList<string> NotFound,
    int FramesAnalyzed,
    int FramesNotAssessed,
    TimeSpan Duration);

/// <summary>
/// Sucht Rohranfang und Rohrende in einem Video: Bilder holen, je Bild und
/// Klasse das angeheftete Modell fragen, die staerkste Stelle je Klasse melden.
///
/// GENAU EIN VORSCHLAG JE KLASSE — und zwar aus der Sache heraus: Ein Video hat
/// einen Rohranfang und ein Rohrende. Diese Regel wurde am 2026-08-12 gegen ein
/// zuerst gebautes Zeitfenster gemessen und gewann deutlich: Beim Rohrende stieg
/// der Recall von 57,6 % auf 88,4 %, weil die Aufnahme nach dem Zielschacht oft
/// weiterlaeuft oder vorher abbricht. Ein Zeitfenster wird deshalb bewusst NICHT
/// eingebaut.
///
/// Die Vorschlaege sind zum Bestaetigen gedacht, nicht zum Uebernehmen: Gemessen
/// sind 85,5 % Precision beim Rohranfang und 88,9 % beim Rohrende — etwa jede
/// siebte Angabe ist falsch.
/// </summary>
public static class PipeEndpointScanUseCase
{
    public static async Task<PipeEndpointScanResult> ExecuteAsync(
        PipeEndpointScanRequest request,
        PipeEndpointScanActions actions,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (request.Classes is not { Count: > 0 })
        {
            return Leer("Keine freigegebene Klasse angeheftet.");
        }

        if (request.Classes.Any(c => string.IsNullOrWhiteSpace(c.Klasse)
                                     || string.IsNullOrWhiteSpace(c.WeightSha256)))
        {
            // Ohne Pin koennte der Sidecar selbst ein Modell waehlen. Genau das
            // soll strukturell unmoeglich sein.
            return Leer("Klasse oder Gewicht-Hash fehlen.");
        }

        if (request.Classes.Select(c => c.Klasse).Distinct(StringComparer.Ordinal).Count() != request.Classes.Count)
        {
            return Leer("Dieselbe Klasse ist mehrfach angeheftet.");
        }

        var started = actions.Now();
        cancellationToken.ThrowIfCancellationRequested();

        var frames = await actions.ExtractFrames(cancellationToken).ConfigureAwait(false)
                     ?? Array.Empty<VideoSequenceFrame>();
        if (frames.Count == 0)
        {
            return Leer("Aus dem Video liessen sich keine Bilder gewinnen.");
        }

        var beste = new Dictionary<string, PipeEndpointSuggestion>(StringComparer.Ordinal);
        var nichtBewertet = 0;

        for (var i = 0; i < frames.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var frame = frames[i];

            foreach (var klasse in request.Classes)
            {
                // Wirft der Aufruf, bricht der ganze Durchlauf ab. Ein Modellausfall
                // darf nicht als "hier ist nichts" durchgehen.
                var ergebnis = await actions.ClassifyFrame(frame, klasse, cancellationToken).ConfigureAwait(false);
                if (ergebnis.Outcome == PipeEndpointFrameOutcome.NotAssessed)
                {
                    nichtBewertet++;
                    continue;
                }

                if (ergebnis.Confidence < request.MinConfidence)
                {
                    continue;
                }

                if (!beste.TryGetValue(klasse.Klasse, out var bisher) || ergebnis.Confidence > bisher.Confidence)
                {
                    beste[klasse.Klasse] = new PipeEndpointSuggestion(
                        klasse.Klasse, frame.TimeSeconds, ergebnis.Confidence, frame.Index,
                        klasse.Precision, klasse.Recall);
                }
            }

            actions.ReportProgress?.Invoke(i + 1, frames.Count);
        }

        var vorschlaege = request.Classes
            .Select(c => beste.TryGetValue(c.Klasse, out var v) ? v : null)
            .Where(v => v is not null)
            .Select(v => v!)
            .ToArray();

        var fehlend = request.Classes
            .Where(c => !beste.ContainsKey(c.Klasse))
            .Select(c => c.Klasse)
            .ToArray();

        return new PipeEndpointScanResult(
            true,
            vorschlaege.Length == request.Classes.Count
                ? "Durchlauf abgeschlossen."
                : "Durchlauf abgeschlossen; nicht jede Klasse wurde gefunden.",
            vorschlaege, fehlend, frames.Count, nichtBewertet, actions.Now() - started);

        static PipeEndpointScanResult Leer(string grund) => new(
            false, grund, Array.Empty<PipeEndpointSuggestion>(), Array.Empty<string>(),
            0, 0, TimeSpan.Zero);
    }
}
