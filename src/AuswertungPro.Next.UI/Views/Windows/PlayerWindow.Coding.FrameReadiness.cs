using AuswertungPro.Next.Application.Ai;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    // Warmup-Puffer: Ergebnis aus der Warmup-Phase wird zwischengespeichert
    // und nach Transition zu Ready nachtraeglich verarbeitet.
    private LiveDetection? _pendingWarmupResult;

    /// <summary>Setzt den Einblendungs-Zustand zurueck (bei Eintritt/Austritt Codier-Modus).</summary>
    private void ResetFrameReadiness()
    {
        _codingFrameReadiness.Reset();
        _codingLastOsdMeter = null; // Stale Meter aus vorheriger Session verhindern
        _codingLastOsdTimestampSec = null;
        _pendingWarmupResult = null;
    }

    /// <summary>
    /// Reine Bewertung: Ist der aktuelle Frame bereit fuer die Analyse?
    /// Aendert KEINEN Zustand - dafuer ist UpdateFrameReadiness zustaendig.
    /// </summary>
    private bool IsFrameReady() => _codingFrameReadiness.IsReady;

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
        _codingFrameReadiness.Update(result.TimestampSeconds, result.MeterReading.HasValue, fallbackTimestamp);
    }
}
