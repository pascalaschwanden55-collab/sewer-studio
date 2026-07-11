using System;

namespace AuswertungPro.Next.UI.Controls.Animations;

/// <summary>
/// Verzoegerung fuer gestaffelte Einblend-Animationen: linear pro Index,
/// gedeckelt, damit lange Listen nicht minutenlang nachtroepfeln.
/// Pure Statik — testbar ohne WPF.
/// </summary>
public static class StaggerDelayPolicy
{
    /// <summary>Standard-Schritt zwischen zwei Elementen.</summary>
    public static readonly TimeSpan DefaultStep = TimeSpan.FromMilliseconds(40);

    /// <summary>Ab diesem Index starten alle weiteren Elemente gleichzeitig.</summary>
    public const int DefaultCap = 12;

    public static TimeSpan DelayFor(int index, TimeSpan? step = null, int cap = DefaultCap)
    {
        var effectiveIndex = Math.Clamp(index, 0, Math.Max(0, cap));
        var effectiveStep = step ?? DefaultStep;
        return TimeSpan.FromTicks(effectiveStep.Ticks * effectiveIndex);
    }
}
