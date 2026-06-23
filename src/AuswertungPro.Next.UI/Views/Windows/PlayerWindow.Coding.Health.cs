using System;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private async Task InitCodingAi()
    {
        try
        {
            var platformConfig = new AppSettingsAiSettingsProvider().Load();
            var runtime = CodingAiRuntimeFactory.Create(platformConfig, CodeCatalog, _serviceProvider?.PipelineCfg);
            var config = runtime.RuntimeSettings;
            _codingPipelineConfig = runtime.PipelineConfig;
            _codingAiModelName = runtime.ModelName;
            if (!config.Enabled)
            {
                SetCodingAiState("Künstliche Intelligenz deaktiviert", PlayerStatusColors.Muted, "Modell: aus");
                BtnCodingAnalyze.IsEnabled = false;
                return;
            }

            _codingLiveDetection = runtime.LiveDetection;
            _codingEnhancedVision = runtime.EnhancedVision;
            _codingQualityGate = runtime.QualityGate;

            if (runtime.MultiModelAvailable && runtime.VisionClient is not null)
            {
                _codingVisionClient = runtime.VisionClient;
                _codingMultiModel = runtime.MultiModel;
                _codingBoxSegmentation = runtime.BoxSegmentation;
                _codingAiEnabled = true;

                _codingHealthMonitor = CodingAiRuntimeFactory.CreateHealthMonitor(
                    _codingVisionClient,
                    aiEnabled: () => _codingAiEnabled,
                    qwenAvailable: () => _codingLiveDetection != null || _codingEnhancedVision != null);
                _codingHealthMonitor.StatusChanged += OnPipelineHealthChanged;
                _codingHealthMonitor.Start();

                var initial = await _codingHealthMonitor.RefreshOnceAsync();
                ApplyPipelineHealth(initial);
            }
            else if (!string.IsNullOrWhiteSpace(runtime.MultiModelError))
            {
                _codingUseMultiModel = false;
                SetCodingAiState("Künstliche Intelligenz bereit (Qwen)", PlayerStatusColors.Success,
                    $"Monitor-Fehler: {runtime.MultiModelError}");
            }
            SetYoloStatus("Bereit", PlayerStatusColors.Success, LiveDetectionDisplayPolicy.CompactModelName(_codingAiModelName));
        }
        catch (Exception ex)
        {
            SetCodingAiState($"Fehler: {ex.Message}", PlayerStatusColors.Error,
                $"Modell: {LiveDetectionDisplayPolicy.CompactModelName(_codingAiModelName)}");
            BtnCodingAnalyze.IsEnabled = false;
        }
    }

}
