using System;
using System.IO;
using AuswertungPro.Next.Application.Backup;

namespace AuswertungPro.Next.Infrastructure.Backup;

/// <summary>
/// Liest den aktuellen Git-Commit direkt aus .git, ohne git.exe aufzurufen.
/// Fehler sind nicht kritisch fuer die Sicherung und liefern null.
/// </summary>
public sealed class GitCommitFileResolver : IGitCommitResolver
{
    public string? Resolve(string? repoRoot)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(repoRoot))
                return null;

            var gitDir = Path.Combine(repoRoot, ".git");
            var headPath = Path.Combine(gitDir, "HEAD");
            if (!File.Exists(headPath))
                return null;

            var head = File.ReadAllText(headPath).Trim();
            if (head.Length == 0)
                return null;

            const string refPrefix = "ref:";
            if (!head.StartsWith(refPrefix, StringComparison.OrdinalIgnoreCase))
                return head;

            var refName = head[refPrefix.Length..].Trim().Replace('/', Path.DirectorySeparatorChar);
            if (refName.Length == 0)
                return null;

            var refPath = Path.Combine(gitDir, refName);
            if (File.Exists(refPath))
            {
                var commit = File.ReadAllText(refPath).Trim();
                return commit.Length == 0 ? null : commit;
            }

            return ResolveFromPackedRefs(Path.Combine(gitDir, "packed-refs"), head[refPrefix.Length..].Trim());
        }
        catch
        {
            return null;
        }
    }

    private static string? ResolveFromPackedRefs(string packedRefsPath, string refName)
    {
        if (!File.Exists(packedRefsPath))
            return null;

        foreach (var line in File.ReadLines(packedRefsPath))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith("#", StringComparison.Ordinal))
                continue;

            var parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2 && string.Equals(parts[1], refName, StringComparison.Ordinal))
                return parts[0];
        }

        return null;
    }
}

/// <summary>
/// Kompatibilitaetsfassade fuer bestehende Aufrufer der Git-Commit-Erkennung.
/// </summary>
public static class GitCommitResolver
{
    public static IGitCommitResolver DefaultResolver { get; } =
        new GitCommitFileResolver();

    public static string? Resolve(string? repoRoot)
        => DefaultResolver.Resolve(repoRoot);
}
