using System;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.UI.Ai;

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
