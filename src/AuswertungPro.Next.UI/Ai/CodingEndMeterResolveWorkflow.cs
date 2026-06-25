namespace AuswertungPro.Next.UI.Ai;

public enum CodingEndMeterResolveOutcome
{
    NoCodingContext,
    Resolved
}

public sealed record CodingEndMeterResolveRequest(
    bool HasCodingViewModel);

public sealed record CodingEndMeterResolveActions(
    Func<double> ResolveEndMeter);

public sealed record CodingEndMeterResolveResult(
    CodingEndMeterResolveOutcome Outcome,
    double? EndMeter);

public static class CodingEndMeterResolveWorkflow
{
    public static CodingEndMeterResolveResult Execute(
        CodingEndMeterResolveRequest request,
        CodingEndMeterResolveActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (!request.HasCodingViewModel)
            return Result(CodingEndMeterResolveOutcome.NoCodingContext, endMeter: null);

        return Result(
            CodingEndMeterResolveOutcome.Resolved,
            actions.ResolveEndMeter());
    }

    private static CodingEndMeterResolveResult Result(
        CodingEndMeterResolveOutcome outcome,
        double? endMeter)
        => new(outcome, endMeter);
}
