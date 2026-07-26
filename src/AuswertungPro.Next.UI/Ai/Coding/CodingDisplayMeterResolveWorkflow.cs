namespace AuswertungPro.Next.UI.Ai.Coding;

public enum CodingDisplayMeterResolveOutcome
{
    NoCodingContext,
    Resolved
}

public sealed record CodingDisplayMeterResolveRequest(
    bool HasCodingViewModel);

public sealed record CodingDisplayMeterResolveActions(
    Func<double> ResolveDisplayMeter);

public sealed record CodingDisplayMeterResolveResult(
    CodingDisplayMeterResolveOutcome Outcome,
    double DisplayMeter);

public static class CodingDisplayMeterResolveWorkflow
{
    public static CodingDisplayMeterResolveResult Execute(
        CodingDisplayMeterResolveRequest request,
        CodingDisplayMeterResolveActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (!request.HasCodingViewModel)
            return Result(CodingDisplayMeterResolveOutcome.NoCodingContext, 0);

        return Result(
            CodingDisplayMeterResolveOutcome.Resolved,
            actions.ResolveDisplayMeter());
    }

    private static CodingDisplayMeterResolveResult Result(
        CodingDisplayMeterResolveOutcome outcome,
        double displayMeter)
        => new(outcome, displayMeter);
}
