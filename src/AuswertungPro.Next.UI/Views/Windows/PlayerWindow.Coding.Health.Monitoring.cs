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
                if (!_closing && _isCodingMode && _codingAiRuntimeOwner.Controller.HasHealthMonitor)
                    ApplyPipelineHealth(status);
            }));
            return;
        }

        if (_isCodingMode && _codingAiRuntimeOwner.Controller.HasHealthMonitor)
            ApplyPipelineHealth(status);
    }

    private void ApplyPipelineHealth(PipelineHealthStatus status)
    {
        _codingAiRuntimeOwner.Controller.SetUseMultiModel(status.MultiModelActive);
        if (status.MultiModelActive)
            _codingAiRuntimeOwner.Controller.EnsureMultiModel(CodingAiRuntimeFactory.CreateMultiModelService);

        var uiState = PipelineHealthUiStateFactory.Create(status);
        SetCodingAiState(uiState.Summary, uiState.Color, uiState.Detail);
        CodingAnalyzeButtonControls.SetEnabled(BtnCodingAnalyze, uiState.AnalysisEnabled);
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
        _codingAiRuntimeOwner.Controller
            .StopHealthMonitor()
            ?.SafeFireAndForget("PipelineHealthMonitorStop");
    }
}
