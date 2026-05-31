using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.Application.Projects;

public interface IProjectRepository
{
    Result<Project> Load(string path);
    Result Save(Project project, string path);

    /// <summary>
    /// Erstellt eine unabhaengige Tiefenkopie des Projekts (gleiche Serialisierung
    /// wie beim Speichern). Wird fuer Import-Vorschau/DryRun genutzt, damit der
    /// Import nicht das echte Live-Projekt veraendert.
    /// </summary>
    Project DeepCopy(Project source);
}
