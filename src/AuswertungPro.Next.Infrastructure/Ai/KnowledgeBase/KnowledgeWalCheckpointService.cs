using System;
using System.IO;
using AuswertungPro.Next.Application.Backup;
using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;

/// <summary>
/// Führt vor einer Dateisicherung einen best-effort SQLite-WAL-Checkpoint aus.
/// </summary>
public sealed class KnowledgeWalCheckpointService : IKnowledgeWalCheckpoint
{
    private readonly string? _dbPath;
    private readonly Action<string> _executeCheckpoint;
    private readonly Action<string> _reportWarning;

    public KnowledgeWalCheckpointService(string? dbPath = null)
        : this(dbPath, ExecuteCheckpoint, message => BestEffort.ReportWarning(message))
    {
    }

    internal KnowledgeWalCheckpointService(
        string? dbPath,
        Action<string> executeCheckpoint,
        Action<string> reportWarning)
    {
        _dbPath = dbPath;
        _executeCheckpoint = executeCheckpoint ?? throw new ArgumentNullException(nameof(executeCheckpoint));
        _reportWarning = reportWarning ?? throw new ArgumentNullException(nameof(reportWarning));
    }

    public void TryCheckpoint()
    {
        try
        {
            var dbPath = _dbPath ?? KnowledgeBasePaths.GetKnowledgeDbPath();
            if (!File.Exists(dbPath))
                return;

            _executeCheckpoint(dbPath);
        }
        catch (Exception ex)
        {
            _reportWarning($"[FullBackup] WAL-Checkpoint fehlgeschlagen: {ex.Message}");
        }
    }

    private static void ExecuteCheckpoint(string dbPath)
    {
        using var context = new KnowledgeBaseContext(dbPath);
        using var command = context.Connection.CreateCommand();
        command.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
        command.ExecuteNonQuery();
    }
}
