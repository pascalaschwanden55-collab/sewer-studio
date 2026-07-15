namespace AuswertungPro.Next.Application.Backup;

/// <summary>
/// Prueft den Zielordner einer Vollsicherung und legt dessen Schutzmarker an.
/// </summary>
public interface IBackupTargetMarkerGuard
{
    /// <returns><see langword="null"/> bei Erfolg, sonst eine verstaendliche Fehlermeldung.</returns>
    string? ValidateAndCreateMarker(string backupRoot);
}
