using System.Windows;
using AuswertungPro.Next.Application.Ai.QualityGate;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void InitializeCodingConfirmationPanelControls()
    {
        PlayerCodingConfirmationPanelInitializer.Initialize(
            _codingConfirmationPanelControls,
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
                StorePendingConfirmation: _codingPendingConfirmationState.Store,
                ApplyConfirmationPanel: _codingConfirmationPanelControls.Apply,
                ShowStatus: (status, color, detail) => SetCodingAiState(status, color, detail)));
    }

    private void ConfirmAccept_Click(object sender, RoutedEventArgs e)
        => _codingConfirmationDecisionController.Accept();

    private void ConfirmEdit_Click(object sender, RoutedEventArgs e)
        => _codingConfirmationDecisionController.Edit();

    private void ConfirmReject_Click(object sender, RoutedEventArgs e)
        => _codingConfirmationDecisionController.Reject();
}
