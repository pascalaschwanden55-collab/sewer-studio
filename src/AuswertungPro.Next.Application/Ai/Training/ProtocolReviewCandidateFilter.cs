using System.Collections.Generic;
using AuswertungPro.Next.Application.Protocol;

namespace AuswertungPro.Next.Application.Ai.Training;

/// <summary>
/// Waehlt aus Protokoll-Samples die review-faehigen Kandidaten: Status New UND katalog-gueltiger,
/// selektierbarer Code (Phantom-/Rechnungs-Codes und ObservedExtensions raus). Liefert nur die
/// Auswahl — Enqueue/Persistenz uebernimmt der Aufrufer (kein KB-Index).
/// </summary>
public static class ProtocolReviewCandidateFilter
{
    public static IEnumerable<TrainingSample> SelectCandidates(
        IEnumerable<TrainingSample> samples, ICodeCatalogProvider catalog)
    {
        foreach (var s in samples)
        {
            if (s.Status != TrainingSampleStatus.New) continue;
            if (!TrainingSampleEligibility.Evaluate(s, catalog).IsEligible) continue;
            yield return s;
        }
    }
}
