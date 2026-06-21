using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AuswertungPro.Next.Application.Ai.Training;

/// <summary>
/// Ergebnis eines KB-Index-Laufs mit Grund-Unterscheidung:
/// <see cref="IndexedIds"/> = erfolgreich in der KB; <see cref="SkippedIds"/> = bewusst/dauerhaft
/// verworfen (Eval-Schutz / nicht index-wuerdig). Alles, was in KEINER der Listen steht, ist ein
/// echter (transienter) Fehler -> der Aufrufer setzt KbIndexState.Error (Wiederholversuch sinnvoll).
/// </summary>
public sealed record KbIndexOutcome(
    IReadOnlyList<string> IndexedIds,
    IReadOnlyCollection<string> SkippedIds)
{
    public static readonly KbIndexOutcome Empty =
        new(new List<string>(), new List<string>());

    /// <summary>True, wenn das Sample erfolgreich indexiert wurde.</summary>
    public bool IsIndexed(string sampleId) => IndexedIds.Contains(sampleId);

    /// <summary>True, wenn das Sample bewusst/dauerhaft uebersprungen wurde.</summary>
    public bool IsSkipped(string sampleId) => SkippedIds.Contains(sampleId);
}

/// <summary>Abstraktion fuer KB-Indexierung und -Deindexierung von Trainingssamples.</summary>
public interface IKnowledgeBaseIndexer
{
    /// <summary>
    /// Indexiert die uebergebenen Samples in die Knowledge Base. Liefert ein <see cref="KbIndexOutcome"/>,
    /// das erfolgreich indexierte von bewusst uebersprungenen (Skipped) Samples unterscheidet.
    /// </summary>
    Task<KbIndexOutcome> IndexAsync(IReadOnlyList<TrainingSample> samples, CancellationToken ct);

    /// <summary>Entfernt das Sample mit der gegebenen SampleId aus der Knowledge Base.</summary>
    void Deindex(string sampleId);
}
