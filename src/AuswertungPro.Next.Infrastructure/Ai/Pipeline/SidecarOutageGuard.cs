namespace AuswertungPro.Next.Infrastructure.Ai.Pipeline;

/// <summary>
/// Einheitlicher Ausfallschutz des Multi-Model-Laufs (befund-2, erweitert):
/// Zaehlt aufeinanderfolgende FRAMES mit mindestens einem Transport-Level-
/// Sidecar-Fehler (HTTP unerreichbar, Timeout, 503 nach Retry) ueber die
/// YOLO-, DINO- und SAM-Aufrufe gemeinsam. Modell-Level-"kein Ergebnis"
/// (leere Antwort, degraded-Flag) zaehlt NICHT als Transportfehler.
/// Der implizite Reset ergibt sich aus den Frame-Indizes: liegt zwischen zwei
/// Fehler-Frames ein fehlerfreier Frame, beginnt die Serie neu.
/// Zusaetzlich wird die Zahl fehlerbedingt uebersprungener Frames fuer die
/// Skip-Quote (Unvollstaendigkeits-Kennzeichnung) gefuehrt.
/// </summary>
internal sealed class SidecarOutageGuard
{
    private readonly int _limit;
    private int _lastErrorFrameIndex;

    public SidecarOutageGuard(int limit) => _limit = limit;

    /// <summary>Aufeinanderfolgende Frames mit Transportfehler (Serie).</summary>
    public int ConsecutiveErrorFrames { get; private set; }

    /// <summary>Fehlerbedingt uebersprungene Frames (Transport- und Modellfehler).</summary>
    public int ErrorSkipCount { get; private set; }

    public bool LimitReached => ConsecutiveErrorFrames >= _limit;

    /// <summary>
    /// Transportfehler des aktuellen Frames melden (YOLO-/DINO-/SAM-Call warf).
    /// Ein Frame zaehlt auch bei mehreren fehlgeschlagenen Stufen nur einmal,
    /// weil der Frame-Loop nach dem ersten Fehler per continue weiterzieht.
    /// </summary>
    public void RegisterTransportError(int frameIndex)
    {
        if (frameIndex > _lastErrorFrameIndex)
        {
            ConsecutiveErrorFrames = frameIndex == _lastErrorFrameIndex + 1
                ? ConsecutiveErrorFrames + 1
                : 1;
            _lastErrorFrameIndex = frameIndex;
        }
        ErrorSkipCount++;
    }

    /// <summary>
    /// Fehlerbedingter Skip ohne Transportfehler (z. B. DINO degraded =
    /// Modellfehler): zaehlt fuer die Skip-Quote, aber NICHT fuer den Ausfall.
    /// </summary>
    public void RegisterFailureSkip() => ErrorSkipCount++;

    /// <summary>
    /// Setzt die Fehler-Serie nach einem erfolgreichen kontrollierten Neustart zurueck
    /// (Paket 3/A2). Die Skip-Quote (ErrorSkipCount) bleibt erhalten: die verlorenen
    /// Frames bleiben fuer die Unvollstaendigkeits-Kennzeichnung sichtbar.
    /// </summary>
    public void ResetSeries()
    {
        ConsecutiveErrorFrames = 0;
        _lastErrorFrameIndex = 0;
    }
}
