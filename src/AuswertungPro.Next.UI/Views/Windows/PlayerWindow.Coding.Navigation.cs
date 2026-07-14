using System.ComponentModel;
using System.Windows;
using AuswertungPro.Next.UI.Helpers;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    // Benannter Handler fuer sauberes Cleanup via -=
    private void CodingVm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        => PlayerDispatcherScheduler.ScheduleNormal(
            Dispatcher,
            () => _codingNavigationController.UpdateUi(e.PropertyName));

    /// <summary>
    /// Zeigt den naechsten existierenden Code in der Toolbar an, basierend auf aktuellem Meter.
    /// </summary>
    private void UpdateCodingCurrentCode()
        => _codingNavigationController.UpdateCurrentCode();

    private void SyncVideoToCodingMeter()
        => _codingNavigationController.SyncVideoToCodingMeter();

    private void CodingNext_Click(object sender, RoutedEventArgs e)
        => _codingNavigationController
            .MoveNextAsync(nameof(CodingNext_Click))
            .SafeFireAndForget("CodingNext");

    private void CodingPrevious_Click(object sender, RoutedEventArgs e)
        => _codingNavigationController
            .MovePreviousAsync(nameof(CodingPrevious_Click))
            .SafeFireAndForget("CodingPrevious");
}
