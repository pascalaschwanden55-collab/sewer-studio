namespace AuswertungPro.Next.Application.Import;

/// <summary>
/// Erzeugt eine kurzlebige Datei-Sitzung fuer genau einen Importlauf.
/// Erst <see cref="IImportFileStagingSession.Publish"/> legt vorbereitete
/// Kopien an ihren endgueltigen Projektpfaden ab.
/// </summary>
public interface IImportFileStagingService
{
    IImportFileStagingSession? Begin(string? projectPath);
}

/// <summary>
/// Bereitet neue Projektdateien vor und kann sie bis zur bestaetigten
/// Projektuebernahme wieder entfernen. Bereits vorhandene Dateien werden
/// dabei nie als neue Dateien des Laufs behandelt.
/// </summary>
public interface IImportFileStagingSession : IDisposable
{
    string ProjectRoot { get; }

    /// <summary>
    /// Laufbezogener Arbeitsordner neben der aktiven Projektdatei. Die produktive
    /// Implementierung liefert den eigenen GUID-Unterordner; der Standard wahrt die
    /// Kompatibilitaet alter Implementierungen.
    /// </summary>
    string StagingRoot => System.IO.Path.Combine(ProjectRoot, ".import-staging");

    /// <summary>
    /// Vor <see cref="Publish"/> feststehende Ziele neu vorbereiteter Dateien.
    /// Sie werden als Write-ahead-Liste im Transaktionsmarker gespeichert, damit
    /// auch ein Prozessabbruch mitten in <see cref="Publish"/> ruecksetzbar bleibt.
    /// Alte Implementierungen fallen kompatibel auf <see cref="PublishedFiles"/> zurueck.
    /// </summary>
    IReadOnlyList<PublishedFileInfo> PreparedFiles => PublishedFiles;

    /// <summary>
    /// Die von <see cref="Publish"/> an ihre endgueltigen Ziele bewegten Dateien
    /// (Pfad relativ zum <see cref="ProjectRoot"/> + Inhalts-Hash). Vor Publish leer.
    /// Grundlage fuer den Transaktions-Marker und das Recovery-Rollback.
    /// </summary>
    IReadOnlyList<PublishedFileInfo> PublishedFiles { get; }

    string StageCopy(
        string sourcePath,
        string targetDirectory,
        Func<DateTime>? now = null,
        CancellationToken cancellationToken = default);

    void Publish();

    void Accept();
}
