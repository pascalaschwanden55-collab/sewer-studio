using System;
using System.Threading;
using System.Threading.Tasks;

namespace AuswertungPro.Next.Application.Ai;

/// <summary>
/// Pollt den KI-Pipeline-Zustand und meldet Aenderungen. Fuehrt keine UI-Aenderung aus.
/// </summary>
public interface IPipelineHealthMonitor : IAsyncDisposable
{
    /// <summary>Letzter ausgewerteter Zustand.</summary>
    PipelineHealthStatus CurrentStatus { get; }

    /// <summary>Wird gefeuert, wenn sich der Zustand aendert.</summary>
    event EventHandler<PipelineHealthStatus>? StatusChanged;

    /// <summary>Startet das periodische Polling (idempotent).</summary>
    void Start();

    /// <summary>Stoppt das Polling und wartet auf das Ende der Schleife.</summary>
    Task StopAsync();

    /// <summary>Fuehrt sofort eine einzelne Auswertung durch und liefert den Status.</summary>
    Task<PipelineHealthStatus> RefreshOnceAsync(CancellationToken ct = default);
}
