using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.ViewModels.Windows;

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
        if (_codingVm == null) return;
        TxtCodingMeter.Text = $"{_codingVm.CurrentMeter:F2}m";
        PipeTimeline.CurrentMeter = _codingVm.CurrentMeter;
        // Video NUR synchronisieren wenn explizite Navigation (Next/Previous)
        // Verhindert Zurueckspringen beim normalen Abspielen
        if (propertyName is nameof(CodingSessionViewModel.CurrentMeter) && _codingNavPending)
        {
            _codingNavPending = false;
            SyncVideoToCodingMeter();
        }
        UpdateCodingOverlayInfo(_codingVm.CurrentOverlay);

        // Aktuellen Code am Zeitstempel anzeigen (Echtzeit)
        UpdateCodingCurrentCode();

        // Statistiken aktualisieren (nur bei relevanten Property-Aenderungen)
        if (CodingStatisticsRefreshPolicy.ShouldRefresh(propertyName))
        {
            UpdateCodingStatistics();
        }
    }

    /// <summary>
    /// Zeigt den naechsten existierenden Code in der Toolbar an, basierend auf aktuellem Meter.
    /// </summary>
    private void UpdateCodingCurrentCode()
    {
        if (_codingVm == null)
        {
            CodingCurrentCodeBadge.Visibility = Visibility.Collapsed;
            return;
        }

        var state = CodingCurrentCodeBadgePolicy.Build(
            _codingVm.Events,
            ResolveCurrentCodingDisplayMeter());

        TxtCodingCurrentCode.Text = state.Text;
        CodingCurrentCodeBadge.Visibility = state.IsVisible
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private double ResolveCurrentCodingDisplayMeter()
        => _codingVm == null
            ? 0
            : CodingCurrentMeterResolver.Resolve(
                _codingLastOsdMeter,
                _player.Time,
                _player.Length,
                _codingVm.EndMeter,
                _codingVm.CurrentMeter);

    private void SyncVideoToCodingMeter()
    {
        if (_codingVm == null) return;
        if (!CodingVideoSyncPolicy.TryResolveTargetTimeMs(
                _codingVm.CurrentMeter,
                _codingVm.EndMeter,
                _player.Length,
                out var targetMs))
            return;

        _player.Time = targetMs;
        _codingVm.CurrentVideoTime = TimeSpan.FromMilliseconds(_player.Time);
    }

    private async void CodingNext_Click(object sender, RoutedEventArgs e)
        => await MoveCodingByCommandAsync(
            vm => vm.MoveNextCommand.Execute(null),
            nameof(CodingNext_Click));

    private async void CodingPrevious_Click(object sender, RoutedEventArgs e)
        => await MoveCodingByCommandAsync(
            vm => vm.MovePreviousCommand.Execute(null),
            nameof(CodingPrevious_Click));

    private async Task MoveCodingByCommandAsync(
        Action<CodingSessionViewModel> executeMoveCommand,
        string traceName)
    {
        try
        {
            if (_codingVm == null) return;
            _codingNavPending = true;
            executeMoveCommand(_codingVm);
            _player.SetPause(true);
            _codingLastOsdMeter = null;
            _codingLastOsdTimestampSec = null;
            await CodingReadOsdMeterAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PlayerWindow] {traceName} error: {ex.Message}");
        }
    }
}
