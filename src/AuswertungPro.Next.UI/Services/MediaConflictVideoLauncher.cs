using AuswertungPro.Next.UI.Player;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.Services;

/// <summary>Oeffnet ein Konflikt-Video mit denselben Player-Einstellungen wie der normale Player.</summary>
internal static class MediaConflictVideoLauncher
{
    public static Action<string> Create(ServiceProvider services)
        => path =>
        {
            var options = PlayerWindowOptions.FromSettings(services.Settings);
            var window = new PlayerWindow(path, options, serviceProvider: services)
            {
                Owner = System.Windows.Application.Current?.MainWindow
            };
            window.Show();
        };
}
