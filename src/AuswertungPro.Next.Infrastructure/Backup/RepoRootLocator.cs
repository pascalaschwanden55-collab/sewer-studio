using System;
using System.IO;

namespace AuswertungPro.Next.Infrastructure.Backup;

/// <summary>Findet die Repo-Wurzel anhand der AuswertungPro.sln.</summary>
public static class RepoRootLocator
{
    public static string? Locate()
        => Locate(AppContext.BaseDirectory);

    public static string? Locate(string? startPath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(startPath))
                return null;

            var fullStart = Path.GetFullPath(startPath);
            var directory = File.Exists(fullStart)
                ? Path.GetDirectoryName(fullStart)
                : fullStart;
            if (string.IsNullOrWhiteSpace(directory))
                return null;

            var current = new DirectoryInfo(directory);
            while (current is not null)
            {
                if (File.Exists(Path.Combine(current.FullName, "AuswertungPro.sln")))
                    return current.FullName;

                current = current.Parent;
            }
        }
        catch
        {
            return null;
        }

        return null;
    }
}
