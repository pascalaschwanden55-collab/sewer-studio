using System.Threading;
using System.Windows.Media;
using AuswertungPro.Next.UI.Ai.Live;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Ai.Coding;

public enum CodingAnalysisCommandWorkflowOutcome
{
    Skipped,
    StoppedAtTerminalBoundary,
    SingleModelCompleted,
    MultiModelCompleted,
    Canceled,
    Failed
}

public sealed record CodingAnalysisCommandWorkflowRequest(
    string ActivityText,
    bool DisableAnalyzeButton,
    string? ModelName);

public sealed record CodingAnalysisCommandWorkflowActions(
    Func<bool> TryBeginAnalysis,
    Func<CancellationToken> GetAnalysisCancellationToken,
    Func<CodingAnalysisPreflightWorkflowResult> RunPreflight,
    Func<double, CancellationToken, Task> RunSingleModelAnalysisAsync,
    Func<double, Task> RunMultiModelAnalysisAsync,
    Action<string, Color, string?> SetCodingAiState,
    Action EndAnalysis,
    Action<bool> SetAnalyzeButtonEnabled);

public sealed record CodingAnalysisCommandWorkflowResult(
    CodingAnalysisCommandWorkflowOutcome Outcome);

public static class CodingAnalysisCommandWorkflow
{
    public static async Task<CodingAnalysisCommandWorkflowResult> ExecuteAsync(
        CodingAnalysisCommandWorkflowRequest request,
        CodingAnalysisCommandWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (!actions.TryBeginAnalysis())
            return Result(CodingAnalysisCommandWorkflowOutcome.Skipped);

        try
        {
            var cancellationToken = actions.GetAnalysisCancellationToken();
            var preflight = actions.RunPreflight();

            if (preflight.Outcome == CodingAnalysisPreflightWorkflowOutcome.StopAtTerminalBoundary)
                return Result(CodingAnalysisCommandWorkflowOutcome.StoppedAtTerminalBoundary);

            if (preflight.Outcome == CodingAnalysisPreflightWorkflowOutcome.RunMultiModel)
            {
                await actions.RunMultiModelAnalysisAsync(preflight.CaptureTimestampSeconds);
                return Result(CodingAnalysisCommandWorkflowOutcome.MultiModelCompleted);
            }

            await actions.RunSingleModelAnalysisAsync(preflight.CaptureTimestampSeconds, cancellationToken);
            return Result(CodingAnalysisCommandWorkflowOutcome.SingleModelCompleted);
        }
        catch (OperationCanceledException)
        {
            return Result(CodingAnalysisCommandWorkflowOutcome.Canceled);
        }
        catch (Exception ex)
        {
            actions.SetCodingAiState(
                $"Fehler: {ex.Message}",
                PlayerStatusColors.Error,
                $"Modell: {LiveDetectionDisplayPolicy.CompactModelName(request.ModelName)}");
            return Result(CodingAnalysisCommandWorkflowOutcome.Failed);
        }
        finally
        {
            actions.EndAnalysis();
            if (request.DisableAnalyzeButton)
                actions.SetAnalyzeButtonEnabled(true);
        }
    }

    private static CodingAnalysisCommandWorkflowResult Result(CodingAnalysisCommandWorkflowOutcome outcome)
        => new(outcome);
}
