namespace AuswertungPro.Next.Infrastructure.Tests;

internal static class TestRepoPaths
{
    public static string RepoRoot()
    {
        foreach (var start in CandidateStarts().Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var dir = new DirectoryInfo(start);
            while (dir is not null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "AuswertungPro.sln")))
                    return dir.FullName;

                dir = dir.Parent;
            }
        }

        throw new DirectoryNotFoundException("Repository-Root mit AuswertungPro.sln nicht gefunden.");
    }

    public static string RepoFile(params string[] relativeParts)
    {
        var candidate = Path.Combine(new[] { RepoRoot() }.Concat(relativeParts).ToArray());
        if (File.Exists(candidate))
            return candidate;

        throw new FileNotFoundException("Repo-Datei nicht gefunden.", Path.Combine(relativeParts));
    }

    private static IEnumerable<string> CandidateStarts()
    {
        yield return AppContext.BaseDirectory;
        yield return Directory.GetCurrentDirectory();
        yield return Path.GetDirectoryName(SourceFilePath())!;
    }

    private static string SourceFilePath([System.Runtime.CompilerServices.CallerFilePath] string sourceFilePath = "")
        => sourceFilePath;
}
