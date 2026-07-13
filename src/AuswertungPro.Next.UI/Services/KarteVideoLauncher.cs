using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Player;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.Services;

/// <summary>Oeffnet ein Karten-Video mit Haltungskontext im normalen Player.</summary>
internal static class KarteVideoLauncher
{
    public static Action<string, HaltungRecord> Create(ServiceProvider services)
        => (path, record) =>
        {
            var options = PlayerWindowOptions.FromSettings(services.Settings);
            var window = new PlayerWindow(
                path,
                options,
                serviceProvider: services,
                haltungId: record.Id.ToString(),
                haltungRecord: record)
            {
                Owner = System.Windows.Application.Current?.MainWindow
            };
            window.Show();
        };
}
