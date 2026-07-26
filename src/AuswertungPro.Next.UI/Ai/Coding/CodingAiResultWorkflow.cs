using System.Windows.Media;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.UI.Ai.Live;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Ai.Coding;

public enum CodingAiResultWorkflowOutcome
{
    Error,
    Warmup,
    NoFindings,
    FindingsShown,
    FindingsHidden
}

public sealed record CodingAiResultWorkflowRequest(
    LiveDetection Result,
    string? ModelName,
    bool HasCodingViewModel,
    bool IsOverlayPopupOpen,
    double PlayerTimeSeconds);

public sealed record CodingAiResultWorkflowActions(
    Action<string, Color, string?> SetAiState,
    Action ClearFindings,
    Action ClearFindingsAndCanvas,
    Action ClearVisuals,
    Action<LiveDetection> UpdateFrameReadiness,
    Func<bool> IsFrameReady,
    Action<LiveDetection> StorePendingWarmupResult,
    Func<int> GetSkippedFrames,
    Func<LiveDetection, LiveDetection> SelectReadyResult,
    Func<LiveDetection, CodingOsdMeterState?> ResolveOsdMeterState,
    Action<CodingOsdMeterState> ApplyOsdMeterState,
    Action<double> MoveSessionToMeter,
    Func<double?, double?, double> ResolveCurrentMeter,
    Func<IReadOnlyList<LiveFrameFinding>, double, IReadOnlyList<LiveFrameFinding>> FilterValidFindings,
    Action<IReadOnlyList<LiveFrameFinding>> ShowFindings,
    Func<IReadOnlyList<LiveFrameFinding>, double, IReadOnlyList<LiveFrameFinding>> SelectFindingsToDraw,
    Action<LiveDetection, IReadOnlyList<LiveFrameFinding>> AddAiFindingsAsEvents,
    Action ShowDetectionOverlay,
    Action<IReadOnlyList<LiveFrameFinding>, double> RenderDetectionOverlay,
    Action ScheduleDetectionAutoHide);

public sealed record CodingAiResultWorkflowResult(
    CodingAiResultWorkflowOutcome Outcome,
    int ValidFindingCount,
    int DrawnFindingCount,
    double? CurrentMeter);

public static class CodingAiResultWorkflow
{
    public static CodingAiResultWorkflowResult Execute(
        CodingAiResultWorkflowRequest request,
        CodingAiResultWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Result);
        ArgumentNullException.ThrowIfNull(actions);

        var result = request.Result;
        if (result.Error != null)
        {
            actions.SetAiState(
                $"Fehler: {result.Error}",
                PlayerStatusColors.Error,
                $"Modell: {LiveDetectionDisplayPolicy.CompactModelName(request.ModelName)}");
            actions.ClearFindings();
            return new CodingAiResultWorkflowResult(
                CodingAiResultWorkflowOutcome.Error,
                ValidFindingCount: 0,
                DrawnFindingCount: 0,
                CurrentMeter: null);
        }

        actions.UpdateFrameReadiness(result);

        if (!actions.IsFrameReady())
        {
            actions.StorePendingWarmupResult(result);
            actions.SetAiState(
                "Dateneinblendung erkannt - uebersprungen",
                PlayerStatusColors.Muted,
                $"Warte auf Videobild... (Bild {actions.GetSkippedFrames()} von 3)");
            actions.ClearFindingsAndCanvas();
            return new CodingAiResultWorkflowResult(
                CodingAiResultWorkflowOutcome.Warmup,
                ValidFindingCount: 0,
                DrawnFindingCount: 0,
                CurrentMeter: null);
        }

        result = actions.SelectReadyResult(result);

        var acceptedOsdMeter = actions.ResolveOsdMeterState(result);
        if (request.HasCodingViewModel && acceptedOsdMeter.HasValue)
        {
            actions.ApplyOsdMeterState(acceptedOsdMeter.Value);
            actions.MoveSessionToMeter(acceptedOsdMeter.Value.Meter);
        }

        var currentMeter = actions.ResolveCurrentMeter(result.TimestampSeconds, result.MeterReading);
        var validFindings = actions.FilterValidFindings(result.Findings, currentMeter);

        if (validFindings.Count == 0)
        {
            var noDamageText = LiveDetectionDisplayPolicy.BuildCodingNoDamageStatusText(result.MeterReading);
            actions.SetAiState(
                noDamageText,
                PlayerStatusColors.Success,
                "Schritt 3 von 3: Overlay aktualisiert");
            actions.ClearFindingsAndCanvas();
            return new CodingAiResultWorkflowResult(
                CodingAiResultWorkflowOutcome.NoFindings,
                ValidFindingCount: 0,
                DrawnFindingCount: 0,
                CurrentMeter: currentMeter);
        }

        var findingsText = LiveDetectionDisplayPolicy.BuildCodingFindingsStatusText(
            result.MeterReading,
            validFindings.Count);
        actions.SetAiState(
            findingsText,
            PlayerStatusColors.Success,
            "Schritt 3 von 3: Overlay und Events");
        actions.ShowFindings(validFindings);

        var findingsToDraw = actions.SelectFindingsToDraw(validFindings, currentMeter);
        actions.AddAiFindingsAsEvents(result, validFindings);

        if (findingsToDraw.Count > 0 && !request.IsOverlayPopupOpen)
        {
            actions.ShowDetectionOverlay();
            actions.RenderDetectionOverlay(findingsToDraw, request.PlayerTimeSeconds);
            actions.ScheduleDetectionAutoHide();
            return new CodingAiResultWorkflowResult(
                CodingAiResultWorkflowOutcome.FindingsShown,
                validFindings.Count,
                findingsToDraw.Count,
                currentMeter);
        }

        actions.ClearVisuals();
        return new CodingAiResultWorkflowResult(
            CodingAiResultWorkflowOutcome.FindingsHidden,
            validFindings.Count,
            findingsToDraw.Count,
            currentMeter);
    }
}
