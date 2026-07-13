using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.Infrastructure.Import.Ibak;

/// <summary>
/// Erstellt eine kurzlebige Arbeitskopie einer IBAK-Firebird-Datenbank.
/// Firebird darf dadurch interne Verwaltungsdaten aendern, ohne das Kundenoriginal anzufassen.
/// </summary>
internal sealed class IbakFdbWorkingCopy : IDisposable
{
    private readonly string _temporaryDirectory;
    private int _disposed;

    private IbakFdbWorkingCopy(string temporaryDirectory, string databasePath)
    {
        _temporaryDirectory = temporaryDirectory;
        DatabasePath = databasePath;
    }

    public string DatabasePath { get; }

    public static IbakFdbWorkingCopy Create(string sourcePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);

        var fullSourcePath = Path.GetFullPath(sourcePath);
        if (!File.Exists(fullSourcePath))
            throw new FileNotFoundException("IBAK-Firebird-Datenbank nicht gefunden.", fullSourcePath);

        var temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            "SewerStudio",
            "IbakFdb",
            Guid.NewGuid().ToString("N"));
        var copyPath = Path.Combine(temporaryDirectory, Path.GetFileName(fullSourcePath));

        try
        {
            Directory.CreateDirectory(temporaryDirectory);
            File.Copy(fullSourcePath, copyPath, overwrite: false);

            var attributes = File.GetAttributes(copyPath);
            if ((attributes & FileAttributes.ReadOnly) != 0)
                File.SetAttributes(copyPath, attributes & ~FileAttributes.ReadOnly);

            return new IbakFdbWorkingCopy(temporaryDirectory, copyPath);
        }
        catch
        {
            TryDeleteDirectory(temporaryDirectory);
            throw;
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        TryDeleteDirectory(_temporaryDirectory);
    }

    private static void TryDeleteDirectory(string path)
    {
        if (!Directory.Exists(path))
            return;

        BestEffort.Try(
            () => Directory.Delete(path, recursive: true),
            "[IBAK FDB] Temporaere Arbeitskopie konnte nicht geloescht werden");
    }
}
