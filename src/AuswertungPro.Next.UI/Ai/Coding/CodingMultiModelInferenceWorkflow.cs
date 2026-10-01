using System.Threading;
using System.Windows.Media;
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

/// <summary>
/// Einstieg des CodingReplay-Messhosts ohne Aufnahmebeleg. Der Player nutzt
/// <see cref="CodingEinzelbildAnalyseUseCase.ExecuteAsync"/>; Verteilung und Fehlerregel liegen dort.
/// </summary>
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

        var result = await CodingEinzelbildAnalyseUseCase.AuswertenAsync(
            request.FrameBytes,
            classifierInput,
            request.CancellationToken,
            new CodingEinzelbildAuswertung<SingleFrameResult>(
                actions.AnalyzeFrameAsync,
                FehlerLesen: analysis => analysis.Error,
                Melden: meldung => CodingEinzelbildStatusAnzeige.Zeigen(
                    meldung,
                    request.ActivityText,
                    actions.SetCodingAiState),
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
