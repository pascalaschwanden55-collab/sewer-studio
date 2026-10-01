using System.Threading.Tasks;
using AuswertungPro.Next.Application.UseCases.CodingEinzelbild;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.UI.Ai.Coding;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    // Ablauf und Aufnahmebindung: CodingEinzelbildAnalyseUseCase (docs/architektur/player-einzelbild-ablauf.md).
    private async Task RunCodingMultiModelAnalysisAsync(string activityText, double captureTimestampSec)
    {
        await CodingEinzelbildAnalyseUseCase.ExecuteAsync(
            new CodingEinzelbildAnfrage<SingleFrameMultiModelService>(
                _codingAiRuntimeOwner.Controller.MultiModel,
                _codingAiRuntimeOwner.Controller.AnalysisCancellation,
                captureTimestampSec),
            new CodingEinzelbildSchritte<SingleFrameMultiModelService, SingleFrameResult>(
                Melden: meldung => CodingEinzelbildStatusAnzeige.Zeigen(
                    meldung,
                    activityText,
                    _liveDetectionStatusController.SetCodingAiState),
                BildAufnehmenAsync: _codingAnalysisContext.CaptureSnapshotAsync,
                AnalysebildMerken: (frameBytes, timestamp) => _liveDetectionController.StoreAnalyzedFrame(frameBytes, timestamp),
                OsdMeterLesenAsync: TryReadAnalyzedFrameOsdMeterAsync,
                BildbereitschaftAktualisieren: UpdateFrameReadiness,
                IstBildBereit: IsFrameReady,
                HatCodiersitzung: () => _codingSessionHost.HasViewModel,
                EndmeterLesen: () => _codingSessionHost.EndMeter,
                NennweiteLesen: () => _codingOverlayToolHost.NominalDiameterMm,
                MeterAufloesen: ResolveCodingMeterEvidenceForFrame,
                AnalysierenAsync: (multiModel, frameBytes, classifierInput, cancellationToken) => multiModel.AnalyzeFrameAsync(
                    frameBytes,
                    classifierInput.NominalDiameterMm,
                    _codingOverlayToolHost.Calibration,
                    cancellationToken,
                    classifierInput.CurrentMeter,
                    classifierInput.ReachLength),
                FehlerLesen: result => result.Error,
                GrenzeBehandelnAsync: TryHandleBoundaryClassifierResultAsync,
                StrukturBehandeln: TryHandleStructuralClassifierResult,
                ErgebnisBehandeln: (result, frame) => CodingMultiModelAnalysisResultWorkflow.Execute(
                    new CodingMultiModelAnalysisResultWorkflowRequest(result, activityText),
                    new CodingMultiModelAnalysisResultWorkflowActions(
                        _liveDetectionStatusController.SetCodingAiState,
                        () => CodingSamMaskOverlayController.Clear(CodingOverlayCanvas),
                        _codingAnalysisContext.BuildSegmentedFindings,
                        ShowMultiModelResults,
                        (findings, imageWidth, imageHeight, yoloMaxConfidence) => AddMultiModelFindingsAsEvents(
                            findings,
                            imageWidth,
                            imageHeight,
                            yoloMaxConfidence,
                            frame)))));
    }
}
