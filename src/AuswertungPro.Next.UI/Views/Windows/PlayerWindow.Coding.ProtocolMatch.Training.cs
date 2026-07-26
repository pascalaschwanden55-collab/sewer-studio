using System.Threading.Tasks;
using System.Windows;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;
using AuswertungPro.Next.UI.Helpers;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void CodingAcceptGreenMatches_Click(object sender, RoutedEventArgs e)
        => HandleCodingAcceptGreenMatchesAsync().SafeFireAndForget("CodingAcceptGreenMatches");

    private async Task HandleCodingAcceptGreenMatchesAsync()
    {
        await CodingAcceptGreenMatchesCommandWorkflow.ExecuteAsync(
            new CodingAcceptGreenMatchesCommandRequest(
                _codingSessionHost.HasViewModel,
                _codingProtocolMatchState.LastMatch),
            new CodingAcceptGreenMatchesCommandActions(
                RunProtocolMatch: () => _codingProtocolMatchController.RunMatch(),
                GetCurrentRouting: () => _codingProtocolMatchState.LastMatch,
                AcceptGreenMatchesAsync: routing => CodingProtocolGreenMatchTrainingRunner.AcceptGreenMatchesAsync(
                    routing,
                    _codingImportReferenceEvents.Events,
                    ConfirmImportAsTrainingAsync),
                ShowOverlay: overlay => ShowOverlay(overlay.Text, overlay.Duration)));
    }

    private void ImportConfirm_Click(object sender, RoutedEventArgs e)
        => HandleImportConfirmAsync().SafeFireAndForget("ImportConfirm");

    private async Task HandleImportConfirmAsync()
    {
        await CodingImportConfirmCommandWorkflow.ExecuteAsync(
            new CodingImportConfirmCommandRequest(LstImportEvents.SelectedItem),
            new CodingImportConfirmCommandActions(ConfirmImportAsTrainingAsync));
    }

    private async Task<bool> ConfirmImportAsTrainingAsync(CodingEvent importEvent)
    {
        var result = await CodingProtocolImportTrainingConfirmationWorkflow.ConfirmAsync(
            importEvent,
            importEventToSeek => _codingProtocolMatchController.SeekImportEvent(importEventToSeek),
            () => TryTakeSnapshot(out var snapshotPath) ? snapshotPath : null,
            CodingProtocolGuidedVerificationAdapter.Create(_codingAiRuntimeOwner.Controller.ProtocolVerifier), _protocolContext.TeacherAnnotations);
        return CodingImportTrainingResultWorkflow.Execute(
            result,
            new CodingImportTrainingResultDisplayActions(
                ShowBadge: text => CodingOsdBadgeControls.Show(OsdMeterBadge, TxtOsdMeter, text),
                HideBadge: () => CodingOsdBadgeControls.Hide(OsdMeterBadge))).Accepted;
    }
}
