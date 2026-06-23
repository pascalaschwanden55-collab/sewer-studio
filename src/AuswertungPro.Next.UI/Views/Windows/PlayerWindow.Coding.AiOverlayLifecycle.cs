using System;
using System.Windows;
using System.Windows.Threading;

using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    /// <summary>Alle Overlays/Einblendungen vom Video entfernen.</summary>
    private void CodingClearOverlays_Click(object sender, RoutedEventArgs e)
        => ClearDetectionOverlays();

    /// <summary>Detection-Overlays aufraeumen (Boxen, Labels, Findings-Liste).</summary>
    private void ClearDetectionOverlays()
    {
        DetectionOverlayCleaner.ClearAll(DetectionCanvas, DetectionOverlayGrid, CodingFindingsList);
    }

    // Analyse-Boxen kurz zeigen, dann nach 3s automatisch ausblenden, damit der Frame nicht
    // zugekleistert wird. WICHTIG: nur die visuellen Boxen entfernen - die Befundliste (KI-BEFUNDE)
    // bleibt stehen (deshalb NICHT ClearDetectionOverlays, das wuerde die Liste mitnehmen).
    private DispatcherTimer? _detectionAutoHideTimer;

    private void ScheduleDetectionAutoHide()
    {
        if (_detectionAutoHideTimer == null)
        {
            _detectionAutoHideTimer = PlayerWindowTimerFactory.CreateOneShotTimer(
                TimeSpan.FromSeconds(3),
                () =>
            {
                DetectionOverlayCleaner.ClearVisuals(DetectionCanvas, DetectionOverlayGrid);
            });
        }
        _detectionAutoHideTimer.Stop();
        _detectionAutoHideTimer.Start();
    }

    /// <summary>
    /// Nach Accept/Reject/Edit: Overlay kurz in Statusfarbe anzeigen, dann ausblenden.
    /// So sieht der User die Bestaetigung, das Bild wird aber danach wieder frei.
    /// </summary>
    private void FadeOutAiOverlayAfterAction()
    {
        // Sofort neu rendern (zeigt gruen/rot je nach Decision)
        RenderAiOverlays();
        // Nach 800ms die KI-Overlays entfernen
        var timer = PlayerWindowTimerFactory.CreateOneShotTimer(TimeSpan.FromMilliseconds(800), () =>
        {
            CodingOverlayCanvasCleaner.ClearAiOverlays(CodingOverlayCanvas);
        });
        timer.Start();
    }
}
