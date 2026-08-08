using System;

namespace AuswertungPro.Next.Application.Ai.Training;

/// <summary>
/// Entscheidet, ob ein Trainingssample zum Messen eines Modells taugt.
///
/// Ein Copilot verzerrt die Daten, die er selbst erzeugt: Wer einen Vorschlag
/// sieht, codiert anders. Belegt am 2026-08-07 — dieselbe Stelle einer Haltung
/// wurde mit sichtbarem Modellrahmen als Bogen codiert und ohne ihn als
/// verschobene Rohrverbindung mit Knick. Wird ein Modell an solchem Material
/// gemessen, bewertet es sich selbst und wird dabei sicherer statt besser.
///
/// Die Regel ist bewusst fail-closed: Nur eine ausdrueckliche Angabe
/// <see cref="TrainingSampleSuggestionOrigin.Independent"/> erlaubt das Messen.
/// Der gesamte Altbestand ohne Herkunftsangabe bleibt Trainingsmaterial, wird
/// aber nie zur Messgrundlage.
/// </summary>
public static class SuggestionProvenancePolicy
{
    public static TrainingSampleSuggestionOrigin ResolveOrigin(TrainingSample? sample) =>
        sample?.SuggestionProvenance?.Origin ?? TrainingSampleSuggestionOrigin.Unknown;

    /// <summary>
    /// Darf dieses Sample ein Modell messen? Nur bei ausdruecklich unabhaengiger
    /// Entstehung. Unbekannt und beeinflusst zaehlen beide nicht.
    /// </summary>
    public static bool IsUnbiasedForMeasurement(TrainingSample? sample) =>
        ResolveOrigin(sample) == TrainingSampleSuggestionOrigin.Independent;

    /// <summary>
    /// Traegt das Sample Information, die das Modell noch nicht hat? Eine blosse
    /// Zustimmung zu einem richtigen Vorschlag bestaetigt nur Bekanntes. Eine
    /// Korrektur — oder ein selbst gefundener Befund — bringt Neues.
    /// </summary>
    public static bool CarriesNewInformation(TrainingSample? sample)
    {
        if (sample is null)
            return false;

        var origin = ResolveOrigin(sample);
        if (origin == TrainingSampleSuggestionOrigin.Independent)
            return true;
        if (origin == TrainingSampleSuggestionOrigin.Unknown)
            return false;

        if (sample.Corrected == true)
            return true;

        // Der Codiermodus setzt Corrected nicht auf jedem Weg. Weicht der
        // gespeicherte Code vom Vorschlag ab, ist das ebenfalls eine Korrektur.
        var vorschlag = sample.SuggestionProvenance?.SuggestedCode;
        return !string.IsNullOrWhiteSpace(vorschlag)
            && !string.Equals(vorschlag, sample.Code, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Klartext fuer Berichte und Oberflaeche, warum ein Sample nicht messen darf.
    /// Leer, wenn es messen darf.
    /// </summary>
    public static string DescribeMeasurementBias(TrainingSample? sample)
    {
        var origin = ResolveOrigin(sample);
        if (origin == TrainingSampleSuggestionOrigin.Independent)
            return string.Empty;
        if (origin == TrainingSampleSuggestionOrigin.Unknown)
            return "Herkunft unbekannt: Es ist nicht belegt, ob beim Codieren ein "
                + "Modellvorschlag sichtbar war.";

        var modell = sample?.SuggestionProvenance?.ModelId;
        return string.IsNullOrWhiteSpace(modell)
            ? "Beim Codieren war ein Modellvorschlag sichtbar."
            : $"Beim Codieren war ein Vorschlag des Modells {modell} sichtbar.";
    }
}
