using System;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Helpers;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
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
            _codingMultiModel = CodingAiRuntimeFactory.CreateMultiModelService(_codingVisionClient, _codingPipelineConfig);

        var uiState = PipelineHealthUiStateFactory.Create(status);
        SetCodingAiState(uiState.Summary, uiState.Color, uiState.Detail);
        BtnCodingAnalyze.IsEnabled = uiState.AnalysisEnabled;
        UpdatePipelineHealthDetails(uiState.Details);
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
        _codingAiEnabled = false;
        if (_codingHealthMonitor != null)
        {
            _codingHealthMonitor.StatusChanged -= OnPipelineHealthChanged;
            _codingHealthMonitor.StopAsync().SafeFireAndForget("PipelineHealthMonitorStop");
            _codingHealthMonitor = null;
        }
    }
}
