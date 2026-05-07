using System;

namespace AuswertungPro.Next.Application.Maintenance;

/// <summary>
/// Persistierter Zustand des MaintenanceScheduler. Wird in
/// <c>{LocalAppData}/SewerStudio/maintenance.json</c> abgelegt.
/// </summary>
public sealed record MaintenanceState
{
    /// <summary>Letzter erfolgreicher Frame-Cleanup-Lauf (UTC). Null = nie.</summary>
    public DateTime? LastFrameCleanupUtc { get; init; }

    /// <summary>Letzter erfolgreicher Versions-Pruning-Lauf (UTC). Null = nie.</summary>
    public DateTime? LastVersionsPruneUtc { get; init; }

    /// <summary>Anzahl der bisher geloeschten Frame-PNGs (kumuliert).</summary>
    public long TotalFramesDeleted { get; init; }

    /// <summary>Anzahl der bisher bereinigten Versions-Snapshots (kumuliert).</summary>
    public long TotalVersionsPruned { get; init; }
}
