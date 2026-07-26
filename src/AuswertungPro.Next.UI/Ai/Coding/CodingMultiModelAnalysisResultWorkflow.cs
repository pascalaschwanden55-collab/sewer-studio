using System.Windows.Media;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Ai.Coding;

public enum CodingMultiModelAnalysisResultWorkflowOutcome
{
    Error,
    ReviewRequired,
    NoDamage,
    NoSegmentedFindings,
    AheadOnly,
    EventsAdded
}

public sealed record CodingMultiModelAnalysisResultWorkflowRequest(
    SingleFrameResult Result,
    string ActivityText);

public sealed record CodingMultiModelAnalysisResultWorkflowActions(
    Action<string, Color, string?, bool> SetAiState,
    Action ClearMasks,
    Func<SingleFrameResult, IReadOnlyList<SegmentedFinding>> BuildSegmentedFindings,
    Action<SingleFrameResult, IReadOnlyList<SegmentedFinding>> ShowMultiModelResults,
    Action<IReadOnlyList<SegmentedFinding>, double, double, double?> AddFindingsAsEvents);

public sealed record CodingMultiModelAnalysisResultWorkflowResult(
    CodingMultiModelAnalysisResultWorkflowOutcome Outcome,
    int VisibleFindingCount);

public static class CodingMultiModelAnalysisResultWorkflow
{
    public static CodingMultiModelAnalysisResultWorkflowResult Execute(
        CodingMultiModelAnalysisResultWorkflowRequest request,
        CodingMultiModelAnalysisResultWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Result);
        ArgumentNullException.ThrowIfNull(actions);

        var result = request.Result;
        if (result.Error != null)
        {
            actions.SetAiState($"Fehler: {result.Error}", PlayerStatusColors.Error, "Multi-Model", false);
            return new CodingMultiModelAnalysisResultWorkflowResult(
                CodingMultiModelAnalysisResultWorkflowOutcome.Error,
                VisibleFindingCount: 0);
        }

        if ((!result.IsRelevant || !result.HasDetections) && result.Degraded)
        {
            actions.SetAiState(
                "KI-Ergebnis unvollstaendig – manuell pruefen",
                PlayerStatusColors.Warning,
                result.DegradedReason ?? "Ein KI-Modell ist nicht qualifiziert oder ausgefallen.",
                false);
            actions.ClearMasks();
            return new CodingMultiModelAnalysisResultWorkflowResult(
                CodingMultiModelAnalysisResultWorkflowOutcome.ReviewRequired,
                VisibleFindingCount: 0);
        }

        if (!result.IsRelevant || !result.HasDetections)
        {
            actions.SetAiState(
                "Kein Schaden erkannt",
                PlayerStatusColors.Success,
                $"YOLO {result.YoloTimeMs:F0}ms | {result.DinoDetections.Count} Detektionen",
                false);
            actions.ClearMasks();
            return new CodingMultiModelAnalysisResultWorkflowResult(
                CodingMultiModelAnalysisResultWorkflowOutcome.NoDamage,
                VisibleFindingCount: 0);
        }

        actions.SetAiState(
            request.ActivityText,
            PlayerStatusColors.Warning,
            $"Schritt 3 von 4: SAM-Masken ({result.DinoDetections.Count} Befunde)",
            true);

        var segmented = actions.BuildSegmentedFindings(result);
        var findingSummary = CodingMultiModelFindingSummary.Build(segmented, result);

        actions.ShowMultiModelResults(result, segmented);

        if (findingSummary.HasNoSegmentedFindings)
        {
            actions.SetAiState(
                "SAM ohne Maske - Befund nicht segmentiert",
                PlayerStatusColors.Warning,
                result.SamResponse?.Degraded == true
                    ? $"SAM degraded ({result.SamResponse.SkippedBoxes} Box(en) verloren)"
                    : "keine Maske erzeugt",
                false);
            return new CodingMultiModelAnalysisResultWorkflowResult(
                CodingMultiModelAnalysisResultWorkflowOutcome.NoSegmentedFindings,
                VisibleFindingCount: 0);
        }

        if (findingSummary.HasOnlyAheadFindings)
        {
            actions.SetAiState(
                "Ereignis voraus erkannt - naeher heranfahren",
                PlayerStatusColors.Warning,
                $"{findingSummary.VorausCount} voraus",
                false);
            return new CodingMultiModelAnalysisResultWorkflowResult(
                CodingMultiModelAnalysisResultWorkflowOutcome.AheadOnly,
                VisibleFindingCount: 0);
        }

        actions.SetAiState(
            result.Degraded
                ? findingSummary.DetectedStatusText + " – manuell pruefen"
                : findingSummary.DetectedStatusText,
            result.Degraded ? PlayerStatusColors.Warning : PlayerStatusColors.Success,
            result.Degraded
                ? findingSummary.TimingText + " | " + result.DegradedReason
                : findingSummary.TimingText,
            false);

        actions.AddFindingsAsEvents(
            findingSummary.VisibleCodierbar,
            result.SamResponse?.ImageWidth ?? 1,
            result.SamResponse?.ImageHeight ?? 1,
            result.YoloMaxConfidence);

        return new CodingMultiModelAnalysisResultWorkflowResult(
            CodingMultiModelAnalysisResultWorkflowOutcome.EventsAdded,
            findingSummary.VisibleCodierbar.Count);
    }
}
