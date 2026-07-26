using AuswertungPro.Next.Application.Ai.Evaluation;

namespace AuswertungPro.Next.UI.Ai.Coding;

public enum CodingAcceptGreenMatchesCommandOutcome
{
    NoCodingViewModel,
    NoRouting,
    NoOverlay,
    OverlayShown
}

public sealed record CodingAcceptGreenMatchesCommandRequest(
    bool HasCodingViewModel,
    CodingMatchRouting? CurrentRouting);

public sealed record CodingAcceptGreenMatchesCommandActions(
    Action RunProtocolMatch,
    Func<CodingMatchRouting?> GetCurrentRouting,
    Func<CodingMatchRouting, Task<CodingProtocolMatchOverlayState?>> AcceptGreenMatchesAsync,
    Action<CodingProtocolMatchOverlayState> ShowOverlay);

public sealed record CodingAcceptGreenMatchesCommandResult(
    CodingAcceptGreenMatchesCommandOutcome Outcome)
{
    public bool Completed => Outcome == CodingAcceptGreenMatchesCommandOutcome.OverlayShown;
}

public static class CodingAcceptGreenMatchesCommandWorkflow
{
    public static async Task<CodingAcceptGreenMatchesCommandResult> ExecuteAsync(
        CodingAcceptGreenMatchesCommandRequest request,
        CodingAcceptGreenMatchesCommandActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (!request.HasCodingViewModel)
            return Result(CodingAcceptGreenMatchesCommandOutcome.NoCodingViewModel);

        var routing = request.CurrentRouting;
        if (routing is null)
        {
            actions.RunProtocolMatch();
            routing = actions.GetCurrentRouting();
        }

        if (routing is null)
            return Result(CodingAcceptGreenMatchesCommandOutcome.NoRouting);

        var overlay = await actions.AcceptGreenMatchesAsync(routing);
        if (!overlay.HasValue)
            return Result(CodingAcceptGreenMatchesCommandOutcome.NoOverlay);

        actions.ShowOverlay(overlay.Value);
        return Result(CodingAcceptGreenMatchesCommandOutcome.OverlayShown);
    }

    private static CodingAcceptGreenMatchesCommandResult Result(
        CodingAcceptGreenMatchesCommandOutcome outcome)
        => new(outcome);
}
