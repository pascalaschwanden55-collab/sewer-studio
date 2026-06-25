using System.Windows;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Ai;

public enum LiveDetectionMarkCatalogOpenWorkflowOutcome
{
    Ignored,
    Opened
}

public sealed record LiveDetectionMarkCatalogCanvasClickRequest(
    bool IsManualMarkMode,
    Point ClickPoint,
    Size CanvasSize,
    double TimestampSec);

public sealed record LiveDetectionMarkCatalogFindingClickRequest(
    string? ClockPosition,
    double TimestampSec,
    string? SuggestedCode);

public sealed record LiveDetectionMarkCatalogOpenWorkflowActions(
    Action<bool> SetPause,
    Action<string?, double, string?> OpenCodeCatalog);

public sealed record LiveDetectionMarkCatalogOpenWorkflowResult(
    LiveDetectionMarkCatalogOpenWorkflowOutcome Outcome)
{
    public bool Handled => Outcome == LiveDetectionMarkCatalogOpenWorkflowOutcome.Opened;
}

public static class LiveDetectionMarkCatalogOpenWorkflow
{
    public static LiveDetectionMarkCatalogOpenWorkflowResult ExecuteCanvasClick(
        LiveDetectionMarkCatalogCanvasClickRequest request,
        LiveDetectionMarkCatalogOpenWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (!request.IsManualMarkMode)
            return Ignored();

        if (request.CanvasSize.Width < 60 || request.CanvasSize.Height < 60)
            return Ignored();

        var clockPosition = LiveDetectionGeometryMapper.ClickToClockPosition(
            request.ClickPoint,
            request.CanvasSize);
        return Open(clockPosition, request.TimestampSec, suggestedCode: null, actions);
    }

    public static LiveDetectionMarkCatalogOpenWorkflowResult ExecuteFindingClick(
        LiveDetectionMarkCatalogFindingClickRequest request,
        LiveDetectionMarkCatalogOpenWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        return Open(request.ClockPosition, request.TimestampSec, request.SuggestedCode, actions);
    }

    private static LiveDetectionMarkCatalogOpenWorkflowResult Open(
        string? clockPosition,
        double timestampSec,
        string? suggestedCode,
        LiveDetectionMarkCatalogOpenWorkflowActions actions)
    {
        PlayerManualMarkPlayback.PauseForManualMarking(actions.SetPause);
        actions.OpenCodeCatalog(clockPosition, timestampSec, suggestedCode);
        return new LiveDetectionMarkCatalogOpenWorkflowResult(
            LiveDetectionMarkCatalogOpenWorkflowOutcome.Opened);
    }

    private static LiveDetectionMarkCatalogOpenWorkflowResult Ignored()
        => new(LiveDetectionMarkCatalogOpenWorkflowOutcome.Ignored);
}
