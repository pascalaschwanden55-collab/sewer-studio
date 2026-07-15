using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Projects;

/// <summary>
/// Repariert veraltete Fotoverknuepfungen eines Projekts anhand der zentralen
/// Haltungsfoto-Ordner und meldet die Anzahl geaenderter Verknuepfungen.
/// </summary>
public interface IProjectPhotoReferenceNormalizer
{
    int Normalize(Project? project, string? projectFilePath);
}
