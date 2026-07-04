using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SelfTrainingMatchRatePresentationControllerTests
{
    [Fact]
    public void Apply_from_tracker_percents_setzt_alle_werte()
    {
        var calls = new List<string>();

        SelfTrainingMatchRatePresentationController.Apply(
            new SelfTrainingStatusCalculator.MatchRatePercents(
                Exact: 0.1,
                Partial: 0.2,
                Mismatch: 0.3,
                NoFindings: 0.4),
            new SelfTrainingMatchRatePresentationUi(
                value => calls.Add($"exact:{value}"),
                value => calls.Add($"partial:{value}"),
                value => calls.Add($"mismatch:{value}"),
                value => calls.Add($"none:{value}")));

        Assert.Equal(["exact:0.1", "partial:0.2", "mismatch:0.3", "none:0.4"], calls);
    }

    [Fact]
    public void Apply_from_last_run_presentation_setzt_alle_werte()
    {
        var calls = new List<string>();

        SelfTrainingMatchRatePresentationController.Apply(
            new SelfTrainingLastMatchRatePresentation(
                ExactPercent: 0.5,
                PartialPercent: 0.6,
                MismatchPercent: 0.7,
                NoFindingsPercent: 0.8),
            new SelfTrainingMatchRatePresentationUi(
                value => calls.Add($"exact:{value}"),
                value => calls.Add($"partial:{value}"),
                value => calls.Add($"mismatch:{value}"),
                value => calls.Add($"none:{value}")));

        Assert.Equal(["exact:0.5", "partial:0.6", "mismatch:0.7", "none:0.8"], calls);
    }
}
