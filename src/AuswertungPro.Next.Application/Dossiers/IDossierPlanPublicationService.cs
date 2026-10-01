using System;

namespace AuswertungPro.Next.Application.Dossiers;

/// <summary>Ergebnis des Rueckbaus einer neu veroeffentlichten Plandatei.</summary>
public sealed record DossierPlanRollbackResult(bool Success, string? Error)
{
    public static DossierPlanRollbackResult Ok() => new(true, null);

    public static DossierPlanRollbackResult Failed(string error) => new(false, error);
}

/// <summary>
/// Beleg fuer genau eine neu erzeugte Plandatei. Bis <see cref="Accept"/> darf
/// sie bei einem spaeteren Speicherfehler wieder sicher entfernt werden.
/// </summary>
public interface IDossierPlanPublication : IDisposable
{
    string PublishedPath { get; }

    /// <summary>Die Dossier-Datei verweist dauerhaft auf den Plan.</summary>
    void Accept();

    /// <summary>
    /// Entfernt nur die von dieser Veroeffentlichung erzeugte, seitdem
    /// unveraenderte Datei.
    /// </summary>
    DossierPlanRollbackResult Rollback();
}

/// <summary>Ergebnis einer sicheren Planveroeffentlichung.</summary>
public sealed record DossierPlanPublicationResult(
    string? ImagePath,
    string? Error,
    IDossierPlanPublication? Publication)
{
    public bool Success => ImagePath is not null;

    public static DossierPlanPublicationResult Existing(string path)
        => new(path, null, null);

    public static DossierPlanPublicationResult Published(
        string path,
        IDossierPlanPublication publication)
        => new(path, null, publication);

    public static DossierPlanPublicationResult Failed(string error)
        => new(null, error, null);
}

/// <summary>
/// Kopiert einen fertig bearbeiteten Plan unter einen freien Namen innerhalb
/// des aktuellen Projekts. Vorhandene Dateien werden nie ersetzt.
/// </summary>
public interface IDossierPlanPublicationService
{
    DossierPlanPublicationResult Publish(
        string projectRoot,
        string sourcePath,
        string targetFolder);
}
