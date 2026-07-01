using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class FieldKeysTests
{
    [Fact]
    public void Core_field_keys_match_persisted_project_field_names()
    {
        Assert.Equal("Haltungsname", FieldKeys.HoldingName);
        Assert.Equal("Link", FieldKeys.Link);
        Assert.Equal("PDF_Path", FieldKeys.PdfPath);
        Assert.Equal("PDF_Eigen", FieldKeys.PdfEigen);
        Assert.Equal("PDF_All", FieldKeys.PdfAll);
    }
}
