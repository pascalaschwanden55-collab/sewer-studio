using System.Windows;

using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;
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
        DetectionOverlayCleanupController.ClearAll(DetectionCanvas, DetectionOverlayGrid, CodingFindingsList);
    }

    // Analyse-Boxen kurz zeigen, dann nach 3s automatisch ausblenden, damit der Frame nicht
    // zugekleistert wird. WICHTIG: nur die visuellen Boxen entfernen - die Befundliste (KI-BEFUNDE)
    // bleibt stehen (deshalb NICHT ClearDetectionOverlays, das wuerde die Liste mitnehmen).
    private void ScheduleDetectionAutoHide()
        => CodingAiOverlayLifecycleWorkflow.ScheduleAutoHide(
            _codingAiOverlayAutoHideTimerOwner.CreateRequest(),
            _codingAiOverlayAutoHideTimerOwner.CreateActions(
                () => DetectionOverlayCleanupController.ClearVisuals(
                    DetectionCanvas,
                    DetectionOverlayGrid)));

    /// <summary>
    /// Nach Accept/Reject/Edit: Overlay kurz in Statusfarbe anzeigen, dann ausblenden.
    /// So sieht der User die Bestaetigung, das Bild wird aber danach wieder frei.
    /// </summary>
    private void FadeOutAiOverlayAfterAction()
        => CodingAiOverlayLifecycleWorkflow.FadeOutAfterAction(
            new CodingAiOverlayFadeOutHostActions(
                RenderAiOverlays: RenderAiOverlays,
                ClearAiOverlays: () => CodingOverlayCleanupController.ClearAiOverlays(CodingOverlayCanvas)));
}
