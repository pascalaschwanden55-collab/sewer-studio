using System;
using System.IO;
using System.Linq;

namespace AuswertungPro.Next.UI.Tests;

internal static class TestRepoPaths
{
    public static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "AuswertungPro.sln")))
                return current.FullName;

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Repository-Root mit AuswertungPro.sln wurde nicht gefunden.");
    }

    public static string FindRepoRoot()
        => FindRepositoryRoot();

    public static string RepoFile(params string[] segments)
        => Path.Combine(new[] { FindRepositoryRoot() }.Concat(segments).ToArray());
}
