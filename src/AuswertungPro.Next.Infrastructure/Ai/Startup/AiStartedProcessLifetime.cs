using System.Diagnostics;
using AuswertungPro.Next.Application.Ai.Startup;

namespace AuswertungPro.Next.Infrastructure.Ai.Startup;

/// <summary>
/// Kompatibilitaetsfassade fuer den zentral aufgebauten KI-Prozess-Lebenszyklus.
/// </summary>
public static class AiStartedProcessLifetime
{
    private static readonly IAiStartedProcessLifetime Default =
        new AiStartedProcessLifetimeService();

    public static IAiStartedProcessLifetime Current => Default;

    [Obsolete("Globaler Austausch wurde entfernt. Den Dienst per Konstruktor uebergeben.")]
    public static void Use(IAiStartedProcessLifetime lifetime)
        => throw new NotSupportedException(
            "Die globale KI-Prozessverwaltung kann nicht mehr ausgetauscht werden. " +
            "IAiStartedProcessLifetime bitte per Konstruktor uebergeben.");

    internal static bool TryTrack(Process process, out string? error)
        => Current.TryTrack(process, out error);

    /// <summary>Beendet alle von Sewer Studio selbst gestarteten KI-Prozesse samt Unterprozessen.</summary>
    public static void StopAllStartedProcesses()
        => Current.StopAllStartedProcesses();
}
