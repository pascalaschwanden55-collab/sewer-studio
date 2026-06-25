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
                SetAnalyzingPhase: () => _eingabemarkerPhase = EingabemarkerPhase.Analyzing,
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
                ShowDuplicateStatus: (codeHint, meter) => SetCodingAiState(
                    $"{codeHint} bereits vorhanden bei {meter:F2}m - Duplikat",
                    PlayerStatusColors.Warning,
                    ""),
                AddDirectEvent: AddDirectEingabemarkerEvent,
                ShowAiFallbackStatus: keyword => SetCodingAiState(
                    $"KI analysiert: \"{keyword}\" ...",
                    PlayerStatusColors.Warning,
                    "Qwen analysiert"),
                RunAiFallbackAsync: keyword => RunCodingAnalysisAsync(
                    $"Eingabemarker: {keyword}",
                    disableAnalyzeButton: true,
                    keywordHint: keyword,
                    codeHint: null),
                ShowErrorStatus: message => SetCodingAiState($"Fehler: {message}", PlayerStatusColors.Error, ""),
                CancelMarker: CancelEingabemarker));
    }

    private void AddDirectEingabemarkerEvent(string codeHint, string keyword)
    {
        var meter = _codingOsdMeterController.LastMeter ?? _codingSessionHost.CurrentMeter;
        var videoTime = _codingSessionHost.CurrentVideoTime ?? _playerTimelineHost.CurrentTimeOrZero;
        var label = LookupVsaLabel(codeHint) ?? keyword;

        var draft = CodingEingabemarkerEventFactory.CreateAccepted(
            codeHint,
            label,
            keyword,
            meter,
            videoTime);

        var fotoPath = CodingCaptureSnapshot(draft.Entry);
        CodingProtocolEntryPhotoPathAppender.AddIfPresent(draft.Entry, fotoPath);

        var ev = CodingEingabemarkerEventAppender.Apply(
            draft,
            _codingSessionHost.CurrentOverlay,
            _codingSessionRuntimeOwner.Service!);
        RefreshCodingEventsList();
        UpdateToolBadge();
        PersistSingleEventAsTrainingSample(ev).SafeFireAndForget("TrainingSaveSingle");
        SetCodingAiState($"{codeHint} {label} bei {meter:F2}m eingetragen",
            PlayerStatusColors.Success,
            "");
    }
}
