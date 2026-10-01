using System;
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Application.UseCases.BendSuggestions;

namespace AuswertungPro.Next.Application.UseCases.CodingSuggestions;

public enum CodingSuggestionConfirmAction
{
    /// <summary>Codierfenster mit vorgewaehltem Hauptcode oeffnen; der Mensch waehlt die Richtung.</summary>
    OpenCodeWindow = 0,

    /// <summary>Grenzereignis (BCD/BCE) direkt anlegen.</summary>
    CreateBoundaryEvent = 1,

    /// <summary>Das Grenzereignis existiert schon — nur springen, nichts anlegen.</summary>
    AlreadyPresent = 2
}

/// <param name="Meter">Vorgabemeter; null = normale Meterermittlung des Codiermodus.</param>
/// <param name="ProposeLength">True = Haltungslaenge fehlt und ein gelesener (nicht geschaetzter) Meter liegt vor.</param>
public sealed record CodingSuggestionConfirmPlan(
    CodingSuggestionConfirmAction Action,
    string Code,
    double? Meter,
    bool ProposeLength,
    string Hinweis);

/// <summary>
/// Entscheidet ohne WPF, was "Bestaetigen" tut. Ein geschaetzter Meter wird nie
/// als Vorgabe oder Laenge verwendet; ein vorhandenes BCD/BCE wird nie verdoppelt.
/// </summary>
public static class CodingSuggestionConfirmPolicy
{
    public static CodingSuggestionConfirmPlan Plan(
        CodingSuggestion vorschlag,
        IReadOnlyList<MeterTrackPoint> meterTrack,
        IReadOnlyCollection<string> activeCodes,
        bool hasHoldingLength)
    {
        ArgumentNullException.ThrowIfNull(vorschlag);
        ArgumentNullException.ThrowIfNull(meterTrack);
        ArgumentNullException.ThrowIfNull(activeCodes);

        switch (vorschlag.Kind)
        {
            case CodingSuggestionKind.Bogen:
                return new CodingSuggestionConfirmPlan(
                    CodingSuggestionConfirmAction.OpenCodeWindow,
                    "BCC",
                    vorschlag.MeterIsEstimated ? null : vorschlag.Meter,
                    ProposeLength: false,
                    Hinweis: string.Empty);

            case CodingSuggestionKind.Rohranfang:
                if (HatCode(activeCodes, "BCD"))
                    return Vorhanden("BCD", "Rohranfang ist bereits codiert.");
                return new CodingSuggestionConfirmPlan(
                    CodingSuggestionConfirmAction.CreateBoundaryEvent, "BCD", 0.0, false, string.Empty);

            case CodingSuggestionKind.Rohrende:
            {
                if (HatCode(activeCodes, "BCE"))
                    return Vorhanden("BCE", "Rohrende ist bereits codiert.");

                var punkt = CodingSuggestionMeterLookup.Find(meterTrack, vorschlag.PeakTimeSeconds);
                var meter = punkt is { IsEstimated: false } ? punkt.Meter : (double?)null;
                return new CodingSuggestionConfirmPlan(
                    CodingSuggestionConfirmAction.CreateBoundaryEvent,
                    "BCE",
                    meter,
                    ProposeLength: meter.HasValue && !hasHoldingLength,
                    Hinweis: string.Empty);
            }

            default:
                throw new ArgumentOutOfRangeException(nameof(vorschlag), vorschlag.Kind, null);
        }
    }

    private static bool HatCode(IReadOnlyCollection<string> codes, string code)
        => codes.Any(c => string.Equals(c?.Trim(), code, StringComparison.OrdinalIgnoreCase));

    private static CodingSuggestionConfirmPlan Vorhanden(string code, string hinweis)
        => new(CodingSuggestionConfirmAction.AlreadyPresent, code, null, false, hinweis);
}
