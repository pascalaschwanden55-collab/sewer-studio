using AuswertungPro.Next.Application.Import;

namespace AuswertungPro.Next.Infrastructure.Import;

/// <summary>
/// Laesst bestehende Verteiler in einen kurzlebigen Systemordner schreiben und
/// uebernimmt deren Ergebnis anschliessend in die gemeinsame Import-Transaktion.
/// So bleiben die bewaehrten PDF-Splitter unveraendert, ohne vorzeitig im Projekt
/// sichtbar zu werden.
/// </summary>
internal sealed class StagedDistributionOutput : IDisposable
{
    private readonly string _tempParent;
    private readonly Dictionary<string, string> _logicalPathByTempPath =
        new(StringComparer.OrdinalIgnoreCase);

    public StagedDistributionOutput()
    {
        _tempParent = Path.GetFullPath(Path.GetTempPath())
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        WorkRoot = Path.Combine(
            _tempParent,
            "sewerstudio-distribution-" + Guid.NewGuid().ToString("N"));
        OutputRoot = Path.Combine(WorkRoot, "output");
        Directory.CreateDirectory(OutputRoot);
    }

    public string WorkRoot { get; }

    public string OutputRoot { get; }

    public string CreateReadableCopy(string readPath, string logicalFileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(readPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(logicalFileName);
        var safeName = Path.GetFileName(logicalFileName);
        if (!safeName.Equals(logicalFileName, StringComparison.Ordinal))
            throw new ArgumentException("Logischer Dateiname ist ungueltig.", nameof(logicalFileName));

        var inputRoot = Path.Combine(WorkRoot, "input");
        Directory.CreateDirectory(inputRoot);
        var destination = Path.Combine(inputRoot, safeName);
        File.Copy(readPath, destination, overwrite: false);
        return destination;
    }

    public IReadOnlyDictionary<string, string> StageAll(
        IImportFileStagingSession fileStaging,
        string logicalRoot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fileStaging);
        ArgumentException.ThrowIfNullOrWhiteSpace(logicalRoot);

        foreach (var tempPath in Directory.EnumerateFiles(
                     OutputRoot,
                     "*",
                     SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relative = Path.GetRelativePath(OutputRoot, tempPath);
            var preferredTarget = Path.Combine(logicalRoot, relative);
            var targetDirectory = Path.GetDirectoryName(preferredTarget)
                                  ?? throw new IOException(
                                      $"Verteilziel hat keinen Ordner: {preferredTarget}");
            var logicalPath = fileStaging.StageCopyAs(
                tempPath,
                targetDirectory,
                Path.GetFileName(preferredTarget),
                cancellationToken: cancellationToken);
            _logicalPathByTempPath[Path.GetFullPath(tempPath)] = logicalPath;
        }

        return _logicalPathByTempPath;
    }

    public string? MapPath(string? tempPath, string logicalRoot)
    {
        if (string.IsNullOrWhiteSpace(tempPath))
            return tempPath;

        var fullPath = Path.GetFullPath(tempPath);
        if (_logicalPathByTempPath.TryGetValue(fullPath, out var logicalFile))
            return logicalFile;

        var relative = Path.GetRelativePath(OutputRoot, fullPath);
        if (relative.Equals("..", StringComparison.Ordinal)
            || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            || Path.IsPathRooted(relative))
        {
            return tempPath;
        }

        return Path.Combine(logicalRoot, relative);
    }

    public void Dispose()
    {
        if (!Directory.Exists(WorkRoot))
            return;

        var fullRoot = Path.GetFullPath(WorkRoot);
        var parent = Path.GetDirectoryName(fullRoot)?.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);
        if (!string.Equals(parent, _tempParent, StringComparison.OrdinalIgnoreCase)
            || !Path.GetFileName(fullRoot).StartsWith(
                "sewerstudio-distribution-",
                StringComparison.Ordinal))
        {
            throw new IOException(
                $"Unsicherer Verteil-Arbeitsordner wird nicht entfernt: {fullRoot}");
        }

        Directory.Delete(fullRoot, recursive: true);
    }
}
