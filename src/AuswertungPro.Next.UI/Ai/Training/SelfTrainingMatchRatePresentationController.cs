namespace AuswertungPro.Next.UI.Ai.Training;

public sealed record SelfTrainingMatchRatePresentationUi(
    Action<double> SetExactPercent,
    Action<double> SetPartialPercent,
    Action<double> SetMismatchPercent,
    Action<double> SetNoFindingsPercent);

public static class SelfTrainingMatchRatePresentationController
{
    public static void Apply(
        SelfTrainingStatusCalculator.MatchRatePercents percents,
        SelfTrainingMatchRatePresentationUi ui)
    {
        ArgumentNullException.ThrowIfNull(ui);

        ui.SetExactPercent(percents.Exact);
        ui.SetPartialPercent(percents.Partial);
        ui.SetMismatchPercent(percents.Mismatch);
        ui.SetNoFindingsPercent(percents.NoFindings);
    }

    public static void Apply(
        SelfTrainingLastMatchRatePresentation presentation,
        SelfTrainingMatchRatePresentationUi ui)
    {
        ArgumentNullException.ThrowIfNull(presentation);
        ArgumentNullException.ThrowIfNull(ui);

        ui.SetExactPercent(presentation.ExactPercent);
        ui.SetPartialPercent(presentation.PartialPercent);
        ui.SetMismatchPercent(presentation.MismatchPercent);
        ui.SetNoFindingsPercent(presentation.NoFindingsPercent);
    }
}
