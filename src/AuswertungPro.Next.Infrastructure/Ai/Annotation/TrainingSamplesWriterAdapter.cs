using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai.Annotation;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Domain.Ai.Training;

namespace AuswertungPro.Next.Infrastructure.Ai.Annotation;

/// <summary>
/// Adapter um den statischen <see cref="TrainingSamplesStore"/>. Erlaubt
/// es <c>OperateurAnnotationService</c> ueber ein Interface zu testen,
/// ohne den Store anfassen zu muessen. Slice 1 (Operateur-Annotation).
/// </summary>
public sealed class TrainingSamplesWriterAdapter : ITrainingSamplesWriter
{
    public Task AppendAsync(TrainingSample sample, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        // AppendOneAsync (kein Signature-Dedup): wenn CommitAsync ein Sample
        // schreibt, soll es im Store landen — auch wenn die Signature mit einem
        // alten Eintrag kollidiert. KB ist fuer Dedup zustaendig (eigener Pfad).
        return TrainingSamplesStore.AppendOneAsync(sample);
    }

    public async Task UpdateIndexStateAsync(string sampleId, KbIndexState state, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        // Best-Effort: wenn das Sample nicht im Store ist, kein Fehler — der
        // Aufrufer hat es entweder schon entfernt oder nie persistiert.
        await TrainingSamplesStore.UpdateIndexStateAsync(sampleId, state).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<TrainingSample>> FindCommittedAsync(
        string caseId,
        string sourceType,
        string code,
        double meter,
        double meterTolerance,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var all = await TrainingSamplesStore.LoadAsync().ConfigureAwait(false);

        var tolerance = Math.Abs(meterTolerance);
        return all
            .Where(s => string.Equals(s.CaseId, caseId, StringComparison.OrdinalIgnoreCase))
            .Where(s => string.Equals(s.SourceType, sourceType, StringComparison.OrdinalIgnoreCase))
            .Where(s => string.Equals(s.Code, code, StringComparison.OrdinalIgnoreCase))
            .Where(s => Math.Abs(s.MeterStart - meter) <= tolerance)
            .ToList();
    }
}
