using System;
using System.Globalization;
using AuswertungPro.Next.Application.UseCases.BendSuggestions;

namespace AuswertungPro.Next.UI.ViewModels.BendSuggestions;

/// <summary>
/// Eine Zeile der Bogen-Vorschlagsliste: Ort, Stufe, Konfidenz, Anzahl Bilder.
/// Reine Darstellung des Aggregat-Ergebnisses — keine eigene Fachlogik.
/// </summary>
public sealed class BendSuggestionRowViewModel
{
    private static readonly CultureInfo Deutsch = CultureInfo.GetCultureInfo("de-DE");

    public BendSuggestionRowViewModel(BendSuggestion suggestion)
    {
        Suggestion = suggestion ?? throw new ArgumentNullException(nameof(suggestion));
        OrtText = BuildOrtText(suggestion);
        StufeText = suggestion.Strength == BendSuggestionStrength.Strong ? "stark" : "schwach";
        KonfidenzText = suggestion.MaxConfidence.ToString("0.00", Deutsch);
    }

    public BendSuggestion Suggestion { get; }

    /// <summary>Ortsangabe, nie "0,0" ohne Wert — siehe <see cref="BuildOrtText"/>.</summary>
    public string OrtText { get; }

    /// <summary>"stark" / "schwach" — die Stufen treffen unterschiedlich oft zu.</summary>
    public string StufeText { get; }

    /// <summary>Stufe als Wert fuer den XAML-DataTrigger (Farbwahl im Fenster).</summary>
    public BendSuggestionStrength Strength => Suggestion.Strength;

    public string KonfidenzText { get; }

    public int FrameCount => Suggestion.FrameCount;

    public double PeakTimeSeconds => Suggestion.PeakTimeSeconds;

    public double TimeStartSeconds => Suggestion.TimeStartSeconds;

    public double TimeEndSeconds => Suggestion.TimeEndSeconds;

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

    private static string FormatMeter(double wert) => wert.ToString("0.00", Deutsch);
}
