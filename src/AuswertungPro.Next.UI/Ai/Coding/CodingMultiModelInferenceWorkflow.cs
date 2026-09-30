using System.Threading;
using System.Windows.Media;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.UseCases.CodingEinzelbild;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Ai.Coding;

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
    Func<SingleFrameResult, double, double?, Task<bool>> TryHandleBoundaryClassifierResultAsync,
    Func<SingleFrameResult, double, double?, bool> TryHandleStructuralClassifierResult,
    Action<SingleFrameResult> HandleAnalysisResult);

public sealed record CodingMultiModelInferenceWorkflowResult(
    CodingMultiModelInferenceWorkflowOutcome Outcome);

public sealed record CodingMultiModelAnalyzedFrameInferenceActions(
    Func<double?, double?, CodingMeterResolution> ResolveCurrentMeter,
    Func<byte[], CodingMultiModelClassifierInput, CancellationToken, Task<SingleFrameResult>> AnalyzeFrameAsync,
    Action<string, Color, string?, bool> SetCodingAiState,
    Func<SingleFrameResult, CodingAnalyzedFrameEvidence, Task<bool>> TryHandleBoundaryClassifierResultAsync,
    Func<SingleFrameResult, CodingAnalyzedFrameEvidence, bool> TryHandleStructuralClassifierResult,
    Action<SingleFrameResult, CodingAnalyzedFrameEvidence> HandleAnalysisResult);

public static class CodingMultiModelInferenceWorkflow
{
    public static Task<CodingMultiModelInferenceWorkflowResult> ExecuteAnalyzedFrameAsync(
        CodingMultiModelInferenceWorkflowRequest request, CodingMultiModelAnalyzedFrameInferenceActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(request.FrameBytes);
        var meter = actions.ResolveCurrentMeter(request.CaptureTimestampSeconds, request.FrameOsdMeter);
        var frame = CodingAnalyzedFrameEvidence.FromResolution(request.FrameBytes,
            TimeSpan.FromSeconds(request.CaptureTimestampSeconds), meter);
        return ExecuteAsync(request, new CodingMultiModelInferenceWorkflowActions(
            (_, _) => frame.Meter, actions.AnalyzeFrameAsync, actions.SetCodingAiState,
            (result, _, _) => actions.TryHandleBoundaryClassifierResultAsync(result, frame),
            (result, _, _) => actions.TryHandleStructuralClassifierResult(result, frame),
            result => actions.HandleAnalysisResult(result, frame)));
    }

    /// <summary>Alter Einstieg ohne Beleg (CodingReplay); Reihenfolge und Fehlerregel liegen im Anwendungsfall.</summary>
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

        var result = await CodingEinzelbildAnalyseUseCase.AuswertenAsync(
            request.FrameBytes,
            classifierInput,
            request.CancellationToken,
            new CodingEinzelbildAuswertung<SingleFrameResult>(
                actions.AnalyzeFrameAsync,
                FehlerLesen: analysis => analysis.Error,
                Melden: meldung => actions.SetCodingAiState(
                    $"Fehler: {meldung.Fehler}",
                    PlayerStatusColors.Error,
                    "Multi-Model",
                    false),
                GrenzeBehandelnAsync: analysis => actions.TryHandleBoundaryClassifierResultAsync(
                    analysis,
                    request.CaptureTimestampSeconds,
                    request.FrameOsdMeter),
                StrukturBehandeln: analysis => actions.TryHandleStructuralClassifierResult(
                    analysis,
                    request.CaptureTimestampSeconds,
                    request.FrameOsdMeter),
                ErgebnisBehandeln: analysis => actions.HandleAnalysisResult(analysis)));

        return new CodingMultiModelInferenceWorkflowResult(result.Ausgang switch
        {
            CodingEinzelbildAusgang.Modellfehler => CodingMultiModelInferenceWorkflowOutcome.Error,
            CodingEinzelbildAusgang.GrenzeBehandelt => CodingMultiModelInferenceWorkflowOutcome.BoundaryHandled,
            CodingEinzelbildAusgang.StrukturBehandelt => CodingMultiModelInferenceWorkflowOutcome.StructuralHandled,
            _ => CodingMultiModelInferenceWorkflowOutcome.ResultHandled
        });
    }
}
