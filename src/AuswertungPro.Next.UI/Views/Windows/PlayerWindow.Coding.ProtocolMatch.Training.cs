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
        if (_codingVm == null) return;
        if (_lastCodingMatch == null)
            RunCodingProtocolMatch();
        if (_lastCodingMatch == null)
            return;

        var overlay = await CodingProtocolGreenMatchTrainingRunner.AcceptGreenMatchesAsync(
            _lastCodingMatch,
            _codingImportEvents,
            ConfirmImportAsTrainingAsync);
        if (overlay.HasValue)
            ShowOverlay(overlay.Value.Text, overlay.Value.Duration);
    }

    private void ImportConfirm_Click(object sender, RoutedEventArgs e)
        => HandleImportConfirmAsync().SafeFireAndForget("ImportConfirm");

    private async Task HandleImportConfirmAsync()
    {
        if (LstImportEvents.SelectedItem is not CodingEvent importEvent) return;
        await ConfirmImportAsTrainingAsync(importEvent);
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
        OsdMeterBadge.Visibility = Visibility.Visible;
        TxtOsdMeter.Text = badge.Text;
        var resetTimer = PlayerWindowTimerFactory.CreateOneShotTimer(
            badge.AutoHideDelay,
            () => OsdMeterBadge.Visibility = Visibility.Collapsed);
        resetTimer.Start();
        return true;
    }
}
