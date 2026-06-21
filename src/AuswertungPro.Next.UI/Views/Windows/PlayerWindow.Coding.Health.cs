using System;
using System.Windows.Media;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Ollama;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.Infrastructure.Ai.QualityGate;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Services;

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
                SetCodingAiState("Künstliche Intelligenz deaktiviert", Color.FromRgb(0x94, 0xA3, 0xB8), "Modell: aus");
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
                SetCodingAiState("Künstliche Intelligenz bereit (Qwen)", Color.FromRgb(0x22, 0xC5, 0x5E),
                    $"Monitor-Fehler: {ex.Message}");
            }
            SetYoloStatus("Bereit", Color.FromRgb(0x22, 0xC5, 0x5E), LiveDetectionDisplayPolicy.CompactModelName(_codingAiModelName));
        }
        catch (Exception ex)
        {
            SetCodingAiState($"Fehler: {ex.Message}", Color.FromRgb(0xEF, 0x44, 0x44),
                $"Modell: {LiveDetectionDisplayPolicy.CompactModelName(_codingAiModelName)}");
            BtnCodingAnalyze.IsEnabled = false;
        }
    }

    private void OnPipelineHealthChanged(object? sender, PipelineHealthStatus status)
    {
        if (_closing || Dispatcher.HasShutdownStarted)
            return;

        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!_closing && _isCodingMode && _codingHealthMonitor != null)
                    ApplyPipelineHealth(status);
            }));
            return;
        }

        if (_isCodingMode && _codingHealthMonitor != null)
            ApplyPipelineHealth(status);
    }

    private void ApplyPipelineHealth(PipelineHealthStatus status)
    {
        _codingUseMultiModel = status.MultiModelActive;
        if (status.MultiModelActive && _codingMultiModel == null && _codingVisionClient != null)
            _codingMultiModel = _codingPipelineConfig is null
                ? new SingleFrameMultiModelService(_codingVisionClient)
                : new SingleFrameMultiModelService(_codingVisionClient, _codingPipelineConfig);

        var uiState = PipelineHealthUiStateFactory.Create(status);
        SetCodingAiState(uiState.Summary, uiState.Color, uiState.Detail);
        BtnCodingAnalyze.IsEnabled = uiState.AnalysisEnabled;
        UpdatePipelineHealthDetails(uiState.Details);
    }

    private void UpdatePipelineHealthDetails(PipelineHealthDetailsUiState details)
    {
        Hd_Sidecar.Text = details.Sidecar;
        Hd_Token.Text = details.Token;
        Hd_Yolo.Text = details.Yolo;
        Hd_Dino.Text = details.Dino;
        Hd_Sam.Text = details.Sam;
        Hd_Mode.Text = details.Mode;
    }

    private void StopPipelineHealthMonitor()
    {
        _codingAiEnabled = false;
        if (_codingHealthMonitor != null)
        {
            _codingHealthMonitor.StatusChanged -= OnPipelineHealthChanged;
            _ = _codingHealthMonitor.StopAsync();
            _codingHealthMonitor = null;
        }
    }
}
