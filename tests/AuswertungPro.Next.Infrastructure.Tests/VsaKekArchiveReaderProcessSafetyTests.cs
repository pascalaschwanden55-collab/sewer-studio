namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class VsaKekArchiveReaderProcessSafetyTests
{
    [Fact]
    public void ArchiveReaderUsesSharedTimeoutProcessRunner()
    {
        var source = File.ReadAllText(FindRepoFile("src", "AuswertungPro.Next.Application", "Protocol", "VsaKekCatalogBuilder.cs"));
        var readerStart = source.IndexOf("public static class VsaKekCatalogArchiveReader", StringComparison.Ordinal);
        Assert.True(readerStart >= 0);
        var readerSource = source[readerStart..];

        Assert.Contains("ExternalProcessRunner.RunAsync", readerSource);
        Assert.DoesNotContain(".ReadToEnd()", readerSource);
        Assert.DoesNotContain("WaitForExit()", readerSource);
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
