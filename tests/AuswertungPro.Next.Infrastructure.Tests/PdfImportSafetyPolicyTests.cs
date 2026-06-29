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

    [Fact]
    public void ResolveMaxBytes_HonorsOverrideAboveDefault_FallsBackOnInvalid()
    {
        // Grosse GEP-/SchachtPro-Gesamtauszuege (z.B. ~934 MB mit Vollbild-Fotos) duerfen mit
        // Override durch, ohne den vorsorglichen 256-MB-Default abzusenken.
        Assert.Equal(2048L * 1024 * 1024, PdfImportSafetyPolicy.ResolveMaxBytes("2048"));
        Assert.Equal(PdfImportSafetyPolicy.DefaultMaxPdfBytes, PdfImportSafetyPolicy.ResolveMaxBytes(null));
        Assert.Equal(PdfImportSafetyPolicy.DefaultMaxPdfBytes, PdfImportSafetyPolicy.ResolveMaxBytes(""));
        Assert.Equal(PdfImportSafetyPolicy.DefaultMaxPdfBytes, PdfImportSafetyPolicy.ResolveMaxBytes("0"));
        Assert.Equal(PdfImportSafetyPolicy.DefaultMaxPdfBytes, PdfImportSafetyPolicy.ResolveMaxBytes("-5"));
        Assert.Equal(PdfImportSafetyPolicy.DefaultMaxPdfBytes, PdfImportSafetyPolicy.ResolveMaxBytes("abc"));
    }

    [Fact]
    public void ResolveMaxPages_HonorsOverrideAboveDefault_FallsBackOnInvalid()
    {
        Assert.Equal(5000, PdfImportSafetyPolicy.ResolveMaxPages("5000"));
        Assert.Equal(PdfImportSafetyPolicy.DefaultMaxPages, PdfImportSafetyPolicy.ResolveMaxPages(null));
        Assert.Equal(PdfImportSafetyPolicy.DefaultMaxPages, PdfImportSafetyPolicy.ResolveMaxPages("0"));
        Assert.Equal(PdfImportSafetyPolicy.DefaultMaxPages, PdfImportSafetyPolicy.ResolveMaxPages("nope"));
    }

    [Fact]
    public void CheckFileBudget_WithoutExplicitLimit_UsesResolvedDefault()
    {
        // Ohne Override greift der 256-MB-Default: eine 5-MB-Datei ist erlaubt.
        var temp = Path.Combine(Path.GetTempPath(), $"pdf_budget_{Guid.NewGuid():N}.pdf");
        try
        {
            File.WriteAllBytes(temp, new byte[5 * 1024 * 1024]);
            Assert.True(PdfImportSafetyPolicy.CheckFileBudget(temp).Allowed);
        }
        finally
        {
            try { File.Delete(temp); } catch { }
        }
    }
}
