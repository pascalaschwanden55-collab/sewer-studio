using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Domain.Ai.Training;

namespace AuswertungPro.Next.Application.Ai.Annotation;

/// <summary>
/// Adapter um den statischen TrainingSamplesStore. Drei Methoden mit klarer
/// Semantik fuer Slice 1 (Operateur-Annotation).
/// </summary>
public interface ITrainingSamplesWriter
{
    /// <summary>
    /// Single-Sample-Append. Intern via MergeAndSaveAsync mit Single-Element-Liste
    /// (Dedup via Signature ist OK).
    /// </summary>
    Task AppendAsync(TrainingSample sample, CancellationToken ct);

    /// <summary>
    /// Findet den Sample anhand SampleId und aktualisiert nur den
    /// KbIndexState. Andere Felder bleiben unangetastet.
    /// </summary>
    Task UpdateIndexStateAsync(string sampleId, KbIndexState state, CancellationToken ct);

    /// <summary>
    /// Findet alle bereits committed Samples fuer eine Haltung+Source mit
    /// Code-Filter und Meter-Toleranz um <paramref name="meter"/>. Fuer
    /// Wiederholt-Import-Erkennung. (K1: meter ist Pflicht — sonst ist
    /// die Toleranz sinnlos.)
    /// </summary>
    Task<IReadOnlyList<TrainingSample>> FindCommittedAsync(
        string caseId,
        string sourceType,
        string code,
        double meter,
        double meterTolerance,
        CancellationToken ct);
}
