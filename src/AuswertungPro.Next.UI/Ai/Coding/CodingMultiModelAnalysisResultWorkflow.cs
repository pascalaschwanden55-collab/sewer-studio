using System.Windows.Media;
using AuswertungPro.Next.Application.UseCases.CodingClassifierHint;
using AuswertungPro.Next.Infrastructure.Ai;
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

        var classifierHint = CodingClassifierImageHint.Create(
            result.ClassifierCode,
            result.ClassifierConfidence,
            VsaCodeResolver.LookupLabel(result.ClassifierCode ?? ""));

        // Erst am abschliessenden Ergebnis anzeigen: Ein Hinweis nach dem
        // Struktur-Workflow wuerde durch die folgende DINO/SAM-Anzeige verschwinden.
        void SetAiState(string status, Color color, string? detail, bool pulse)
            => actions.SetAiState(
                !pulse && classifierHint != null ? classifierHint.BuildStatus(status) : status,
                !pulse && classifierHint != null ? PlayerStatusColors.Warning : color,
                !pulse && classifierHint != null ? classifierHint.AppendToDetail(detail) : detail,
                pulse);

        if ((!result.IsRelevant || !result.HasDetections) && (result.Degraded || classifierHint != null))
        {
            SetAiState(
                result.Degraded ? "KI-Ergebnis unvollständig – manuell prüfen" : "Befund nicht lokalisiert",
                PlayerStatusColors.Warning,
                result.Degraded
                    ? result.DegradedReason ?? "Ein KI-Modell ist nicht qualifiziert oder ausgefallen."
                    : null,
                false);
            actions.ClearMasks();
            return new CodingMultiModelAnalysisResultWorkflowResult(
                CodingMultiModelAnalysisResultWorkflowOutcome.ReviewRequired,
                VisibleFindingCount: 0);
        }

        if (!result.IsRelevant || !result.HasDetections)
        {
            SetAiState(
                "Kein Schaden erkannt",
                PlayerStatusColors.Success,
                $"YOLO {result.YoloTimeMs:F0}ms | {result.DinoDetections.Count} Detektionen",
                false);
            actions.ClearMasks();
            return new CodingMultiModelAnalysisResultWorkflowResult(
                CodingMultiModelAnalysisResultWorkflowOutcome.NoDamage,
                VisibleFindingCount: 0);
        }

        SetAiState(
            request.ActivityText,
            PlayerStatusColors.Warning,
            $"Schritt 3 von 4: SAM-Masken ({result.DinoDetections.Count} Befunde)",
            true);

        var segmented = actions.BuildSegmentedFindings(result);
        var findingSummary = CodingMultiModelFindingSummary.Build(segmented, result);

        actions.ShowMultiModelResults(result, segmented);

        if (findingSummary.HasNoSegmentedFindings)
        {
            SetAiState(
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
            SetAiState(
                "Ereignis voraus erkannt - näher heranfahren",
                PlayerStatusColors.Warning,
                $"{findingSummary.VorausCount} voraus",
                false);
            return new CodingMultiModelAnalysisResultWorkflowResult(
                CodingMultiModelAnalysisResultWorkflowOutcome.AheadOnly,
                VisibleFindingCount: 0);
        }

        SetAiState(
            result.Degraded
                ? findingSummary.DetectedStatusText + " – manuell prüfen"
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
