namespace AuswertungPro.Next.Application.Backup;

/// <summary>
/// Liest den aktuellen Commit eines lokalen Git-Projektordners.
/// </summary>
public interface IGitCommitResolver
{
    string? Resolve(string? repoRoot);
}
