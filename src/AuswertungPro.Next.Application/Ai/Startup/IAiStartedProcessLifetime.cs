using System.Diagnostics;

namespace AuswertungPro.Next.Application.Ai.Startup;

/// <summary>
/// Verfolgt ausschliesslich KI-Prozesse, die SewerStudio selbst gestartet hat,
/// und beendet sie beim Programmende.
/// </summary>
public interface IAiStartedProcessLifetime
{
    bool TryTrack(Process process, out string? error);

    void StopAllStartedProcesses();
}
