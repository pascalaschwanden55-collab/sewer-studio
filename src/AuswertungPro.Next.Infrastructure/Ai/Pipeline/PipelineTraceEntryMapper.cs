using AuswertungPro.Next.Application.Ai;

namespace AuswertungPro.Next.Infrastructure.Ai.Pipeline;

internal static class PipelineTraceEntryMapper
{
    public static PipelineTraceEntry Map(PipelineFrameTrace source) => new()
    {
        RunId = source.RunId,
        TimestampUtc = source.TimestampUtc,
        FrameIndex = source.FrameIndex,
        TimeSec = source.TimeSec,
        Meter = source.Meter,
        Path = source.Path,
        YoloBypass = source.YoloBypass,
        YoloRelevant = source.YoloRelevant,
        YoloDetectionCount = source.YoloDetectionCount,
        DinoBoxCount = source.DinoBoxCount,
        SamMaskCount = source.SamMaskCount,
        FindingsBuilt = source.FindingsBuilt,
        CodesFromLabel = source.CodesFromLabel,
        ClassifierCode = source.ClassifierCode,
        ClassifierConfidence = source.ClassifierConfidence,
        ClassifierSource = source.ClassifierSource,
        ClassifierModel = source.ClassifierModel,
        ClassifierVoteConfirmed = source.ClassifierVoteConfirmed,
        QwenCalled = source.QwenCalled,
        QwenImageQuality = source.QwenImageQuality,
        QwenRawFindingCount = source.QwenRawFindingCount,
        CodesAfterQwen = source.CodesAfterQwen,
        FindingsEndOfFrame = source.FindingsEndOfFrame,
        ActiveCount = source.ActiveCount,
        DetectionsTotal = source.DetectionsTotal,
        DropReason = source.DropReason,
        Degraded = source.Degraded,
        DegradedReason = source.DegradedReason
    };
}
