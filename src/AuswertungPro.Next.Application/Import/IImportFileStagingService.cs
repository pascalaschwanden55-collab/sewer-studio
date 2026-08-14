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
/// Logisches Ziel und aktuell lesbarer Pfad einer Importdatei. Vor der
/// Veroeffentlichung zeigt <see cref="ReadPath"/> in den Arbeitsordner, waehrend
/// <see cref="TargetPath"/> bereits den spaeteren Projektpfad enthaelt.
/// </summary>
public sealed record ImportReadableFile(string TargetPath, string ReadPath);

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

    /// <summary>
    /// Wie <see cref="StageCopy"/>, aber mit einem vom physischen Lesepfad
    /// unabhaengigen logischen Dateinamen. Das wird benoetigt, wenn die Quelle
    /// selbst bereits im Arbeitsordner liegt.
    /// </summary>
    string StageCopyAs(
        string sourcePath,
        string targetDirectory,
        string targetFileName,
        Func<DateTime>? now = null,
        CancellationToken cancellationToken = default)
    {
        if (!Path.GetFileName(sourcePath).Equals(targetFileName, StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException(
                "Diese Datei-Staging-Implementierung unterstuetzt keine abweichenden logischen Dateinamen.");
        }

        return StageCopy(sourcePath, targetDirectory, now, cancellationToken);
    }

    /// <summary>
    /// Liefert fuer ein logisches Ziel die aktuell lesbare Datei. Dadurch koennen
    /// spaetere Importschritte vorbereitete Dateien lesen, ohne sie vorzeitig zu
    /// veroeffentlichen.
    /// </summary>
    string ResolveReadPath(string targetPath) => targetPath;

    /// <summary>
    /// Vereint vorhandene Projektdateien und vorbereitete Dateien zu einer Lesesicht.
    /// Die Zielpfade bleiben dabei logisch stabil.
    /// </summary>
    IReadOnlyList<ImportReadableFile> EnumerateReadableFiles(
        string targetDirectory,
        string searchPattern,
        SearchOption searchOption)
    {
        if (!Directory.Exists(targetDirectory))
            return Array.Empty<ImportReadableFile>();

        return Directory.EnumerateFiles(targetDirectory, searchPattern, searchOption)
            .Select(path => new ImportReadableFile(path, path))
            .ToList();
    }

    /// <summary>
    /// Laesst einen Erzeuger direkt in eine vorbereitete Datei schreiben. Das
    /// bevorzugte Ziel liegt im Projekt, wird aber erst durch <see cref="Publish"/>
    /// sichtbar. Bei einer Namenskollision bleibt das vorhandene Original erhalten.
    /// </summary>
    string StageGeneratedFile(
        string preferredTargetPath,
        Action<string> writeStageFile,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException(
            "Diese Datei-Staging-Implementierung unterstuetzt keine erzeugten Dateien.");

    void Publish();

    void Accept();
}
