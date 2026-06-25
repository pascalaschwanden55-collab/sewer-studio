using System;
using System.Windows.Media;
using AuswertungPro.Next.Application.Ai;

namespace AuswertungPro.Next.UI.Ai;

public sealed record CodingPipelineHealthApplyWorkflowRequest(
    PipelineHealthStatus Status);

public sealed record CodingPipelineHealthApplyWorkflowActions(
    Action<bool> SetUseMultiModel,
    Action EnsureMultiModel,
    Action<string, Color, string?> SetCodingAiState,
    Action<bool> SetAnalyzeButtonEnabled,
    Action<PipelineHealthDetailsUiState> UpdatePipelineHealthDetails);

public static class CodingPipelineHealthApplyWorkflow
{
    public static void Execute(
        CodingPipelineHealthApplyWorkflowRequest request,
        CodingPipelineHealthApplyWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        var status = request.Status;
        actions.SetUseMultiModel(status.MultiModelActive);
        if (status.MultiModelActive)
            actions.EnsureMultiModel();

        var uiState = PipelineHealthUiStateFactory.Create(status);
        actions.SetCodingAiState(uiState.Summary, uiState.Color, uiState.Detail);
        actions.SetAnalyzeButtonEnabled(uiState.AnalysisEnabled);
        actions.UpdatePipelineHealthDetails(uiState.Details);
    }
}
