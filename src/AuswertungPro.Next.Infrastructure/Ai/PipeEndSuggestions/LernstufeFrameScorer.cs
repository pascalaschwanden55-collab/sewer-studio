using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.UseCases.PipeEndSuggestions;

namespace AuswertungPro.Next.Infrastructure.Ai.PipeEndSuggestions;

/// <summary>
/// Fragt eine gepinnte Lernstufe zu einem Bild und liefert die Konfidenz fuer das
/// ganze Bild. Klasse und Gewicht-Hash gehen mit jeder Anfrage mit und werden an
/// der Antwort erneut geprueft.
///
/// Alles ausser einer Konfidenz zwischen 0 und 1 ist ein technischer Fehler und
/// wird geworfen. Er darf nie als "kein Treffer" erscheinen — "nichts gefunden"
/// und "nichts gesehen" sind verschiedene Aussagen. Beide Lernstufen teilen sich
/// im Sidecar einen Modellplatz; antwortet die falsche, ist ein Abbruch besser
/// als eine stille Verwechslung.
/// </summary>
public sealed class LernstufeFrameScorer
{
    /// <summary>Bildgroesse der Abnahme (cls_runs/*_640). Andere Werte messen ein anderes Modellverhalten.</summary>
    public const int ImageSize = 640;

    private readonly Func<LernstufeRequest, CancellationToken, Task<LernstufeResponse>> _ask;

    public LernstufeFrameScorer(Func<LernstufeRequest, CancellationToken, Task<LernstufeResponse>> ask)
    {
        _ask = ask ?? throw new ArgumentNullException(nameof(ask));
    }

    public async Task<double> ScoreAsync(
        byte[] imageBytes,
        PipeEndLernstufePin pin,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(imageBytes);
        ArgumentNullException.ThrowIfNull(pin);

        var request = new LernstufeRequest(
            Convert.ToBase64String(imageBytes),
            pin.Klasse,
            pin.WeightSha256,
            ImageSize);

        var response = await _ask(request, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Der Sidecar hat keine Antwort geliefert.");

        if (!string.Equals(response.Klasse, pin.Klasse, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Die Antwort stammt von der Klasse {response.Klasse}, angefragt war {pin.Klasse}.");
        }

        if (!string.Equals(response.GewichtSha256, pin.WeightSha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Das Gewicht der Antwort weicht vom angefragten ab.");
        }

        var confidence = response.Konfidenz;
        if (double.IsNaN(confidence) || confidence < 0.0 || confidence > 1.0)
        {
            throw new InvalidOperationException(
                $"Die Konfidenz {confidence} liegt ausserhalb von 0 bis 1.");
        }

        return confidence;
    }
}
