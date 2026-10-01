namespace AuswertungPro.Next.Infrastructure.Import;

/// <summary>
/// Schuetzt direkte Schreibziele innerhalb eines Projekts vor Pfadausbruechen
/// und vorhandenen Datei- oder Verzeichnis-Verknuepfungen.
/// </summary>
internal sealed class ProjectWritePathGuard
{
    private readonly ImportFileStagingPathGuard _stagingPathGuard;

    public ProjectWritePathGuard(string projectRoot)
    {
        _stagingPathGuard = new ImportFileStagingPathGuard(projectRoot);
    }

    public string EnsureSafeDirectoryTarget(string path)
        => EnsureSafeTarget(path);

    public string EnsureSafeFileTarget(string path)
        => EnsureSafeTarget(path);

    private string EnsureSafeTarget(string path)
        => _stagingPathGuard.EnsureSafeProjectPath(path, nameof(path));
}
