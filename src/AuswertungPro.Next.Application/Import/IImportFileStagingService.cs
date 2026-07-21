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

    string StageCopy(
        string sourcePath,
        string targetDirectory,
        Func<DateTime>? now = null,
        CancellationToken cancellationToken = default);

    void Publish();

    void Accept();
}
