using System;
using System.Threading.Tasks;
using System.Windows.Media;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Ai;

public enum CodingAiInitializationWorkflowOutcome
{
    Disabled,
    MultiModelReady,
    QwenFallback,
    Ready,
    Failed
}

public sealed record CodingAiInitializationWorkflowActions(
    Func<CodingAiRuntime> CreateRuntime,
    Action<CodingAiRuntime> ApplyRuntime,
    Func<CodingAiRuntime, IPipelineHealthMonitor> CreateHealthMonitor,
    Action<IPipelineHealthMonitor> StartHealthMonitor,
    Func<Task<PipelineHealthStatus>> RefreshHealthOnceAsync,
    Action<PipelineHealthStatus> ApplyPipelineHealth,
    Action<string, Color, string?> SetCodingAiState,
    Action<bool> SetAnalyzeButtonEnabled,
    Action<bool> SetUseMultiModel,
    Func<string> GetModelName,
    Action<string, Color, string?> SetYoloStatus);

public sealed record CodingAiInitializationWorkflowResult(
    CodingAiInitializationWorkflowOutcome Outcome);

public static class CodingAiInitializationWorkflow
{
    public static async Task<CodingAiInitializationWorkflowResult> ExecuteAsync(
        CodingAiInitializationWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(actions);

        try
        {
            var runtime = actions.CreateRuntime();
            actions.ApplyRuntime(runtime);

            if (!runtime.RuntimeSettings.Enabled)
            {
                actions.SetCodingAiState(
                    "Künstliche Intelligenz deaktiviert",
                    PlayerStatusColors.Muted,
                    "Modell: aus");
                actions.SetAnalyzeButtonEnabled(false);
                return Result(CodingAiInitializationWorkflowOutcome.Disabled);
            }

            var outcome = CodingAiInitializationWorkflowOutcome.Ready;
            if (runtime.MultiModelAvailable && runtime.VisionClient is not null)
            {
                var healthMonitor = actions.CreateHealthMonitor(runtime);
                actions.StartHealthMonitor(healthMonitor);

                var initial = await actions.RefreshHealthOnceAsync();
                actions.ApplyPipelineHealth(initial);
                outcome = CodingAiInitializationWorkflowOutcome.MultiModelReady;
            }
            else if (!string.IsNullOrWhiteSpace(runtime.MultiModelError))
            {
                actions.SetUseMultiModel(false);
                actions.SetCodingAiState(
                    "Künstliche Intelligenz bereit (Qwen)",
                    PlayerStatusColors.Success,
                    $"Monitor-Fehler: {runtime.MultiModelError}");
                outcome = CodingAiInitializationWorkflowOutcome.QwenFallback;
            }

            actions.SetYoloStatus(
                "Bereit",
                PlayerStatusColors.Success,
                LiveDetectionDisplayPolicy.CompactModelName(actions.GetModelName()));

            return Result(outcome);
        }
        catch (Exception ex)
        {
            actions.SetCodingAiState(
                $"Fehler: {ex.Message}",
                PlayerStatusColors.Error,
                $"Modell: {LiveDetectionDisplayPolicy.CompactModelName(actions.GetModelName())}");
            actions.SetAnalyzeButtonEnabled(false);
            return Result(CodingAiInitializationWorkflowOutcome.Failed);
        }
    }

    private static CodingAiInitializationWorkflowResult Result(CodingAiInitializationWorkflowOutcome outcome)
        => new(outcome);
}
