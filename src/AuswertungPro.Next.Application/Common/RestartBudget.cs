using System;
using System.Collections.Generic;
using System.Linq;

namespace AuswertungPro.Next.Application.Common;

/// <summary>
/// Sliding-Window-Budget fuer automatische Restart-Versuche (Watchdog-Pattern).
/// Verhindert Crash-Loops, indem nur eine begrenzte Anzahl Restarts in einem
/// Zeitfenster erlaubt wird. Reine Logik, kein I/O — direkt unit-testbar.
///
/// Beispiel: Sidecar-Watchdog erlaubt 3 Restarts in 5 Min; bei der 4. Crash
/// in dem Fenster gibt der Watchdog auf und meldet im Log.
/// </summary>
public sealed class RestartBudget
{
    private readonly Queue<DateTime> _timestamps = new();

    public int MaxRestartsPerWindow { get; set; } = 3;
    public TimeSpan Window { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Versucht einen Restart-Slot zu konsumieren. Liefert true wenn noch Budget
    /// vorhanden war (und merkt sich den Restart). Liefert false wenn das Limit
    /// erreicht ist — der Aufrufer soll dann aufgeben statt erneut zu probieren.
    /// </summary>
    public bool TryConsume(DateTime nowUtc)
    {
        SlideWindow(nowUtc);
        if (_timestamps.Count >= MaxRestartsPerWindow) return false;
        _timestamps.Enqueue(nowUtc);
        return true;
    }

    /// <summary>Anzahl der Restarts im aktuellen Fenster (zu Diagnose-/Log-Zwecken).</summary>
    public int RecentCount(DateTime nowUtc)
    {
        SlideWindow(nowUtc);
        return _timestamps.Count;
    }

    /// <summary>
    /// Berechnet einen Backoff fuer den naechsten Versuch: 10 s pro bisherigem
    /// Versuch, gedeckelt auf 60 s. Erster Versuch = 10 s, vierter = 40 s.
    /// </summary>
    public TimeSpan ComputeBackoff(DateTime nowUtc)
    {
        SlideWindow(nowUtc);
        var seconds = Math.Min(60, 10 * (_timestamps.Count + 1));
        return TimeSpan.FromSeconds(seconds);
    }

    /// <summary>Setzt das Budget zurueck (z.B. bei manuellem Eingriff).</summary>
    public void Reset() => _timestamps.Clear();

    private void SlideWindow(DateTime nowUtc)
    {
        var cutoff = nowUtc - Window;
        while (_timestamps.Count > 0 && _timestamps.Peek() < cutoff)
            _timestamps.Dequeue();
    }
}
