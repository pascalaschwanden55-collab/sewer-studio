using System.Threading;
using System.Windows.Media;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Ai;

public enum CodingMultiModelInferenceWorkflowOutcome
{
    Error,
    BoundaryHandled,
    StructuralHandled,
    ResultHandled
}

public sealed record CodingMultiModelInferenceWorkflowRequest(
    string ActivityText,
    byte[] FrameBytes,
    double CaptureTimestampSeconds,
    double? FrameOsdMeter,
    int? NominalDiameterMm,
    double? EndMeter,
    CancellationToken CancellationToken);

public sealed record CodingMultiModelInferenceWorkflowActions(
    Func<double?, double?, double> ResolveCurrentMeter,
    Func<byte[], CodingMultiModelClassifierInput, CancellationToken, Task<SingleFrameResult>> AnalyzeFrameAsync,
    Action<string, Color, string?, bool> SetCodingAiState,
    Func<SingleFrameResult, double, double?, bool> TryHandleBoundaryClassifierResult,
    Func<SingleFrameResult, double, double?, bool> TryHandleStructuralClassifierResult,
    Action<SingleFrameResult> HandleAnalysisResult);

public sealed record CodingMultiModelInferenceWorkflowResult(
    CodingMultiModelInferenceWorkflowOutcome Outcome);

public static class CodingMultiModelInferenceWorkflow
{
    public static async Task<CodingMultiModelInferenceWorkflowResult> ExecuteAsync(
        CodingMultiModelInferenceWorkflowRequest request,
        CodingMultiModelInferenceWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.FrameBytes);
        ArgumentNullException.ThrowIfNull(actions);

        var currentMeterForClassifier = actions.ResolveCurrentMeter(
            request.CaptureTimestampSeconds,
            request.FrameOsdMeter);
        var classifierInput = CodingMultiModelClassifierInputPolicy.Build(
            request.NominalDiameterMm,
            currentMeterForClassifier,
            request.EndMeter);

        var result = await actions.AnalyzeFrameAsync(
            request.FrameBytes,
            classifierInput,
            request.CancellationToken);

        if (result.Error != null)
        {
            actions.SetCodingAiState(
                $"Fehler: {result.Error}",
                PlayerStatusColors.Error,
                "Multi-Model",
                false);
            return new CodingMultiModelInferenceWorkflowResult(
                CodingMultiModelInferenceWorkflowOutcome.Error);
        }

        if (actions.TryHandleBoundaryClassifierResult(
                result,
                request.CaptureTimestampSeconds,
                request.FrameOsdMeter))
        {
            return new CodingMultiModelInferenceWorkflowResult(
                CodingMultiModelInferenceWorkflowOutcome.BoundaryHandled);
        }

        if (actions.TryHandleStructuralClassifierResult(
                result,
                request.CaptureTimestampSeconds,
                request.FrameOsdMeter))
        {
            return new CodingMultiModelInferenceWorkflowResult(
                CodingMultiModelInferenceWorkflowOutcome.StructuralHandled);
        }

        actions.HandleAnalysisResult(result);
        return new CodingMultiModelInferenceWorkflowResult(
            CodingMultiModelInferenceWorkflowOutcome.ResultHandled);
    }
}
