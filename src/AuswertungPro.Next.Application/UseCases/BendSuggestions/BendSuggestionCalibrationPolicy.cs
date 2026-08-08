using System;

namespace AuswertungPro.Next.Application.UseCases.BendSuggestions;

/// <summary>
/// Gemessener Arbeitspunkt genau eines Bogen-Gewichts.
/// </summary>
public sealed record BendSuggestionCalibration
{
    public required string CandidateId { get; init; }

    /// <summary>SHA-256 des Gewichts, an das dieser Arbeitspunkt gebunden ist.</summary>
    public required string WeightSha256 { get; init; }

    public required double MinConfidence { get; init; }

    public required double StrongConfidence { get; init; }

    /// <summary>Woher die Werte stammen — ohne Beleg gilt der Arbeitspunkt als geraten.</summary>
    public required string Source { get; init; }
}

/// <summary>Ergebnis der Pruefung: brauchbar samt Grenzen, oder Grund der Ablehnung.</summary>
public sealed record BendSuggestionCalibrationResult(
    bool IsUsable,
    BendSuggestionOptions? Options,
    string Reason);

/// <summary>
/// Bindet den gemessenen Arbeitspunkt an genau ein Gewicht.
///
/// Warum das noetig ist: Drei Modelle aus identischen Daten und Einstellungen,
/// nur der Zufallsstartwert unterschiedlich, verhalten sich bei derselben Schwelle
/// voellig verschieden. Bei conf 0,50 fanden Seed 44 und 46 je sieben von zehn
/// protokollierten Boegen, Seed 45 nur zwei. Ein Arbeitspunkt ohne Bindung an das
/// Gewicht ist deshalb wertlos, und ein Gewicht ohne gemessenen Arbeitspunkt darf
/// gar nicht erst als Vorschlagsquelle angeboten werden.
///
/// Fail-closed wie der Sidecar-Waechter: Im Zweifel kein Vorschlag.
/// </summary>
public static class BendSuggestionCalibrationPolicy
{
    public static BendSuggestionCalibrationResult Resolve(
        BendSuggestionCalibration? calibration,
        string candidateId,
        string weightSha256)
    {
        if (calibration is null)
        {
            return Reject(
                "Fuer diesen Kandidaten ist kein gemessener Arbeitspunkt hinterlegt.");
        }

        if (string.IsNullOrWhiteSpace(candidateId) || string.IsNullOrWhiteSpace(weightSha256))
            return Reject("Kandidat und Gewicht muessen benannt sein.");

        if (!string.Equals(calibration.CandidateId, candidateId, StringComparison.Ordinal))
        {
            return Reject(
                $"Der Arbeitspunkt gehoert zum Kandidaten {calibration.CandidateId}, "
                + $"angefragt wurde {candidateId}.");
        }

        if (!string.Equals(calibration.WeightSha256, weightSha256, StringComparison.OrdinalIgnoreCase))
        {
            return Reject(
                "Das Gewicht weicht von dem ab, an dem der Arbeitspunkt gemessen wurde.");
        }

        if (string.IsNullOrWhiteSpace(calibration.Source))
            return Reject("Der Arbeitspunkt traegt keinen Beleg seiner Herkunft.");

        if (calibration.MinConfidence <= 0.0
            || calibration.MinConfidence > 1.0
            || calibration.StrongConfidence > 1.0
            || calibration.StrongConfidence < calibration.MinConfidence)
        {
            return Reject(
                "Die Grenzen sind unbrauchbar: Der Arbeitspunkt muss zwischen 0 und 1 liegen "
                + "und die starke Grenze darf nicht darunter liegen.");
        }

        return new BendSuggestionCalibrationResult(
            true,
            new BendSuggestionOptions
            {
                MinConfidence = calibration.MinConfidence,
                StrongConfidence = calibration.StrongConfidence
            },
            string.Empty);
    }

    private static BendSuggestionCalibrationResult Reject(string reason)
        => new(false, null, reason);
}
