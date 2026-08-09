using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.UseCases.BendSuggestions;

namespace AuswertungPro.Next.Infrastructure.Ai.BendSuggestions;

/// <summary>
/// Fragt den gepinnten Bogen-Kandidaten zu einem Bild und uebersetzt die
/// Sidecar-Antwort in genau drei Ausgaenge: Bogen gefunden, kein Bogen, nicht
/// ausgewertet.
///
/// Alles andere ist ein technischer Fehler und wird geworfen. Er darf nie als
/// "kein Bogen" erscheinen — "nichts gefunden" und "nichts gesehen" sind
/// verschiedene Aussagen.
///
/// Kandidaten-ID und Gewicht-Hash gehen mit jeder Anfrage mit und werden an der
/// Antwort erneut geprueft. Ohne beide waehlt der Sidecar selbst, und zwar nach
/// hoechster interner mAP50 — das waere derzeit der Kandidat mit den meisten
/// Fehlalarmen. Ein stiller Modellwechsel ist schlimmer als ein Abbruch, weil der
/// kalibrierte Arbeitspunkt nur fuer genau ein Gewicht gilt.
/// </summary>
public sealed class BendFrameDetector
{
    /// <summary>Nur diese Klasse zaehlt; der Sidecar filtert bereits, das ist die zweite Grenze.</summary>
    private const string BendClassName = "BCC_bogen";

    private readonly string _candidateId;
    private readonly string _weightSha256;
    private readonly double _floorConfidence;
    private readonly Func<BccTestYoloRequest, CancellationToken, Task<BccTestYoloResponse>> _ask;

    public BendFrameDetector(
        string candidateId,
        string weightSha256,
        double floorConfidence,
        Func<BccTestYoloRequest, CancellationToken, Task<BccTestYoloResponse>> ask)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(candidateId);
        ArgumentException.ThrowIfNullOrWhiteSpace(weightSha256);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(floorConfidence, 0.0);
        _candidateId = candidateId;
        _weightSha256 = weightSha256;
        _floorConfidence = floorConfidence;
        _ask = ask ?? throw new ArgumentNullException(nameof(ask));
    }

    public async Task<BendFrameResult> DetectAsync(
        byte[] imageBytes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(imageBytes);

        var request = new BccTestYoloRequest(
            Convert.ToBase64String(imageBytes),
            _floorConfidence,
            _candidateId,
            _weightSha256);

        var response = await _ask(request, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Der Sidecar hat keine Antwort geliefert.");

        if (!response.Available)
        {
            var detail = string.IsNullOrWhiteSpace(response.Error)
                ? "ohne Begruendung"
                : response.Error;
            throw new InvalidOperationException($"Der Bogen-Kandidat ist nicht verfuegbar: {detail}");
        }

        if (!string.Equals(response.CandidateId, _candidateId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Die Antwort stammt vom Kandidaten {response.CandidateId}, "
                + $"angefragt war {_candidateId}.");
        }

        if (!string.Equals(response.CandidateSha256, _weightSha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Das Gewicht der Antwort weicht vom angefragten ab.");
        }

        // Der rohe OSD-Meterstand desselben Bildes (null = nicht lesbar). Er wird
        // nur durchgereicht; die Folge wird im UseCase plausibilisiert und gefuellt.
        var meter = response.MeterValue;

        if (!response.FrameUsable)
            return BendFrameResult.NotAssessed(response.QualityReason, meter);

        var best = (response.Detections ?? [])
            .Where(detection => string.Equals(
                detection.ClassName, BendClassName, StringComparison.Ordinal))
            .Select(detection => detection.Confidence)
            .DefaultIfEmpty(double.NaN)
            .Max();

        return double.IsNaN(best)
            ? new BendFrameResult(BendFrameOutcome.NoBend, Meter: meter)
            : BendFrameResult.Detected(best, meter);
    }
}
