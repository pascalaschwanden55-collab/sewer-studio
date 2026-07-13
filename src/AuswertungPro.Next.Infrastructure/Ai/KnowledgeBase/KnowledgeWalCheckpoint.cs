using System;
using System.IO;
using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;

/// <summary>Best-effort SQLite-WAL-Checkpoint vor dateibasierter Datensicherung.</summary>
public static class KnowledgeWalCheckpoint
{
    public static void TryCheckpoint()
    {
        try
        {
            var dbPath = KnowledgeBasePaths.GetKnowledgeDbPath();
            if (!File.Exists(dbPath))
                return;

            using var context = new KnowledgeBaseContext();
            using var command = context.Connection.CreateCommand();
            command.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
            command.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            BestEffort.ReportWarning($"[FullBackup] WAL-Checkpoint fehlgeschlagen: {ex.Message}");
        }
    }
}
