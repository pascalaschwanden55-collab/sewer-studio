using System;

namespace AuswertungPro.Next.UI.Services;

/// <summary>
/// Berechnet die CPU-Auslastung in Prozent aus aufeinanderfolgenden GetSystemTimes-Tickwerten.
/// Reine Mathematik, kein Systemzugriff, kein Zustand.
/// </summary>
public static class CpuDeltaCalculator
{
    /// <summary>
    /// Berechnet den CPU-Auslastungsprozentsatz aus der Differenz von Idle- und Gesamt-Ticks.
    /// </summary>
    /// <param name="deltaIdle">Anzahl Idle-Ticks seit letzter Messung.</param>
    /// <param name="deltaTotal">Anzahl Gesamt-Ticks (Kernel + User) seit letzter Messung.</param>
    /// <returns>CPU-Auslastung in Prozent (0–100), oder null wenn deltaTotal ≤ 0.</returns>
    public static int? ComputePercent(long deltaIdle, long deltaTotal)
    {
        if (deltaTotal <= 0)
            return null;

        return (int)Math.Round(100.0 * (deltaTotal - deltaIdle) / deltaTotal);
    }
}
