using System.IO;

namespace AuswertungPro.Next.Infrastructure.Tests;

internal static class TestPaths
{
    public static string FindSolutionRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var slnPath = Path.Combine(dir.FullName, "AuswertungPro.sln");
            if (File.Exists(slnPath))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Solution root not found (AuswertungPro.sln).");
    }
}
