using System;
using System.Threading.Tasks;
using System.Windows;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Helpers;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private async Task SubmitEingabemarker()
    {
        await CodingEingabemarkerSubmissionWorkflow.ExecuteAsync(
            new CodingEingabemarkerSubmissionWorkflowRequest(
                TxtEingabemarker.Text,
                _codingSessionHost.HasViewModel,
                _codingSessionRuntimeOwner.Service != null),
            new CodingEingabemarkerSubmissionWorkflowActions(
                HideInput: () => CodingEingabemarkerPopupControls.Hide(EingabemarkerPopup),
                SetAnalyzingPhase: _codingEingabemarkerInteractionController.SetAnalyzingPhase,
                ResolveCodeHint: ResolveEingabemarkerCodeHint,
                FindDuplicate: codeHint =>
                {
                    var checkMeter = _codingOsdMeterController.LastMeter ?? _codingSessionHost.CurrentMeter;
                    var duplicate = CodingEingabemarkerDuplicatePolicy.FindDuplicate(
                        _codingSessionHost.Events,
                        codeHint,
                        checkMeter);
                    return duplicate == null
                        ? null
                        : new CodingEingabemarkerDuplicateMatch(duplicate.MeterAtCapture);
                },
                ShowDuplicateStatus: (codeHint, meter) => _liveDetectionStatusController.SetCodingAiState(
                    $"{codeHint} bereits vorhanden bei {meter:F2}m - Duplikat",
                    PlayerStatusColors.Warning,
                    ""),
                AddDirectEvent: AddDirectEingabemarkerEvent,
                ShowAiFallbackStatus: keyword => _liveDetectionStatusController.SetCodingAiState(
                    $"KI analysiert: \"{keyword}\" ...",
                    PlayerStatusColors.Warning,
                    "Qwen analysiert"),
                RunAiFallbackAsync: keyword => RunCodingAnalysisAsync(
                    $"Eingabemarker: {keyword}",
                    disableAnalyzeButton: true,
                    keywordHint: keyword,
                    codeHint: null),
                ShowErrorStatus: message => _liveDetectionStatusController.SetCodingAiState($"Fehler: {message}", PlayerStatusColors.Error, ""),
                CancelMarker: () => _codingEingabemarkerInteractionController.Cancel()));
    }

    private void AddDirectEingabemarkerEvent(string codeHint, string keyword)
    {
        CodingEingabemarkerDirectEventWorkflow.Execute(
            new CodingEingabemarkerDirectEventWorkflowRequest(
                codeHint,
                keyword,
                _codingSessionHost.CurrentOverlay,
                _codingSessionRuntimeOwner.Service!),
            new CodingEingabemarkerDirectEventWorkflowActions(
                ResolveMeter: () => _codingOsdMeterController.LastMeter ?? _codingSessionHost.CurrentMeter,
                ResolveVideoTime: () => _codingSessionHost.CurrentVideoTime ?? _playerTimelineHost.CurrentTimeOrZero,
                LookupLabel: _codingFindingContext.LookupLabel,
                CapturePhoto: CodingCaptureSnapshot,
                RefreshEvents: RefreshCodingEventsList,
                UpdateToolBadge: UpdateToolBadge,
                PersistTraining: ev => _codingTrainingPersistenceContext.PersistSingleEventAsync(ev).SafeFireAndForget("TrainingSaveSingle"),
                ShowSuccessStatus: (code, label, meter) => _liveDetectionStatusController.SetCodingAiState(
                    $"{code} {label} bei {meter:F2}m eingetragen",
                    PlayerStatusColors.Success,
                    "")));
    }
}
