using System;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private async Task InitCodingAi()
    {
        try
        {
            var platformConfig = PlayerAiSettingsLoader.LoadPlatformSettings();
            var runtime = CodingAiRuntimeFactory.Create(platformConfig, CodeCatalog, _dependencies.PipelineConfig);
            _codingAiRuntimeOwner.Controller.ApplyRuntime(runtime);
            var config = runtime.RuntimeSettings;
            if (!config.Enabled)
            {
                SetCodingAiState("Künstliche Intelligenz deaktiviert", PlayerStatusColors.Muted, "Modell: aus");
                CodingAnalyzeButtonControls.SetEnabled(BtnCodingAnalyze, false);
                return;
            }

            if (runtime.MultiModelAvailable && runtime.VisionClient is not null)
            {
                var healthMonitor = CodingAiRuntimeFactory.CreateHealthMonitor(
                    _codingAiRuntimeOwner.Controller.VisionClient!,
                    aiEnabled: () => _codingAiRuntimeOwner.Controller.AiEnabled,
                    qwenAvailable: () => _codingAiRuntimeOwner.Controller.QwenAvailable);
                _codingAiRuntimeOwner.Controller.StartHealthMonitor(healthMonitor, OnPipelineHealthChanged);

                var initial = await _codingAiRuntimeOwner.Controller.RefreshHealthOnceAsync();
                ApplyPipelineHealth(initial);
            }
            else if (!string.IsNullOrWhiteSpace(runtime.MultiModelError))
            {
                _codingAiRuntimeOwner.Controller.SetUseMultiModel(false);
                SetCodingAiState("Künstliche Intelligenz bereit (Qwen)", PlayerStatusColors.Success,
                    $"Monitor-Fehler: {runtime.MultiModelError}");
            }
            SetYoloStatus("Bereit", PlayerStatusColors.Success, LiveDetectionDisplayPolicy.CompactModelName(_codingAiRuntimeOwner.Controller.ModelName));
        }
        catch (Exception ex)
        {
            SetCodingAiState($"Fehler: {ex.Message}", PlayerStatusColors.Error,
                $"Modell: {LiveDetectionDisplayPolicy.CompactModelName(_codingAiRuntimeOwner.Controller.ModelName)}");
            CodingAnalyzeButtonControls.SetEnabled(BtnCodingAnalyze, false);
        }
    }

}
