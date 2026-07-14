using AuswertungPro.Next.Application.Maintenance;

namespace AuswertungPro.Next.Infrastructure.Maintenance;

/// <summary>
/// Erkennt die Programmwurzel an der Solution-Datei. Fehlt sie, bleibt der App-Ordner aktiv.
/// </summary>
public sealed class ProgramRootFileLocator : IProgramRootLocator
{
    public string FindProgramRoot(string appBaseDirectory, string currentDirectory)
    {
        foreach (var start in new[] { appBaseDirectory, currentDirectory }
                     .Where(path => !string.IsNullOrWhiteSpace(path))
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var directory = new DirectoryInfo(Path.GetFullPath(start));
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "AuswertungPro.sln")))
                    return directory.FullName;

                directory = directory.Parent;
            }
        }

        return Path.GetFullPath(appBaseDirectory);
    }
}
