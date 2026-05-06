using System;
using AuswertungPro.Next.Application.Ai.Vision;

namespace AuswertungPro.Next.Application.Ai.SelfImproving;

public sealed record ReviewQueueItem(
    string Id,
    MappedProtocolEntry? Entry,
    double Priority,
    DateTime EnqueuedUtc
)
{
    /// <summary>True wenn aus Self-Training statt aus Inference-Pipeline.</summary>
    public bool IsFromSelfTraining => SelfTrainingCaseId is not null;

    // Self-Training-Felder (optional)
    public string? SelfTrainingCaseId { get; init; }
    public string? SelfTrainingVsaCode { get; init; }
    public string? SelfTrainingSuggestedCode { get; init; }
    public double? SelfTrainingMeter { get; init; }
    public string? SelfTrainingFramePath { get; init; }
    public string? SelfTrainingMatchLevel { get; init; }
    /// <summary>Stabiler SampleId fuer eindeutiges Mapping bei Review-Korrektur (Finding 2 Fix).</summary>
    public string? SelfTrainingSampleId { get; init; }

    public string Label
    {
        get
        {
            if (IsFromSelfTraining)
            {
                var code = SelfTrainingVsaCode ?? "";
                var klartext = VsaCodeResolver.LookupLabel(code);
                var codeWithLabel = string.IsNullOrWhiteSpace(klartext) ? code : $"{code} — {klartext}";
                return $"{codeWithLabel} @ {SelfTrainingMeter:F1}m ({SelfTrainingMatchLevel})";
            }
            return Entry!.Detection.FindingLabel;
        }
    }
    public string? SuggestedCode
    {
        get
        {
            var raw = IsFromSelfTraining ? SelfTrainingSuggestedCode : Entry?.SuggestedCode;
            if (string.IsNullOrWhiteSpace(raw)) return raw;
            var klartext = VsaCodeResolver.LookupLabel(raw);
            return string.IsNullOrWhiteSpace(klartext) ? raw : $"{raw} — {klartext}";
        }
    }
    public double Confidence => IsFromSelfTraining ? 0 : Entry?.Confidence ?? 0;
    public string PriorityLabel => Priority >= 0.7 ? "Hoch" : Priority >= 0.4 ? "Mittel" : "Niedrig";
}
