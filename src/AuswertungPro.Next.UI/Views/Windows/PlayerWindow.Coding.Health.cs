using System;
using System.Windows.Media;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Ollama;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.Infrastructure.Ai.QualityGate;
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
            var config = platformConfig.ToRuntimeSettings();
            _codingPipelineConfig = _serviceProvider is not null
                ? _serviceProvider.PipelineCfg
                : platformConfig.ToPipelineConfig();
            _codingAiModelName = config.VisionModel;
            if (!config.Enabled)
            {
                SetCodingAiState("Künstliche Intelligenz deaktiviert", PlayerStatusColors.Muted, "Modell: aus");
                BtnCodingAnalyze.IsEnabled = false;
                return;
            }

            var client = new OllamaClient(
                config.OllamaBaseUri,
                ownedTimeout: config.OllamaRequestTimeout,
                keepAlive: config.OllamaKeepAlive,
                numCtx: config.OllamaNumCtx);
            _codingLiveDetection = new LiveDetectionService(client, config.VisionModel);
            _codingEnhancedVision = new EnhancedVisionAnalysisService(client, config.VisionModel, CodeCatalog);
            _codingQualityGate = new QualityGateService();

            try
            {
                _codingVisionClient = new VisionPipelineClient(
                    _codingPipelineConfig.SidecarUrl,
                    sidecarToken: _codingPipelineConfig.SidecarToken);
                _codingMultiModel = new SingleFrameMultiModelService(_codingVisionClient, _codingPipelineConfig);
                _codingBoxSegmentation = new MarkBoxSegmentationService(_codingVisionClient.SegmentSamAsync);
                _codingAiEnabled = true;

                _codingHealthMonitor = new PipelineHealthMonitor(
                    _codingVisionClient,
                    aiEnabled: () => _codingAiEnabled,
                    qwenAvailable: () => _codingLiveDetection != null || _codingEnhancedVision != null);
                _codingHealthMonitor.StatusChanged += OnPipelineHealthChanged;
                _codingHealthMonitor.Start();

                var initial = await _codingHealthMonitor.RefreshOnceAsync();
                ApplyPipelineHealth(initial);
            }
            catch (Exception ex)
            {
                _codingUseMultiModel = false;
                SetCodingAiState("Künstliche Intelligenz bereit (Qwen)", PlayerStatusColors.Success,
                    $"Monitor-Fehler: {ex.Message}");
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
