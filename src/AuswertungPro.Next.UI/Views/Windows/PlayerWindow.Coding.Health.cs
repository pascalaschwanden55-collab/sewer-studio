using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private async Task InitCodingAi()
    {
        await CodingAiInitializationWorkflow.ExecuteAsync(
            new CodingAiInitializationWorkflowActions(
                CreateRuntime: () =>
                {
                    var platformConfig = PlayerAiSettingsLoader.LoadPlatformSettings();
                    return CodingAiRuntimeFactory.Create(platformConfig, CodeCatalog, _dependencies.PipelineConfig);
                },
                ApplyRuntime: _codingAiRuntimeOwner.Controller.ApplyRuntime,
                CreateHealthMonitor: runtime => CodingAiRuntimeFactory.CreateHealthMonitor(
                    runtime.VisionClient!,
                    aiEnabled: () => _codingAiRuntimeOwner.Controller.AiEnabled,
                    qwenAvailable: () => _codingAiRuntimeOwner.Controller.QwenAvailable),
                StartHealthMonitor: monitor => _codingAiRuntimeOwner.Controller.StartHealthMonitor(
                    monitor,
                    OnPipelineHealthChanged),
                RefreshHealthOnceAsync: () => _codingAiRuntimeOwner.Controller.RefreshHealthOnceAsync(),
                ApplyPipelineHealth: ApplyPipelineHealth,
                SetCodingAiState: (status, color, detail) => SetCodingAiState(status, color, detail),
                SetAnalyzeButtonEnabled: enabled => CodingAnalyzeButtonControls.SetEnabled(BtnCodingAnalyze, enabled),
                SetUseMultiModel: _codingAiRuntimeOwner.Controller.SetUseMultiModel,
                GetModelName: () => _codingAiRuntimeOwner.Controller.ModelName,
                SetYoloStatus: (status, color, model) => SetYoloStatus(status, color, model)));
    }
}
