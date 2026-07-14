namespace AuswertungPro.Next.Infrastructure.Import;

/// <summary>
/// Ergebnis eines Archive-Aufrufs.
/// </summary>
/// <param name="Copied">Anzahl der neu kopierten Dateien.</param>
/// <param name="Reused">Anzahl der unveraendert schon vorhandenen Dateien.</param>
/// <param name="Messages">Hinweise, zum Beispiel bei Namenskollisionen.</param>
public sealed record ArchiveResult(int Copied, int Reused, IReadOnlyList<string> Messages);

/// <summary>
/// Kompatibilitaetsfassade fuer bestehende Aufrufer. Neue Aufrufer erhalten
/// <see cref="IImportSourceArchiver"/> zentral als Instanz.
/// </summary>
public static class ImportSourceArchiver
{
    private static readonly IImportSourceArchiver DefaultService = new ImportSourceArchiveService();

    public static ArchiveResult Archive(string sourceFolder, string projectFolder)
        => DefaultService.Archive(sourceFolder, projectFolder);
}
