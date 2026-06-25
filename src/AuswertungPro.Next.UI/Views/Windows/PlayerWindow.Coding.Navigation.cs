using System.ComponentModel;
using System.Windows;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Helpers;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    // Flag: wird true wenn Meter-Navigation (Next/Previous) ausloest.
    private bool _codingNavPending;

    // Benannter Handler fuer sauberes Cleanup via -=
    private void CodingVm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        => Dispatcher.InvokeAsync(() => UpdateCodingUi(e.PropertyName));

    private void UpdateCodingUi(string? propertyName)
    {
        if (!_codingSessionHost.HasViewModel) return;

        var result = CodingUiUpdateWorkflow.Apply(
            propertyName,
            _codingNavPending,
            new CodingUiUpdateActions(
                ApplyMeterTimeline: () => CodingMeterTimelineControls.Apply(TxtCodingMeter, PipeTimeline, _codingSessionHost.CurrentMeter),
                SyncVideoToCodingMeter: SyncVideoToCodingMeter,
                UpdateOverlayInfo: () => UpdateCodingOverlayInfo(_codingSessionHost.CurrentOverlay),
                UpdateCurrentCode: UpdateCodingCurrentCode,
                UpdateStatistics: UpdateCodingStatistics));
        _codingNavPending = result.NavigationPending;
    }

    /// <summary>
    /// Zeigt den naechsten existierenden Code in der Toolbar an, basierend auf aktuellem Meter.
    /// </summary>
    private void UpdateCodingCurrentCode()
    {
        if (!_codingSessionHost.HasViewModel)
        {
            CodingCurrentCodeBadgeControls.Apply(
                CodingCurrentCodeBadge,
                TxtCodingCurrentCode,
                CodingCurrentCodeBadgeState.Hidden);
            return;
        }

        var state = CodingCurrentCodeBadgePolicy.Build(
            _codingSessionHost.Events,
            ResolveCurrentCodingDisplayMeter());

        CodingCurrentCodeBadgeControls.Apply(CodingCurrentCodeBadge, TxtCodingCurrentCode, state);
    }

    private double ResolveCurrentCodingDisplayMeter()
        => !_codingSessionHost.HasViewModel
            ? 0
            : CodingVideoNavigationController.ResolveDisplayMeter(
                _codingOsdMeterController.LastMeter,
                _playerTimelineHost.TimeMilliseconds ?? 0,
                _playerTimelineHost.LengthMilliseconds ?? 0,
                _codingSessionHost.EndMeter,
                _codingSessionHost.CurrentMeter);

    private void SyncVideoToCodingMeter()
    {
        if (!_codingSessionHost.HasViewModel) return;
        CodingVideoNavigationController.SyncVideoToCodingMeter(
            _codingSessionHost.CurrentMeter,
            _codingSessionHost.EndMeter,
            _playerTimelineHost.LengthMilliseconds ?? 0,
            _playerTimelineHost.SeekMilliseconds,
            () => _playerTimelineHost.TimeMilliseconds ?? 0,
            _codingSessionHost.SetCurrentVideoTime);
    }

    private void CodingNext_Click(object sender, RoutedEventArgs e)
        => MoveCodingByCommandAsync(
            host => host.ExecuteMoveNext(),
            nameof(CodingNext_Click))
            .SafeFireAndForget("CodingNext");

    private void CodingPrevious_Click(object sender, RoutedEventArgs e)
        => MoveCodingByCommandAsync(
            host => host.ExecuteMovePrevious(),
            nameof(CodingPrevious_Click))
            .SafeFireAndForget("CodingPrevious");

    private async Task MoveCodingByCommandAsync(
        Action<ICodingSessionHost> executeMoveCommand,
        string traceName)
    {
        try
        {
            if (!CodingVideoNavigationController.PrepareMoveByCommand(
                    _codingSessionHost.HasViewModel ? _codingSessionHost : null,
                    executeMoveCommand,
                    () => _codingNavPending = true,
                    () => PlayerCodingPlayback.PauseForCodingInteraction(_playerPlaybackControlHost.SetPause),
                    () =>
                    {
                        _codingOsdMeterController.ResetRecentMeter();
                    }))
                return;

            await CodingReadOsdMeterAsync();
        }
        catch (Exception ex)
        {
            PlayerTrace.WriteLine($"[PlayerWindow] {traceName} error: {ex.Message}");
        }
    }
}
