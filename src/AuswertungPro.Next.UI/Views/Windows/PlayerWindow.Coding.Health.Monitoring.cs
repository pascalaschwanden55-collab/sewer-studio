using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Helpers;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void OnPipelineHealthChanged(object? sender, PipelineHealthStatus status)
    {
        CodingPipelineHealthChangeWorkflow.Execute(
            new CodingPipelineHealthChangeWorkflowRequest(
                _shutdownState.IsClosing,
                PlayerDispatcherScheduler.HasShutdownStarted(Dispatcher),
                PlayerDispatcherScheduler.HasAccess(Dispatcher)),
            new CodingPipelineHealthChangeWorkflowActions(
                ShouldApply: () => !_shutdownState.IsClosing && _codingModeState.IsCodingMode && _codingAiRuntimeOwner.Controller.HasHealthMonitor,
                DispatchToUi: action => PlayerDispatcherScheduler.ScheduleNormal(Dispatcher, action),
                ApplyPipelineHealth: () => ApplyPipelineHealth(status)));
    }

    private void ApplyPipelineHealth(PipelineHealthStatus status)
    {
        CodingPipelineHealthApplyWorkflow.Execute(
            new CodingPipelineHealthApplyWorkflowRequest(status),
            new CodingPipelineHealthApplyWorkflowActions(
                SetUseMultiModel: _codingAiRuntimeOwner.Controller.SetUseMultiModel,
                EnsureMultiModel: () => CodingAiMultiModelEnsureWorkflow.Ensure(_codingAiRuntimeOwner.Controller),
                SetCodingAiState: (summary, color, detail) => SetCodingAiState(summary, color, detail),
                SetAnalyzeButtonEnabled: enabled => CodingAnalyzeButtonControls.SetEnabled(BtnCodingAnalyze, enabled),
                UpdatePipelineHealthDetails: UpdatePipelineHealthDetails));
    }

    private void UpdatePipelineHealthDetails(PipelineHealthDetailsUiState details)
    {
        LiveDetectionStatusControls.ShowPipelineHealthDetails(
            Hd_Sidecar,
            Hd_Token,
            Hd_Yolo,
            Hd_Dino,
            Hd_Sam,
            Hd_Mode,
            details);
    }

    private void StopPipelineHealthMonitor()
    {
        _codingAiRuntimeOwner.Controller
            .StopHealthMonitor()
            ?.SafeFireAndForget("PipelineHealthMonitorStop");
    }
}
