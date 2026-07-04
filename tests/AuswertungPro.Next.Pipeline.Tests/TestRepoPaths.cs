using System;
using System.IO;
using System.Linq;

namespace AuswertungPro.Next.Pipeline.Tests;

internal static class TestRepoPaths
{
    public static string FindRepositoryRoot()
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

    public static string RepoFile(params string[] segments)
        => Path.Combine(new[] { FindRepositoryRoot() }.Concat(segments).ToArray());

    private static IEnumerable<string> CandidateStarts()
    {
        yield return AppContext.BaseDirectory;
        yield return Directory.GetCurrentDirectory();
        yield return Path.GetDirectoryName(SourceFilePath())!;
    }

    private static string SourceFilePath([System.Runtime.CompilerServices.CallerFilePath] string sourceFilePath = "")
        => sourceFilePath;
}
