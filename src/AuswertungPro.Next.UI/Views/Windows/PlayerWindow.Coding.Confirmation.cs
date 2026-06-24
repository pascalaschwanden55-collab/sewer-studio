using System.Windows;
using System.Windows.Media;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.QualityGate;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Helpers;

using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void PauseAndAskConfirmation(CodingEvent codingEvent, QualityGateResult gateResult)
    {
        PlayerConfirmationPlayback.PauseCodingConfirmation(pause => _player.SetPause(pause));
        _codingSessionService?.SetWaitingForInput();

        _codingPendingConfirmEvent = codingEvent;
        _codingPendingGateResult = gateResult;

        var ampelColor = CodingConfirmationDisplayPolicy.AmpelColor(gateResult);
        ConfirmAmpel.Fill = new SolidColorBrush(ampelColor);

        SetCodingAiState(TxtCodingAiStatus.Text, ampelColor,
            CodingConfirmationDisplayPolicy.QualityGateStatusText(gateResult));

        TxtConfirmCode.Text = codingEvent.Entry.Code ?? "???";
        TxtConfirmConfidence.Text = $"({gateResult.CompositeConfidence:P0})";
        TxtConfirmDescription.Text = codingEvent.Entry.Beschreibung ?? codingEvent.AiContext?.Reason ?? "";
        TxtConfirmDetail.Text = CodingConfirmationDisplayPolicy.ConfirmationDetail(gateResult);

        CodingConfirmationPanel.Visibility = Visibility.Visible;
    }

    private void ConfirmAccept_Click(object sender, RoutedEventArgs e)
    {
        if (CodingEventDecisionPolicy.ApplyAiConfirmationDecision(
                _codingPendingConfirmEvent,
                CodingUserDecision.Accepted,
                _codingPendingGateResult))
        {
            PersistSingleEventAsTrainingSample(_codingPendingConfirmEvent!).SafeFireAndForget("TrainingSaveAccept");
        }

        CloseConfirmationAndResume();
    }

    private void ConfirmEdit_Click(object sender, RoutedEventArgs e)
    {
        CloseConfirmationPanel();

        if (_codingPendingConfirmEvent != null)
        {
            CodingEventDecisionPolicy.ApplyAiConfirmationDecision(
                _codingPendingConfirmEvent,
                CodingUserDecision.AcceptedWithEdit,
                _codingPendingGateResult);
            LstCodingEvents.SelectedItem = _codingPendingConfirmEvent;
        }

        ResumeAfterConfirmation();
    }

    private void ConfirmReject_Click(object sender, RoutedEventArgs e)
    {
        if (_codingPendingConfirmEvent != null)
        {
            CodingEventDecisionPolicy.ApplyAiConfirmationDecision(
                _codingPendingConfirmEvent,
                CodingUserDecision.Rejected,
                _codingPendingGateResult);

            PersistSingleEventAsTrainingSample(_codingPendingConfirmEvent).SafeFireAndForget("TrainingSaveReject");

            CodingEventDeleteApplier.Apply(
                _codingPendingConfirmEvent, _codingSessionService, _codingVm?.Events, selectedDefect: null);
            RefreshCodingEventsList();
        }

        CloseConfirmationAndResume();
    }

    private void CloseConfirmationAndResume()
    {
        CloseConfirmationPanel();
        ResumeAfterConfirmation();
    }

    private void CloseConfirmationPanel()
    {
        CodingConfirmationPanel.Visibility = Visibility.Collapsed;
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
