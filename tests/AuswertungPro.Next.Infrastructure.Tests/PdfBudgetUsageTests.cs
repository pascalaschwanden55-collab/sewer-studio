namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class PdfBudgetUsageTests
{
    [Fact]
    public void CorePdfImportPathsUsePdfImportSafetyPolicy()
    {
        var textExtractor = File.ReadAllText(FindRepoFile("src", "AuswertungPro.Next.Infrastructure", "Import", "Pdf", "PdfTextExtractor.cs"));
        var protocolExtractor = File.ReadAllText(FindRepoFile("src", "AuswertungPro.Next.Infrastructure", "Ai", "Training", "Services", "PdfProtocolExtractor.cs"));
        var holdingParser = File.ReadAllText(FindRepoFile("src", "AuswertungPro.Next.Infrastructure", "HoldingFolderDistributor.PdfParsing.cs"));

        Assert.Contains("PdfImportSafetyPolicy.ThrowIfFileTooLarge", textExtractor);
        Assert.Contains("PdfImportSafetyPolicy.ThrowIfTooManyPages", textExtractor);
        Assert.Contains("PdfImportSafetyPolicy.ThrowIfFileTooLarge", protocolExtractor);
        Assert.Contains("PdfImportSafetyPolicy.ThrowIfTooManyPages", protocolExtractor);
        Assert.Contains("PdfImportSafetyPolicy.CheckFileBudget(path)", protocolExtractor);
        Assert.Contains("PdfImportSafetyPolicy.CheckPageBudget(pageCount, OcrFallbackMaxPages)", protocolExtractor);
        Assert.Contains("PdfImportSafetyPolicy.ThrowIfFileTooLarge", holdingParser);
        Assert.Contains("PdfImportSafetyPolicy.ThrowIfTooManyPages", holdingParser);
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
