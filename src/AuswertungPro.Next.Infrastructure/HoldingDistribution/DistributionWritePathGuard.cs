using AuswertungPro.Next.Infrastructure.Import;

namespace AuswertungPro.Next.Infrastructure.HoldingDistribution;

/// <summary>
/// Prueft alle direkten Ziele einer Verteilung gegen den ausdruecklich
/// gewaehlten Zielordner. Beim Schreiben sind der Zielordner selbst und alle
/// beruehrten Unterpfade fail-closed gegen Verknuepfungen gesperrt.
/// </summary>
internal sealed class DistributionWritePathGuard
{
    private readonly ProjectWritePathGuard _paths;

    public DistributionWritePathGuard(string selectedDestinationRoot)
    {
        _paths = new ProjectWritePathGuard(selectedDestinationRoot);
        _paths.EnsureSafeDirectoryTarget(selectedDestinationRoot);
    }

    public string EnsureDirectoryTarget(string path)
    {
        var safePath = _paths.EnsureSafeDirectoryTarget(path);
        if (!Directory.Exists(safePath))
            return safePath;

        // Einige Verteilwege suchen vor dem Kopieren nach vorhandenen Dateien.
        // Deshalb Links im unmittelbar gelesenen Zielordner vor dieser Suche sperren.
        foreach (var entry in Directory.EnumerateFileSystemEntries(safePath))
            _paths.EnsureSafeFileTarget(entry);

        return safePath;
    }

    public string EnsureFileTarget(string path)
        => _paths.EnsureSafeFileTarget(path);

    public string ResolveUniqueFileTarget(string preferredPath, bool overwrite)
    {
        var parent = Path.GetDirectoryName(Path.GetFullPath(preferredPath));
        if (!string.IsNullOrWhiteSpace(parent))
            EnsureDirectoryTarget(parent);

        _paths.EnsureSafeFileTarget(preferredPath);
        var resolved = DistributionFileTransfer.EnsureUniquePath(preferredPath, overwrite);
        return _paths.EnsureSafeFileTarget(resolved);
    }
}
