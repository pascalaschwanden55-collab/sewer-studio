using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

namespace AuswertungPro.Next.Infrastructure.Tests;

internal static class TestPaths
{
    public static string FindSolutionRoot([CallerFilePath] string sourceFilePath = "")
    {
        foreach (var start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory(), Path.GetDirectoryName(sourceFilePath)! }.Distinct())
        {
            var dir = new DirectoryInfo(start);
            while (dir is not null)
            {
                var slnPath = Path.Combine(dir.FullName, "AuswertungPro.sln");
                if (File.Exists(slnPath))
                    return dir.FullName;
                dir = dir.Parent;
            }
        }

        throw new DirectoryNotFoundException("Solution root not found (AuswertungPro.sln).");
    }
}
