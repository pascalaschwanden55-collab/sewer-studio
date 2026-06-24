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
            _codingAiController.ApplyRuntime(runtime);
            var config = runtime.RuntimeSettings;
            if (!config.Enabled)
            {
                SetCodingAiState("Künstliche Intelligenz deaktiviert", PlayerStatusColors.Muted, "Modell: aus");
                CodingAnalyzeButtonControls.SetEnabled(BtnCodingAnalyze, false);
                return;
            }

            if (runtime.MultiModelAvailable && runtime.VisionClient is not null)
            {
                _codingHealthMonitor = CodingAiRuntimeFactory.CreateHealthMonitor(
                    _codingAiController.VisionClient!,
                    aiEnabled: () => _codingAiController.AiEnabled,
                    qwenAvailable: () => _codingAiController.QwenAvailable);
                _codingHealthMonitor.StatusChanged += OnPipelineHealthChanged;
                _codingHealthMonitor.Start();

                var initial = await _codingHealthMonitor.RefreshOnceAsync();
                ApplyPipelineHealth(initial);
            }
            else if (!string.IsNullOrWhiteSpace(runtime.MultiModelError))
            {
                _codingAiController.SetUseMultiModel(false);
                SetCodingAiState("Künstliche Intelligenz bereit (Qwen)", PlayerStatusColors.Success,
                    $"Monitor-Fehler: {runtime.MultiModelError}");
            }
            SetYoloStatus("Bereit", PlayerStatusColors.Success, LiveDetectionDisplayPolicy.CompactModelName(_codingAiController.ModelName));
        }
        catch (Exception ex)
        {
            SetCodingAiState($"Fehler: {ex.Message}", PlayerStatusColors.Error,
                $"Modell: {LiveDetectionDisplayPolicy.CompactModelName(_codingAiController.ModelName)}");
            CodingAnalyzeButtonControls.SetEnabled(BtnCodingAnalyze, false);
        }
    }

}
