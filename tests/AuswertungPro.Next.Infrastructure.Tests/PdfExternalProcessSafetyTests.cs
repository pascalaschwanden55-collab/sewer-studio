namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class PdfExternalProcessSafetyTests
{
    [Fact]
    public void PdfExtractorsUseSharedAsyncTimeoutProcessRunner()
    {
        var textExtractor = File.ReadAllText(FindRepoFile("src", "AuswertungPro.Next.Infrastructure", "Import", "Pdf", "PdfTextExtractor.cs"));
        var ocrExtractor = File.ReadAllText(FindRepoFile("src", "AuswertungPro.Next.Infrastructure", "Import", "Pdf", "PdfOcrExtractor.cs"));

        Assert.Contains("ExternalProcessRunner.RunAsync", textExtractor);
        Assert.Contains("ExternalProcessRunner.RunAsync", ocrExtractor);
        Assert.DoesNotContain(".ReadToEnd()", textExtractor);
        Assert.DoesNotContain(".ReadToEnd()", ocrExtractor);
        Assert.DoesNotContain("WaitForExit()", textExtractor);
        Assert.DoesNotContain("WaitForExit(timeoutMs)", ocrExtractor);
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
