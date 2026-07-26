using System;

namespace AuswertungPro.Next.Application.Ai.Training;

/// <summary>
/// Erkennt persoenliche Gold-Entwuerfe (Status=Draft): menschlich bestaetigt und abgespeichert,
/// aber noch ohne gepruefte SAM-Maske — sie gehoeren in die Reparatur-Queue und zaehlen im
/// Goldstand/Album als "unvollstaendig". Ergaenzt <see cref="ManualGoldTrainingPolicy"/>, die
/// bewusst nur voll Freigegebenes (Approved) akzeptiert und unveraendert bleibt.
/// </summary>
public static class GoldDraftMatcher
{
    /// <summary>
    /// True, wenn das Sample ein Entwurf der eigenen Person ist (gleiche Nutzerbindung wie
    /// <see cref="ManualGoldTrainingPolicy.IsManuallyConfirmed(TrainingSample, string?)"/>,
    /// aber Status=Draft statt Approved).
    /// </summary>
    public static bool IsOwnDraft(TrainingSample sample, string? confirmedByUser)
    {
        ArgumentNullException.ThrowIfNull(sample);

        return sample.Status == TrainingSampleStatus.Draft
               && sample.HumanConfirmed == true
               && !string.IsNullOrWhiteSpace(confirmedByUser)
               && string.Equals(
                   sample.ConfirmedByUser?.Trim(),
                   confirmedByUser.Trim(),
                   StringComparison.OrdinalIgnoreCase);
    }
}
