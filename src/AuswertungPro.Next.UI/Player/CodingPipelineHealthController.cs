using System;
using System.Threading.Tasks;
using System.Windows.Media;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Helpers;

namespace AuswertungPro.Next.UI.Player;

public interface ICodingPipelineHealthController
{
    Task InitializeAsync();

    void Stop();
}

public sealed record CodingPipelineHealthControllerActions(
    Func<CodingAiRuntime> CreateRuntime,
    Func<CodingAiRuntime, IPipelineHealthMonitor> CreateHealthMonitor,
    Func<bool> IsClosing,
    Func<bool> DispatcherHasShutdownStarted,
    Func<bool> HasDispatcherAccess,
    Func<bool> IsCodingMode,
    Action<Action> DispatchToUi,
    Action<string, Color, string?> SetCodingAiState,
    Action<bool> SetAnalyzeButtonEnabled,
    Action<string, Color, string?> SetYoloStatus,
    Action<PipelineHealthDetailsUiState> UpdatePipelineHealthDetails);

public sealed class CodingPipelineHealthController : ICodingPipelineHealthController
{
    private readonly CodingAiController _runtimeController;
    private readonly CodingPipelineHealthControllerActions _actions;

    public CodingPipelineHealthController(
        CodingAiController runtimeController,
        CodingPipelineHealthControllerActions actions)
    {
        ArgumentNullException.ThrowIfNull(runtimeController);
        ArgumentNullException.ThrowIfNull(actions);

        _runtimeController = runtimeController;
        _actions = actions;
    }

    public Task InitializeAsync()
        => CodingAiInitializationWorkflow.ExecuteAsync(
            new CodingAiInitializationWorkflowActions(
                CreateRuntime: _actions.CreateRuntime,
                ApplyRuntime: _runtimeController.ApplyRuntime,
                CreateHealthMonitor: _actions.CreateHealthMonitor,
                StartHealthMonitor: monitor => _runtimeController.StartHealthMonitor(
                    monitor,
                    (_, status) => HandleStatusChanged(status)),
                RefreshHealthOnceAsync: () => _runtimeController.RefreshHealthOnceAsync(),
                ApplyPipelineHealth: ApplyPipelineHealth,
                SetCodingAiState: _actions.SetCodingAiState,
                SetAnalyzeButtonEnabled: _actions.SetAnalyzeButtonEnabled,
                SetUseMultiModel: _runtimeController.SetUseMultiModel,
                GetModelName: () => _runtimeController.ModelName,
                SetYoloStatus: _actions.SetYoloStatus));

    internal void HandleStatusChanged(PipelineHealthStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);

        CodingPipelineHealthChangeWorkflow.Execute(
            new CodingPipelineHealthChangeWorkflowRequest(
                _actions.IsClosing(),
                _actions.DispatcherHasShutdownStarted(),
                _actions.HasDispatcherAccess()),
            new CodingPipelineHealthChangeWorkflowActions(
                ShouldApply: () =>
                    !_actions.IsClosing() &&
                    _actions.IsCodingMode() &&
                    _runtimeController.HasHealthMonitor,
                DispatchToUi: _actions.DispatchToUi,
                ApplyPipelineHealth: () => ApplyPipelineHealth(status)));
    }

    public void Stop()
    {
        _runtimeController
            .StopHealthMonitor()
            ?.SafeFireAndForget("PipelineHealthMonitorStop");
    }

    private void ApplyPipelineHealth(PipelineHealthStatus status)
    {
        CodingPipelineHealthApplyWorkflow.Execute(
            new CodingPipelineHealthApplyWorkflowRequest(status),
            new CodingPipelineHealthApplyWorkflowActions(
                SetUseMultiModel: _runtimeController.SetUseMultiModel,
                EnsureMultiModel: () => CodingAiMultiModelEnsureWorkflow.Ensure(_runtimeController),
                SetCodingAiState: _actions.SetCodingAiState,
                SetAnalyzeButtonEnabled: _actions.SetAnalyzeButtonEnabled,
                UpdatePipelineHealthDetails: _actions.UpdatePipelineHealthDetails));
    }
}
