namespace AuswertungPro.Next.Application.Projects;

/// <summary>
/// Legt die verbindliche Ordnerstruktur eines SewerStudio-Projekts an.
/// </summary>
public interface IProjectStructureInitializer
{
    void EnsureCreated(string projectFolder);
}
