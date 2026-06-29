using AuswertungPro.Next.Application.Ai.Training;

namespace AuswertungPro.Next.UI.Ai.Training;

public static class SelfTrainingHistorySnapshotBuilder
{
    public static SelfTrainingRunSnapshot? Build(SelfTrainingResult result, DateTime timestampUtc)
    {
        ArgumentNullException.ThrowIfNull(result);

        var matchTotal = result.ExactMatches + result.PartialMatches + result.Mismatches + result.NoFindings;
        if (matchTotal == 0)
            return null;

        return new SelfTrainingRunSnapshot(
            timestampUtc,
            result.CaseId,
            result.TotalEntries,
            (double)result.ExactMatches / matchTotal,
            (double)result.PartialMatches / matchTotal,
            (double)result.Mismatches / matchTotal,
            (double)result.NoFindings / matchTotal);
    }
}
