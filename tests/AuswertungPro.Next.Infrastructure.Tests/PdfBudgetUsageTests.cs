namespace AuswertungPro.Next.Infrastructure.Tests;

using static TestRepoPaths;

public sealed class PdfBudgetUsageTests
{
    [Fact]
    public void CorePdfImportPathsUsePdfImportSafetyPolicy()
    {
        var textExtractor = File.ReadAllText(RepoFile("src", "AuswertungPro.Next.Infrastructure", "Import", "Pdf", "PdfTextExtractor.cs"));
        var protocolExtractor = File.ReadAllText(RepoFile("src", "AuswertungPro.Next.Infrastructure", "Ai", "Training", "Services", "PdfProtocolExtractor.cs"));
        var holdingParser = File.ReadAllText(RepoFile("src", "AuswertungPro.Next.Infrastructure", "HoldingFolderDistributor.PdfParsing.cs"));

        Assert.Contains("PdfImportSafetyPolicy.ThrowIfFileTooLarge", textExtractor);
        Assert.Contains("PdfImportSafetyPolicy.ThrowIfTooManyPages", textExtractor);
        Assert.Contains("PdfImportSafetyPolicy.ThrowIfFileTooLarge", protocolExtractor);
        Assert.Contains("PdfImportSafetyPolicy.ThrowIfTooManyPages", protocolExtractor);
        Assert.Contains("PdfImportSafetyPolicy.CheckFileBudget(path)", protocolExtractor);
        Assert.Contains("PdfImportSafetyPolicy.CheckPageBudget(pageCount, OcrFallbackMaxPages)", protocolExtractor);
        Assert.Contains("PdfImportSafetyPolicy.ThrowIfFileTooLarge", holdingParser);
        Assert.Contains("PdfImportSafetyPolicy.ThrowIfTooManyPages", holdingParser);
    }
}
