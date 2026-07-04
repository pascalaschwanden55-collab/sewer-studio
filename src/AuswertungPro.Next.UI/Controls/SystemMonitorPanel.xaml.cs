using System.Windows.Controls;
using System.Windows;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Controls;

public partial class SystemMonitorPanel : UserControl
{
    public SystemMonitorPanel()
    {
        InitializeComponent();
    }

    private void Detach_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;

        var owner = Window.GetWindow(this);
        var panel = new SystemMonitorPanel
        {
            DataContext = DataContext,
            Margin = new Thickness(12)
        };

        var window = new Window
        {
            Title = "System-Monitor",
            Owner = owner,
            Width = 420,
            Height = 520,
            MinWidth = 360,
            MinHeight = 420,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = panel
        };

        WindowStateManager.Track(window, "SystemMonitorWindow");
        window.Show();
    }
}
