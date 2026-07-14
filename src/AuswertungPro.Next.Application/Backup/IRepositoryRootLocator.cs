namespace AuswertungPro.Next.Application.Backup;

/// <summary>
/// Sucht die Wurzel des SewerStudio-Quellcodeordners. Fehlt der Marker, wird nichts geliefert.
/// </summary>
public interface IRepositoryRootLocator
{
    string? Locate(string? startPath);
}
