namespace AuswertungPro.Next.Infrastructure.Ai.Backup;

/// <summary>
/// Alle Speicherorte einer Wissenssicherung. Die explizite Uebergabe verhindert,
/// dass Tests oder Importlaeufe versehentlich andere Benutzerpfade verwenden.
/// </summary>
public sealed record KnowledgeBackupLocations(
    string KnowledgeRoot,
    string RoamingAuswertungPro,
    string RoamingSewerStudio,
    string LocalSewerStudio,
    string TrainingCenterStatePath,
    string TempRoot);
