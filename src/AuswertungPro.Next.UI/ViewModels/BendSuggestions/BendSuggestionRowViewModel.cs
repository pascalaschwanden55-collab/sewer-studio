using System;
using System.Globalization;
using AuswertungPro.Next.Application.UseCases.BendSuggestions;
using AuswertungPro.Next.Application.UseCases.PipeEndSuggestions;

namespace AuswertungPro.Next.UI.ViewModels.BendSuggestions;

/// <summary>
/// Eine Zeile der Vorschlagsliste aus dem Video-Durchlauf: Art, Ort, Stufe,
/// Konfidenz, Anzahl Bilder. Reine Darstellung des Aggregat-Ergebnisses — keine
/// eigene Fachlogik.
///
/// Neben dem Bogen (Kandidat mit Arbeitspunkt) stehen seit 2026-09-04 auch
/// Rohranfang und Rohrende (freigegebene Lernstufen) in derselben Liste. Beide
/// Arten teilen sich Vorschau und Clip ueber die Videozeit.
/// </summary>
public sealed class BendSuggestionRowViewModel
{
    private static readonly CultureInfo Deutsch = CultureInfo.GetCultureInfo("de-DE");

    public BendSuggestionRowViewModel(BendSuggestion suggestion)
    {
        Suggestion = suggestion ?? throw new ArgumentNullException(nameof(suggestion));
        ArtText = "BCC Bogen";
        OrtText = BuildOrtText(suggestion);
        StufeText = suggestion.Strength == BendSuggestionStrength.Strong ? "stark" : "schwach";
        Strength = suggestion.Strength;
        KonfidenzText = suggestion.MaxConfidence.ToString("0.00", Deutsch);
        FrameCount = suggestion.FrameCount;
        PeakTimeSeconds = suggestion.PeakTimeSeconds;
        TimeStartSeconds = suggestion.TimeStartSeconds;
        TimeEndSeconds = suggestion.TimeEndSeconds;
    }

    private BendSuggestionRowViewModel(PipeEndSuggestion suggestion, double precision)
    {
        Suggestion = null;
        ArtText = $"{PipeEndKinds.VsaCode(suggestion.Kind)} {PipeEndKinds.Label(suggestion.Kind)}";
        OrtText = BuildOrtText(suggestion);
        // Es gibt hier kein "stark/schwach": Die Regel liefert genau einen Vorschlag je
        // Video, und seine Trefferquote ist die gemessene Abnahme des Gewichts.
        StufeText = $"Abnahme {Math.Round(precision * 100.0).ToString("0", Deutsch)} %";
        Strength = BendSuggestionStrength.Strong;
        KonfidenzText = suggestion.MaxConfidence.ToString("0.00", Deutsch);
        FrameCount = suggestion.FrameCount;
        PeakTimeSeconds = suggestion.PeakTimeSeconds;
        TimeStartSeconds = suggestion.TimeStartSeconds;
        TimeEndSeconds = suggestion.TimeEndSeconds;
    }

    /// <summary>Zeile fuer Rohranfang oder Rohrende; <paramref name="precision"/> ist die Abnahme des Pins.</summary>
    public static BendSuggestionRowViewModel FromPipeEnd(PipeEndSuggestion suggestion, double precision)
        => new(suggestion ?? throw new ArgumentNullException(nameof(suggestion)), precision);

    /// <summary>Der Bogen-Vorschlag; null bei einer Rohranfang-/Rohrende-Zeile.</summary>
    public BendSuggestion? Suggestion { get; }

    /// <summary>"BCC Bogen", "BCD Rohranfang", "BCE Rohrende" — Code plus Klartext.</summary>
    public string ArtText { get; }

    /// <summary>Ortsangabe, nie "0,0" ohne Wert — siehe <see cref="BuildOrtText(BendSuggestion)"/>.</summary>
    public string OrtText { get; }

    /// <summary>"stark" / "schwach" beim Bogen; "Abnahme 85 %" bei Rohranfang/Rohrende.</summary>
    public string StufeText { get; }

    /// <summary>Stufe als Wert fuer den XAML-DataTrigger (Farbwahl im Fenster).</summary>
    public BendSuggestionStrength Strength { get; }

    public string KonfidenzText { get; }

    public int FrameCount { get; }

    public double PeakTimeSeconds { get; }

    public double TimeStartSeconds { get; }

    public double TimeEndSeconds { get; }

    /// <summary>
    /// Ortstext-Regeln (verbindlich): gelesener Meterstand als "Meter 9,42", Bereich als
    /// "Meter 0,20 – 3,40"; ein geschaetzter Wert traegt den Zusatz " (geschaetzt)"; ohne
    /// jeden Wert "Sekunde 214 (Meterstand nicht lesbar)". Niemals "0,0" schreiben, wenn
    /// kein Wert vorliegt — null bleibt sichtbar "nicht lesbar".
    /// </summary>
    internal static string BuildOrtText(BendSuggestion suggestion)
    {
        if (suggestion.MeterStart is { } start)
        {
            var ort = suggestion.MeterEnd is { } end && end > start
                ? $"Meter {FormatMeter(start)} – {FormatMeter(end)}"
                : $"Meter {FormatMeter(start)}";
            return suggestion.MeterIsEstimated ? ort + " (geschätzt)" : ort;
        }

        var sekunde = (int)Math.Round(suggestion.PeakTimeSeconds);
        return $"Sekunde {sekunde} (Meterstand nicht lesbar)";
    }

    /// <summary>
    /// Die Lernstufen lesen keinen Meterstand; die Angabe bleibt ehrlich die Videosekunde.
    /// "nicht gelesen" statt "nicht lesbar": Es wurde gar nicht versucht.
    /// </summary>
    internal static string BuildOrtText(PipeEndSuggestion suggestion)
        => $"Sekunde {(int)Math.Round(suggestion.PeakTimeSeconds)} (Meterstand nicht gelesen)";

    private static string FormatMeter(double wert) => wert.ToString("0.00", Deutsch);
}
