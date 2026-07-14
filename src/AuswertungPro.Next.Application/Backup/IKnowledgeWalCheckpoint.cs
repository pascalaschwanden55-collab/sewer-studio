namespace AuswertungPro.Next.Application.Backup;

/// <summary>
/// Schreibt offene SQLite-WAL-Daten vor einer dateibasierten Sicherung zurück.
/// </summary>
public interface IKnowledgeWalCheckpoint
{
    void TryCheckpoint();
}
