using System;
using System.IO;
using System.Linq;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SystemMonitorProcessSafetyTests
{
    [Fact]
    public void SystemMonitorUsesSharedTimeoutProcessRunnerForExternalCommands()
    {
        var source = File.ReadAllText(FindRepoFile("src", "AuswertungPro.Next.UI", "Services", "SystemMonitorService.cs"));

        Assert.Contains("ExternalProcessRunner.RunAsync", source);
        Assert.DoesNotContain(".ReadToEnd()", source);
        Assert.DoesNotContain("WaitForExit(", source);
    }

    private static string FindRepoFile(params string[] relativeParts)
    {
        foreach (var start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory(), Path.GetDirectoryName(SourceFilePath())! }.Distinct())
        {
            var dir = new DirectoryInfo(start);
            while (dir is not null)
            {
                var candidate = Path.Combine(new[] { dir.FullName }.Concat(relativeParts).ToArray());
                if (File.Exists(candidate))
                    return candidate;
                dir = dir.Parent;
            }
        }

        throw new FileNotFoundException("Repo-Datei nicht gefunden.", Path.Combine(relativeParts));
    }

    private static string SourceFilePath([System.Runtime.CompilerServices.CallerFilePath] string sourceFilePath = "")
        => sourceFilePath;
}
