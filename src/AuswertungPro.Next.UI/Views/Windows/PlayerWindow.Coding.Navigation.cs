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
        var result = CodingUiUpdateCommandWorkflow.Execute(
            new CodingUiUpdateCommandRequest(
                _codingSessionHost.HasViewModel,
                propertyName,
                _codingNavPending),
            new CodingUiUpdateCommandActions(
                ApplyUiUpdate: (changedPropertyName, navigationPending) => CodingUiUpdateWorkflow.Apply(
                    changedPropertyName,
                    navigationPending,
                    new CodingUiUpdateActions(
                        ApplyMeterTimeline: () => CodingMeterTimelineControls.Apply(TxtCodingMeter, PipeTimeline, _codingSessionHost.CurrentMeter),
                        SyncVideoToCodingMeter: SyncVideoToCodingMeter,
                        UpdateOverlayInfo: () => UpdateCodingOverlayInfo(_codingSessionHost.CurrentOverlay),
                        UpdateCurrentCode: UpdateCodingCurrentCode,
                        UpdateStatistics: UpdateCodingStatistics))));
        _codingNavPending = result.NavigationPending;
    }

    /// <summary>
    /// Zeigt den naechsten existierenden Code in der Toolbar an, basierend auf aktuellem Meter.
    /// </summary>
    private void UpdateCodingCurrentCode()
    {
        CodingCurrentCodeUpdateWorkflow.Execute(
            new CodingCurrentCodeUpdateRequest(_codingSessionHost.HasViewModel),
            new CodingCurrentCodeUpdateActions(
                GetEvents: () => _codingSessionHost.Events,
                ResolveCurrentMeter: ResolveCurrentCodingDisplayMeter,
                ApplyState: state => CodingCurrentCodeBadgeControls.Apply(
                    CodingCurrentCodeBadge,
                    TxtCodingCurrentCode,
                    state)));
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
        => CodingVideoSyncCommandWorkflow.Execute(
            new CodingVideoSyncCommandRequest(_codingSessionHost.HasViewModel),
            new CodingVideoSyncCommandActions(
                SyncVideoToCodingMeter: () => CodingVideoNavigationController.SyncVideoToCodingMeter(
                    _codingSessionHost.CurrentMeter,
                    _codingSessionHost.EndMeter,
                    _playerTimelineHost.LengthMilliseconds ?? 0,
                    _playerTimelineHost.SeekMilliseconds,
                    () => _playerTimelineHost.TimeMilliseconds ?? 0,
                    _codingSessionHost.SetCurrentVideoTime)));

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
        await CodingMoveByCommandWorkflow.ExecuteAsync(
            new CodingMoveByCommandRequest(traceName),
            new CodingMoveByCommandActions(
                PrepareMoveByCommand: () => CodingVideoNavigationController.PrepareMoveByCommand(
                    _codingSessionHost.HasViewModel ? _codingSessionHost : null,
                    executeMoveCommand,
                    () => _codingNavPending = true,
                    () => PlayerCodingPlayback.PauseForCodingInteraction(_playerPlaybackControlHost.SetPause),
                    () =>
                    {
                        _codingOsdMeterController.ResetRecentMeter();
                    }),
                ReadOsdMeterAsync: CodingReadOsdMeterAsync,
                TraceError: message => PlayerTrace.WriteLine(message)));
    }
}
