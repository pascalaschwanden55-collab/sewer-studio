using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Reports;

/// <summary>
/// Prüft, ob ein Haltungsdossier mindestens ein vorhandenes Protokollfoto enthält.
/// </summary>
public interface IDossierPhotoAvailabilityService
{
    bool HasPrintablePhotos(HaltungRecord record, string projectFolder);
}
