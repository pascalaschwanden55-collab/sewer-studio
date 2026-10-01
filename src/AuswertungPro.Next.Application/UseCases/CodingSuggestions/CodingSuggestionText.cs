using System;
using System.Globalization;

namespace AuswertungPro.Next.Application.UseCases.CodingSuggestions;

/// <summary>
/// Der Zeilentext der Vorschlagsliste. Ein fehlender Meterstand heisst
/// "nicht lesbar", niemals 0,0; ein gefuellter Wert heisst "ca.".
/// </summary>
public static class CodingSuggestionText
{
    // de-DE wie im Training Studio: Windows fuehrt de-CH mit Punkt, die Anzeige soll das Komma tragen.
    private static readonly CultureInfo Deutsch = CultureInfo.GetCultureInfo("de-DE");

    public static string Zeile(CodingSuggestion vorschlag)
    {
        ArgumentNullException.ThrowIfNull(vorschlag);

        return vorschlag.Kind switch
        {
            CodingSuggestionKind.Bogen => $"Bogen · {Ort(vorschlag)} · {(vorschlag.IsStrong ? "stark" : "schwach")}",
            CodingSuggestionKind.Rohranfang => $"Rohranfang · Sekunde {Sekunde(vorschlag)} · Abnahme {Prozent(vorschlag.AcceptancePrecision)}",
            CodingSuggestionKind.Rohrende => $"Rohrende · Sekunde {Sekunde(vorschlag)} · Abnahme {Prozent(vorschlag.AcceptancePrecision)}",
            _ => throw new ArgumentOutOfRangeException(nameof(vorschlag), vorschlag.Kind, null)
        };
    }

    public static string Art(CodingSuggestionKind kind) => kind switch
    {
        CodingSuggestionKind.Bogen => "Bogen",
        CodingSuggestionKind.Rohranfang => "Rohranfang",
        CodingSuggestionKind.Rohrende => "Rohrende",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };

    private static string Ort(CodingSuggestion v)
    {
        if (v.Meter is not { } meter)
            return $"Sekunde {Sekunde(v)} (Meterstand nicht lesbar)";
        return v.MeterIsEstimated
            ? $"Meter ca. {meter.ToString("0.0", Deutsch)}"
            : $"Meter {meter.ToString("0.00", Deutsch)}";
    }

    private static string Sekunde(CodingSuggestion v)
        => Math.Floor(v.PeakTimeSeconds).ToString("0", CultureInfo.InvariantCulture);

    private static string Prozent(double anteil)
        => $"{Math.Round(anteil * 100.0).ToString("0", CultureInfo.InvariantCulture)} %";
}
