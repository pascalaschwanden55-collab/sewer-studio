using AuswertungPro.Next.Application.Backup;

namespace AuswertungPro.Next.Infrastructure.Backup;

/// <summary>Findet die Quellcodewurzel anhand der Datei AuswertungPro.sln.</summary>
public sealed class RepositoryRootFileLocator : IRepositoryRootLocator
{
    public string? Locate(string? startPath)
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
