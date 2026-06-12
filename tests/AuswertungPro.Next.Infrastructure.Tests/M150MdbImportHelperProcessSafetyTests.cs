namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class M150MdbImportHelperProcessSafetyTests
{
    [Fact]
    public void M150MdbImportHelperUsesSharedTimeoutProcessRunner()
    {
        var source = File.ReadAllText(FindRepoFile("src", "AuswertungPro.Next.Infrastructure", "Import", "Xtf", "M150MdbImportHelper.cs"));

        Assert.Contains("ExternalProcessRunner.RunAsync", source);
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
