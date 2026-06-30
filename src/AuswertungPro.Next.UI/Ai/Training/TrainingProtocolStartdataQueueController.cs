using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Protocol;
using InfraSelfImproving = AuswertungPro.Next.Infrastructure.Ai.SelfImproving;

namespace AuswertungPro.Next.UI.Ai.Training;

public sealed record TrainingProtocolStartdataQueueResult(
    int AddedCount,
    int CandidateCount,
    string StatusText,
    string LogText);

public static class TrainingProtocolStartdataQueueController
{
    public static TrainingProtocolStartdataQueueResult Run(
        IReadOnlyList<TrainingSample> samples,
        ICodeCatalogProvider catalog,
        InfraSelfImproving.ReviewQueueService queueService)
    {
        ArgumentNullException.ThrowIfNull(samples);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(queueService);

        var candidates = ProtocolReviewCandidateFilter.SelectCandidates(samples, catalog).ToList();
        var queued = queueService.GetAll()
            .Select(q => q.SelfTrainingSampleId)
            .Where(s => !string.IsNullOrEmpty(s))
            .ToHashSet(StringComparer.Ordinal);

        var added = 0;
        foreach (var sample in candidates)
        {
            if (queued.Contains(sample.SampleId))
                continue;

            queueService.EnqueueFromSelfTraining(
                sample.CaseId,
                sample.Code,
                sample.Code,
                sample.MeterStart,
                sample.FramePath,
                matchLevel: "ProtocolStartdata",
                reason: "Protokoll-Startdaten",
                sampleId: sample.SampleId);
            added++;
        }

        return new TrainingProtocolStartdataQueueResult(
            added,
            candidates.Count,
            $"{added} Protokoll-Startdaten als Kandidaten eingereiht (Freigabe ueber Review).",
            $"Protokoll-Startdaten: {added} Kandidaten eingereiht (von {candidates.Count} gefiltert).");
    }
}
