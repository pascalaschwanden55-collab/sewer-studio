using AuswertungPro.Next.Application.Ai.Training;

namespace AuswertungPro.Next.UI.Ai.Training;

public static class SelfTrainingReviewCandidateSelector
{
    public static bool HasReviewableMatches(SelfTrainingResult result)
        => result.PartialMatches > 0
        || result.Mismatches > 0
        || result.ExactMatches > 0
        || result.NoFindings > 0;

    public static List<TrainingSample> SelectForRun(
        IEnumerable<TrainingSample> samples,
        SelfTrainingResult result)
    {
        ArgumentNullException.ThrowIfNull(samples);
        ArgumentNullException.ThrowIfNull(result);

        return samples
            .Where(s => s.CaseId == result.CaseId
                && Enum.TryParse<MatchLevel>(s.MatchLevel, ignoreCase: true, out var level)
                && SelfTrainingReviewRouting.ShouldEnqueue(level, s.Status))
            .ToList();
    }
}
