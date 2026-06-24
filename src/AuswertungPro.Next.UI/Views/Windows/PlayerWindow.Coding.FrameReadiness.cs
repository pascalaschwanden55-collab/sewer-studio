using AuswertungPro.Next.Application.Ai;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    /// <summary>Setzt den Einblendungs-Zustand zurueck (bei Eintritt/Austritt Codier-Modus).</summary>
    private void ResetFrameReadiness()
    {
        _codingFrameReadinessController.Reset();
        _codingOsdMeterController.ResetRecentMeter();
    }

    /// <summary>
    /// Reine Bewertung: Ist der aktuelle Frame bereit fuer die Analyse?
    /// Aendert KEINEN Zustand - dafuer ist UpdateFrameReadiness zustaendig.
    /// </summary>
    private bool IsFrameReady() => _codingFrameReadinessController.IsReady;

    /// <summary>
    /// Aktualisiert den Einblendungs-Zustand anhand des aktuellen Analyse-Ergebnisses.
    /// Muss VOR IsFrameReady aufgerufen werden.
    ///
    /// Uebergaenge:
    ///   WaitingForVideo -> Warmup: erster Frame mit Meterstand (aus aktuellem result)
    ///   WaitingForVideo -> Ready: 3 Frames ohne Meter (kein OSD vorhanden)
    ///   Warmup          -> Ready: 2. Frame mit Meterstand (Bestaetigung)
    ///   Warmup          -> Ready: 2 Frames in Warmup ohne zweiten Meter (Fallback gegen Deadlock)
    /// </summary>
    private void UpdateFrameReadiness(LiveDetection result)
    {
        var fallbackTimestamp = _player != null ? _player.Time / 1000.0 : 0.0;
        _codingFrameReadinessController.Update(result, fallbackTimestamp);
    }
}
