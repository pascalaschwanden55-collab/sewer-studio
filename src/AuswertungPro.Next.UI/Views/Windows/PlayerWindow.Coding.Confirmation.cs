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
        CodingConfirmationPauseWorkflow.Execute(
            new CodingConfirmationPauseWorkflowRequest(
                codingEvent,
                gateResult,
                TxtCodingAiStatus.Text,
                _codingSessionRuntimeOwner.Service),
            new CodingConfirmationPauseWorkflowActions(
                SetPause: _playerPlaybackControlHost.SetPause,
                StorePendingConfirmation: (pendingEvent, pendingGate) =>
                {
                    _codingPendingConfirmEvent = pendingEvent;
                    _codingPendingGateResult = pendingGate;
                },
                ApplyConfirmationPanel: _codingConfirmationPanelControls.Apply,
                ShowStatus: (status, color, detail) => SetCodingAiState(status, color, detail)));
    }

    private void ConfirmAccept_Click(object sender, RoutedEventArgs e)
    {
        CodingConfirmationDecisionCommandWorkflow.Execute(
            new CodingConfirmationDecisionCommandActions(
                ApplyDecision: () => CodingConfirmationDecisionWorkflow.Accept(
                    _codingPendingConfirmEvent,
                    _codingPendingGateResult,
                    codingEvent => PersistSingleEventAsTrainingSample(codingEvent).SafeFireAndForget("TrainingSaveAccept")),
                CloseConfirmationPanel: CloseConfirmationPanel,
                ResumeAfterConfirmation: ResumeAfterConfirmation));
    }

    private void ConfirmEdit_Click(object sender, RoutedEventArgs e)
    {
        CodingConfirmationEditCommandWorkflow.Execute(
            new CodingConfirmationEditCommandActions(
                EditConfirmation: () => CodingConfirmationDecisionWorkflow.Edit(
                    _codingPendingConfirmEvent,
                    _codingPendingGateResult),
                CloseConfirmationPanel: CloseConfirmationPanel,
                SelectEvent: codingEvent => LstCodingEvents.SelectedItem = codingEvent,
                ResumeAfterConfirmation: ResumeAfterConfirmation));
    }

    private void ConfirmReject_Click(object sender, RoutedEventArgs e)
    {
        CodingConfirmationDecisionCommandWorkflow.Execute(
            new CodingConfirmationDecisionCommandActions(
                ApplyDecision: () => CodingConfirmationDecisionWorkflow.Reject(
                    _codingPendingConfirmEvent,
                    _codingPendingGateResult,
                    _codingSessionRuntimeOwner.Service,
                    _codingSessionHost.EventCollection,
                    codingEvent => PersistSingleEventAsTrainingSample(codingEvent).SafeFireAndForget("TrainingSaveReject"),
                    RefreshCodingEventsList),
                CloseConfirmationPanel: CloseConfirmationPanel,
                ResumeAfterConfirmation: ResumeAfterConfirmation));
    }

    private void CloseConfirmationPanel()
    {
        _codingConfirmationPanelControls.Hide();
        _codingPendingConfirmEvent = null;
        _codingPendingGateResult = null;
    }

    private void ResumeAfterConfirmation()
    {
        var result = CodingConfirmationResumeWorkflow.Apply(
            _codingSessionRuntimeOwner.Service,
            BtnCodingLiveAi.IsChecked == true,
            _codingAiRuntimeOwner.Controller.ModelName,
            _playerPlaybackControlHost.SetPause);

        SetCodingAiState(result.Status.StatusText, PlayerStatusColors.Success, result.Status.DetailText);
    }
}
