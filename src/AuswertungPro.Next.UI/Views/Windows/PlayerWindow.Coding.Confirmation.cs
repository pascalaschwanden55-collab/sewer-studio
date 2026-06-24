using System.Windows;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.QualityGate;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Helpers;

using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void InitializeCodingConfirmationPanelControls()
    {
        _codingConfirmationPanelControls = new CodingConfirmationPanelControls(
            CodingConfirmationPanel,
            ConfirmAmpel,
            TxtConfirmCode,
            TxtConfirmConfidence,
            TxtConfirmDescription,
            TxtConfirmDetail);
    }

    private void PauseAndAskConfirmation(CodingEvent codingEvent, QualityGateResult gateResult)
    {
        PlayerConfirmationPlayback.PauseCodingConfirmation(pause => _player.SetPause(pause));
        _codingSessionService?.SetWaitingForInput();

        _codingPendingConfirmEvent = codingEvent;
        _codingPendingGateResult = gateResult;

        var ampelColor = _codingConfirmationPanelControls.Apply(codingEvent, gateResult);

        SetCodingAiState(TxtCodingAiStatus.Text, ampelColor,
            CodingConfirmationDisplayPolicy.QualityGateStatusText(gateResult));
    }

    private void ConfirmAccept_Click(object sender, RoutedEventArgs e)
    {
        CodingConfirmationDecisionWorkflow.Accept(
            _codingPendingConfirmEvent,
            _codingPendingGateResult,
            codingEvent => PersistSingleEventAsTrainingSample(codingEvent).SafeFireAndForget("TrainingSaveAccept"));

        CloseConfirmationAndResume();
    }

    private void ConfirmEdit_Click(object sender, RoutedEventArgs e)
    {
        var selectedEvent = CodingConfirmationDecisionWorkflow.Edit(
            _codingPendingConfirmEvent,
            _codingPendingGateResult);

        CloseConfirmationPanel();

        if (selectedEvent != null)
            LstCodingEvents.SelectedItem = selectedEvent;

        ResumeAfterConfirmation();
    }

    private void ConfirmReject_Click(object sender, RoutedEventArgs e)
    {
        CodingConfirmationDecisionWorkflow.Reject(
            _codingPendingConfirmEvent,
            _codingPendingGateResult,
            _codingSessionService,
            _codingVm?.Events,
            codingEvent => PersistSingleEventAsTrainingSample(codingEvent).SafeFireAndForget("TrainingSaveReject"),
            RefreshCodingEventsList);

        CloseConfirmationAndResume();
    }

    private void CloseConfirmationAndResume()
    {
        CloseConfirmationPanel();
        ResumeAfterConfirmation();
    }

    private void CloseConfirmationPanel()
    {
        _codingConfirmationPanelControls.Hide();
        _codingPendingConfirmEvent = null;
        _codingPendingGateResult = null;
    }

    private void ResumeAfterConfirmation()
    {
        if (_codingSessionService?.ActiveSession?.State == CodingSessionState.WaitingForUserInput)
            _codingSessionService.ResumeSession();

        var isLiveAiEnabled = BtnCodingLiveAi.IsChecked == true;
        PlayerConfirmationPlayback.ResumeCodingLiveAi(isLiveAiEnabled, pause => _player.SetPause(pause));

        if (isLiveAiEnabled)
        {
            var status = CodingLiveAiButtonDisplayPolicy.BuildStatus(
                isActive: true,
                LiveDetectionDisplayPolicy.CompactModelName(_codingAiModelName));
            SetCodingAiState(status.StatusText, PlayerStatusColors.Success, status.DetailText);
        }
        else
        {
            var status = CodingLiveAiButtonDisplayPolicy.BuildStatus(
                isActive: false,
                LiveDetectionDisplayPolicy.CompactModelName(_codingAiModelName));
            SetCodingAiState(status.StatusText, PlayerStatusColors.Success, status.DetailText);
        }
    }
}
