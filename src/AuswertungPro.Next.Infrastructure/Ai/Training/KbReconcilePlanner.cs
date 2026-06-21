using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Application.Ai.Training;

namespace AuswertungPro.Next.Infrastructure.Ai.Training;

/// <summary>
/// Reine, testbare Auswahl-Logik fuer den KB-Nachhol-Lauf ("Gold in KB nachholen").
///
/// Hintergrund: Im Codiermodus bestaetigte Gold-Samples landen in training_samples.json mit
/// Status=Approved, aber ihr KbIndexState bleibt haeufig auf None/Pending stehen (der
/// Index-Versuch im Codiermodus schreibt das Ergebnis nicht zurueck, und es gibt keinen
/// automatischen Nachhol-Mechanismus). Solche Samples sind menschlich bestaetigt, aber nicht
/// in der KnowledgeBase.db auffindbar. Dieser Planner findet genau diese Nachzuegler.
///
/// Bewusst KEINE Indexierung hier — nur Auswahl. Das eigentliche Indexieren laeuft ueber den
/// bestehenden eval-geschuetzten Pfad (KnowledgeBaseManager.IndexSampleAsync), der Eval-Schutz
/// und IsIndexWorthy weiterhin durchsetzt. Rein additiv.
/// </summary>
public static class KbReconcilePlanner
{
    /// <summary>
    /// Liefert alle Samples, die menschlich als Gold bestaetigt (Status=Approved), aber noch
    /// NICHT erfolgreich in die KB indexiert wurden (KbIndexState != Indexed). Schliesst
    /// None (nie versucht), Pending (haengen geblieben) und Error (frueher fehlgeschlagen) ein.
    ///
    /// Rejected/Removed/New bleiben aussen vor — Negativ- und Roh-Samples gehoeren nicht in die
    /// KB der positiven Retrieval-Beispiele.
    /// </summary>
    public static IReadOnlyList<TrainingSample> SelectPending(IEnumerable<TrainingSample> samples)
    {
        if (samples is null)
            return new List<TrainingSample>();

        return samples
            .Where(s => s is not null)
            .Where(s => s.Status == TrainingSampleStatus.Approved)
            .Where(s => IsRetryable(s.KbIndexState))
            .ToList();
    }

    /// <summary>
    /// Zustaende, die ein Nachhol-Lauf erneut versuchen DARF: noch nie gelaufen, haengen geblieben
    /// oder echter (transienter) Fehler. <see cref="KbIndexState.Indexed"/> (fertig) und
    /// <see cref="KbIndexState.Skipped"/> (bewusst/dauerhaft verworfen) werden NICHT erneut versucht –
    /// sonst liefe ein eval-/qualitaetsbedingt verworfenes Sample bei jedem Lauf wieder ins Leere.
    /// </summary>
    private static bool IsRetryable(KbIndexState state)
        => state is KbIndexState.None or KbIndexState.Pending or KbIndexState.Error;

    /// <summary>
    /// Aufschluesselung der Nachzuegler fuer eine ehrliche Laufzeit-Anzeige VOR dem Lauf:
    /// Gesamt = alle wartenden (Approved &amp;&amp; !Indexed); Eligible = davon als trainingsfaehig
    /// markiert (TrainingEligible). Der eigentliche Index-Pfad entscheidet ueber IsIndexWorthy
    /// endgueltig — diese Zahlen dienen nur der Anzeige, NICHT als hartes Filter.
    /// </summary>
    public static (int Total, int Eligible) CountPending(IEnumerable<TrainingSample> samples)
    {
        var pending = SelectPending(samples);
        var eligible = pending.Count(s => s.TrainingEligible);
        return (pending.Count, eligible);
    }
}
