using System;
using System.Windows;
using System.Windows.Threading;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    /// <summary>Alle Overlays/Einblendungen vom Video entfernen.</summary>
    private void CodingClearOverlays_Click(object sender, RoutedEventArgs e)
        => ClearDetectionOverlays();

    /// <summary>Detection-Overlays aufraeumen (Boxen, Labels, Findings-Liste).</summary>
    private void ClearDetectionOverlays()
    {
        DetectionCanvas.Children.Clear();
        DetectionOverlayGrid.Visibility = Visibility.Collapsed;
        CodingFindingsList.ItemsSource = null;
    }

    // Analyse-Boxen kurz zeigen, dann nach 3s automatisch ausblenden, damit der Frame nicht
    // zugekleistert wird. WICHTIG: nur die visuellen Boxen entfernen - die Befundliste (KI-BEFUNDE)
    // bleibt stehen (deshalb NICHT ClearDetectionOverlays, das wuerde die Liste mitnehmen).
    private DispatcherTimer? _detectionAutoHideTimer;

    private void ScheduleDetectionAutoHide()
    {
        if (_detectionAutoHideTimer == null)
        {
            _detectionAutoHideTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(3)
            };
            _detectionAutoHideTimer.Tick += (s, e) =>
            {
                _detectionAutoHideTimer!.Stop();
                DetectionCanvas.Children.Clear();
                DetectionOverlayGrid.Visibility = Visibility.Collapsed;
            };
        }
        _detectionAutoHideTimer.Stop();
        _detectionAutoHideTimer.Start();
    }
}
