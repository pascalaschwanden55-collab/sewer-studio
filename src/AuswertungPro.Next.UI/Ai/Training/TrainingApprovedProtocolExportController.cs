using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.UI.Ai.Training;

public sealed record TrainingApprovedProtocolExportResult(
    int ExportedCount,
    string StatusText,
    IReadOnlyList<string> LogLines);

public static class TrainingApprovedProtocolExportController
{
    public static async Task<TrainingApprovedProtocolExportResult> RunAsync(
        IReadOnlyList<TrainingSample> samples,
        Func<TrainingSample, bool> isExportEligible,
        Action<ProtocolEntry, string?> addProtocolTrainingSample,
        Func<Task> persistSamplesAsync,
        Func<DateTime> utcNow,
        string targetPath)
    {
        ArgumentNullException.ThrowIfNull(samples);
        ArgumentNullException.ThrowIfNull(isExportEligible);
        ArgumentNullException.ThrowIfNull(addProtocolTrainingSample);
        ArgumentNullException.ThrowIfNull(persistSamplesAsync);
        ArgumentNullException.ThrowIfNull(utcNow);

        var candidates = samples
            .Where(s => s.Status == TrainingSampleStatus.Approved && s.ExportedUtc is null)
            .ToList();
        var approved = candidates
            .Where(isExportEligible)
            .ToList();

        if (candidates.Count != approved.Count)
            await persistSamplesAsync().ConfigureAwait(false);

        if (approved.Count == 0)
        {
            return new TrainingApprovedProtocolExportResult(
                0,
                "Keine nicht-exportierten Approved-Samples vorhanden.",
                []);
        }

        var exportTime = utcNow();
        foreach (var sample in approved)
        {
            addProtocolTrainingSample(CreateProtocolEntry(sample), sample.CaseId);
            sample.ExportedUtc = exportTime;
        }

        await persistSamplesAsync().ConfigureAwait(false);

        var codes = approved.Select(s => s.Code).Distinct().OrderBy(c => c).ToList();
        var logLines = new[]
        {
            $"Protokoll-Training: {approved.Count} Samples als Few-Shot-Beispiele gespeichert.",
            $"  Codes: {string.Join(", ", codes)}",
            $"  Ziel: {targetPath}",
            "  Wirkung: Qwen nutzt diese Beispiele bei zuk\u00fcnftigen Protokoll-Generierungen."
        };

        return new TrainingApprovedProtocolExportResult(
            approved.Count,
            $"Protokoll-Training: {approved.Count} Samples als Few-Shot-Beispiele gespeichert ({codes.Count} Codes).",
            logLines);
    }

    private static ProtocolEntry CreateProtocolEntry(TrainingSample sample)
        => new()
        {
            Code = sample.Code,
            Beschreibung = sample.Beschreibung,
            MeterStart = sample.MeterStart,
            MeterEnd = sample.MeterEnd,
            IsStreckenschaden = sample.IsStreckenschaden
        };
}
