using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace AuswertungPro.Next.Application.UseCases.ModelPromotion;

/// <summary>Ein Messwert eines Modells auf einem eingefrorenen Bestand.</summary>
/// <param name="SetId">Kennung des Messbestands.</param>
/// <param name="SetSha256">Manifest-Hash des Bestands — bindet an genau eine Version.</param>
/// <param name="Seed">Zufallsstartwert des Laufs.</param>
/// <param name="Recall">Anteil gefundener Sollbefunde.</param>
/// <param name="FalseAlarmRate">Anteil Bilder ohne Befund, auf denen das Modell feuert.</param>
public sealed record ModelMeasurement(
    string SetId,
    string SetSha256,
    int Seed,
    double Recall,
    double FalseAlarmRate);

/// <summary>Auftrag der Tauschentscheidung.</summary>
public sealed record ModelPromotionRequest
{
    public required IReadOnlyList<ModelMeasurement> Incumbent { get; init; }

    public required IReadOnlyList<ModelMeasurement> Candidate { get; init; }

    /// <summary>Einzellaeufe sind keine Belege — drei Seeds je Bedingung.</summary>
    public int MinimumSeeds { get; init; } = 3;
}

/// <summary>Ergebnis samt Spannen, damit nie der beste Lauf berichtet wird.</summary>
public sealed record ModelPromotionDecision(
    bool Promote,
    string Reason,
    double IncumbentMean,
    double IncumbentMinimum,
    double IncumbentMaximum,
    double CandidateMean,
    double CandidateMinimum,
    double CandidateMaximum);

/// <summary>
/// Entscheidet, ob ein neues Modell das bestehende ersetzen darf.
///
/// Drei Regeln, jede aus einem Fehler dieser Woche entstanden:
///
/// 1. <b>Drei Laeufe je Bedingung.</b> Ein Einzellauf sah wie ein Fortschritt aus
///    und war Seed-Glueck: Drei identische Laeufe fanden 20, 25 und 28 von 37
///    Boegen. Der beste von drei liegt systematisch ueber dem Erwartungswert.
///
/// 2. <b>Derselbe eingefrorene Bestand.</b> Werden Modell und Messgrundlage
///    zugleich gewechselt, misst der Vergleich den Bestand statt das Modell. Ein
///    Lauf aendert genau eine Sache.
///
/// 3. <b>Der Gewinn muss groesser sein als die Streuung</b> — und die
///    Fehlalarmquote darf sich nicht ueber ihre eigene Streuung hinaus
///    verschlechtern. Mehr Treffer bei mehr Fehlalarmen ist kein Fortschritt.
///
/// Berichtet wird immer die Spanne, nie der beste Lauf.
/// </summary>
public static class ModelPromotionPolicy
{
    public static ModelPromotionDecision Decide(ModelPromotionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var incumbent = request.Incumbent ?? [];
        var candidate = request.Candidate ?? [];
        if (incumbent.Count == 0 || candidate.Count == 0)
            return Reject("Es fehlen Messungen fuer das bestehende Modell oder den Kandidaten.");

        if (incumbent.Count < request.MinimumSeeds || candidate.Count < request.MinimumSeeds)
        {
            return Reject(
                $"Es braucht mindestens {request.MinimumSeeds} Laeufe je Seite; vorhanden sind "
                + $"{incumbent.Count} und {candidate.Count}. Einzellaeufe sind keine Belege.",
                incumbent, candidate);
        }

        var sets = incumbent.Concat(candidate)
            .Select(measurement => (measurement.SetId, Sha: measurement.SetSha256.ToLowerInvariant()))
            .Distinct()
            .ToList();
        if (sets.Count != 1)
        {
            return Reject(
                "Die Messungen stammen nicht vom selben eingefrorenen Messbestand. Ein Lauf "
                + "aendert genau eine Sache — Modell ODER Bestand.",
                incumbent, candidate);
        }

        var recall = Spanne(incumbent, candidate, measurement => measurement.Recall);
        var alarm = Spanne(incumbent, candidate, measurement => measurement.FalseAlarmRate);

        var gewinn = recall.CandidateMean - recall.IncumbentMean;
        var streuung = Math.Max(
            recall.IncumbentMaximum - recall.IncumbentMinimum,
            recall.CandidateMaximum - recall.CandidateMinimum);

        if (gewinn <= streuung)
        {
            return new ModelPromotionDecision(
                false,
                $"Der Gewinn von {Prozent(gewinn)} liegt nicht ueber der gemessenen Streuung von "
                + $"{Prozent(streuung)}. Nicht nachweisbar ist nicht dasselbe wie besser.",
                recall.IncumbentMean, recall.IncumbentMinimum, recall.IncumbentMaximum,
                recall.CandidateMean, recall.CandidateMinimum, recall.CandidateMaximum);
        }

        var alarmZuwachs = alarm.CandidateMean - alarm.IncumbentMean;
        var alarmStreuung = Math.Max(
            alarm.IncumbentMaximum - alarm.IncumbentMinimum,
            alarm.CandidateMaximum - alarm.CandidateMinimum);
        if (alarmZuwachs > alarmStreuung)
        {
            return new ModelPromotionDecision(
                false,
                $"Die Fehlalarmquote steigt um {Prozent(alarmZuwachs)} und damit ueber ihre "
                + $"eigene Streuung von {Prozent(alarmStreuung)}. Mehr Treffer bei mehr "
                + "Fehlalarmen ist kein Fortschritt.",
                recall.IncumbentMean, recall.IncumbentMinimum, recall.IncumbentMaximum,
                recall.CandidateMean, recall.CandidateMinimum, recall.CandidateMaximum);
        }

        return new ModelPromotionDecision(
            true,
            $"Der Gewinn von {Prozent(gewinn)} ist groesser als die Streuung von "
            + $"{Prozent(streuung)}, und die Fehlalarmquote verschlechtert sich nicht darueber "
            + "hinaus.",
            recall.IncumbentMean, recall.IncumbentMinimum, recall.IncumbentMaximum,
            recall.CandidateMean, recall.CandidateMinimum, recall.CandidateMaximum);
    }

    private static (double IncumbentMean, double IncumbentMinimum, double IncumbentMaximum,
        double CandidateMean, double CandidateMinimum, double CandidateMaximum) Spanne(
        IReadOnlyList<ModelMeasurement> incumbent,
        IReadOnlyList<ModelMeasurement> candidate,
        Func<ModelMeasurement, double> auswahl)
    {
        var alt = incumbent.Select(auswahl).ToList();
        var neu = candidate.Select(auswahl).ToList();
        return (alt.Average(), alt.Min(), alt.Max(), neu.Average(), neu.Min(), neu.Max());
    }

    private static string Prozent(double anteil)
        => (anteil * 100).ToString("0.0", CultureInfo.InvariantCulture) + " Prozentpunkten";

    private static ModelPromotionDecision Reject(
        string reason,
        IReadOnlyList<ModelMeasurement>? incumbent = null,
        IReadOnlyList<ModelMeasurement>? candidate = null)
    {
        var alt = (incumbent ?? []).Select(measurement => measurement.Recall).ToList();
        var neu = (candidate ?? []).Select(measurement => measurement.Recall).ToList();
        return new ModelPromotionDecision(
            false, reason,
            alt.Count > 0 ? alt.Average() : 0.0, alt.Count > 0 ? alt.Min() : 0.0,
            alt.Count > 0 ? alt.Max() : 0.0,
            neu.Count > 0 ? neu.Average() : 0.0, neu.Count > 0 ? neu.Min() : 0.0,
            neu.Count > 0 ? neu.Max() : 0.0);
    }
}
