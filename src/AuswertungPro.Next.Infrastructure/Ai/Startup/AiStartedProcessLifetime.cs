using System.Diagnostics;
using System.Threading;
using AuswertungPro.Next.Application.Ai.Startup;

namespace AuswertungPro.Next.Infrastructure.Ai.Startup;

/// <summary>
/// Kompatibilitaetsfassade fuer den zentral aufgebauten KI-Prozess-Lebenszyklus.
/// </summary>
public static class AiStartedProcessLifetime
{
    private static IAiStartedProcessLifetime _current = new AiStartedProcessLifetimeService();

    public static IAiStartedProcessLifetime Current => Volatile.Read(ref _current);

    public static void Use(IAiStartedProcessLifetime lifetime)
        => Volatile.Write(
            ref _current,
            lifetime ?? throw new ArgumentNullException(nameof(lifetime)));

    internal static bool TryTrack(Process process, out string? error)
        => Current.TryTrack(process, out error);

    /// <summary>Beendet alle von Sewer Studio selbst gestarteten KI-Prozesse samt Unterprozessen.</summary>
    public static void StopAllStartedProcesses()
        => Current.StopAllStartedProcesses();
}
