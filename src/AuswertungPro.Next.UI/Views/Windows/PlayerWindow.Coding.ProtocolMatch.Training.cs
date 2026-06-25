using System.Threading.Tasks;
using System.Windows;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Helpers;
using AuswertungPro.Next.UI.Player;

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
                _lastCodingMatch),
            new CodingAcceptGreenMatchesCommandActions(
                RunProtocolMatch: RunCodingProtocolMatch,
                GetCurrentRouting: () => _lastCodingMatch,
                AcceptGreenMatchesAsync: routing => CodingProtocolGreenMatchTrainingRunner.AcceptGreenMatchesAsync(
                    routing,
                    _codingImportEvents,
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
        var result = await CodingProtocolImportTrainingWorkflowServiceFactory.Create(
                SeekToImportEvent,
                () => TryTakeSnapshot(out var snapshotPath) ? snapshotPath : null)
            .ConfirmAsync(importEvent);
        if (!result.Accepted)
            return false;

        var badge = result.Badge;
        CodingOsdBadgeControls.Show(OsdMeterBadge, TxtOsdMeter, badge.Text);
        var resetTimer = PlayerWindowTimerFactory.CreateOneShotTimer(
            badge.AutoHideDelay,
            () => CodingOsdBadgeControls.Hide(OsdMeterBadge));
        resetTimer.Start();
        return true;
    }
}
