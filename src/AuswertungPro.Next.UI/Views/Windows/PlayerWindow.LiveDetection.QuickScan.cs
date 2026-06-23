using System.Windows;
using AuswertungPro.Next.UI.Helpers;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    // Duenne Huelle: XAML bindet Click="QuickScan_Click"; die Logik liegt im QuickScanController.
    private void QuickScan_Click(object sender, RoutedEventArgs e)
        => _quickScanController.ToggleAsync().SafeFireAndForget("QuickScan");
}
