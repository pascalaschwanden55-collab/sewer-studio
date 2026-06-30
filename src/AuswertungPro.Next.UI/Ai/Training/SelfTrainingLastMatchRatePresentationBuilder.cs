using AuswertungPro.Next.Application.Ai.Training;

namespace AuswertungPro.Next.UI.Ai.Training;

public sealed record SelfTrainingLastMatchRatePresentation(
    double ExactPercent,
    double PartialPercent,
    double MismatchPercent,
    double NoFindingsPercent);

public static class SelfTrainingLastMatchRatePresentationBuilder
{
    public static SelfTrainingLastMatchRatePresentation? Build(IReadOnlyList<SelfTrainingRunSnapshot> runs)
    {
        ArgumentNullException.ThrowIfNull(runs);

        if (runs.Count == 0)
            return null;

        var last = runs[^1];
        return new SelfTrainingLastMatchRatePresentation(
            last.ExactPercent,
            last.PartialPercent,
            last.MismatchPercent,
            last.NoFindingsPercent);
    }
}
