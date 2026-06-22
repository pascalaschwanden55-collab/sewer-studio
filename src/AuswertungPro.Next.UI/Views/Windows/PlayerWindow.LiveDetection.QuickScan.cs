using System.Windows;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    // Duenne Huelle: XAML bindet Click="QuickScan_Click"; die Logik liegt im QuickScanController.
    private async void QuickScan_Click(object sender, RoutedEventArgs e)
        => await _quickScanController.ToggleAsync();
}
