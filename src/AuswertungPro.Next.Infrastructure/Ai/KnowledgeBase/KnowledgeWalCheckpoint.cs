using AuswertungPro.Next.Application.Backup;

namespace AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;

/// <summary>Best-effort SQLite-WAL-Checkpoint vor dateibasierter Datensicherung.</summary>
public static class KnowledgeWalCheckpoint
{
    private static readonly IKnowledgeWalCheckpoint Default = new KnowledgeWalCheckpointService();

    public static void TryCheckpoint()
        => Default.TryCheckpoint();
}
