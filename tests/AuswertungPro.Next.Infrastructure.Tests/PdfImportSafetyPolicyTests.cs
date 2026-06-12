using AuswertungPro.Next.Infrastructure.Import.Pdf;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class PdfImportSafetyPolicyTests
{
    [Fact]
    public void CheckFileBudget_RejectsFilesAboveConfiguredLimit()
    {
        var temp = Path.Combine(Path.GetTempPath(), $"pdf_budget_{Guid.NewGuid():N}.pdf");
        try
        {
            File.WriteAllBytes(temp, new byte[4]);

            var check = PdfImportSafetyPolicy.CheckFileBudget(temp, maxBytes: 3);

            Assert.False(check.Allowed);
            Assert.Contains("zu gross", check.Message);
        }
        finally
        {
            try { File.Delete(temp); } catch { }
        }
    }

    [Fact]
    public void CheckPageBudget_RejectsTooManyPages()
    {
        var check = PdfImportSafetyPolicy.CheckPageBudget(pageCount: 11, maxPages: 10);

        Assert.False(check.Allowed);
        Assert.Contains("zu viele Seiten", check.Message);
    }

    [Fact]
    public void CheckBudgets_AllowsValuesWithinLimits()
    {
        var temp = Path.Combine(Path.GetTempPath(), $"pdf_budget_{Guid.NewGuid():N}.pdf");
        try
        {
            File.WriteAllBytes(temp, new byte[4]);

            Assert.True(PdfImportSafetyPolicy.CheckFileBudget(temp, maxBytes: 4).Allowed);
            Assert.True(PdfImportSafetyPolicy.CheckPageBudget(pageCount: 10, maxPages: 10).Allowed);
        }
        finally
        {
            try { File.Delete(temp); } catch { }
        }
    }
}
