using System;
using System.IO;
using static AuswertungPro.Next.UI.Tests.SourceTextTestHelpers;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataPageOriginalPdfArchitectureTests
{
    [Fact]
    public void DataPageViewModel_delegiert_original_pdf_oeffnen_an_controller()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(root, "src", "AuswertungPro.Next.UI", "ViewModels", "Pages", "DataPageViewModel.cs"));

        var method = ExtractMethodBody(source, "private void OpenOriginalPdf(HaltungRecord? record)");

        Assert.Contains("_originalPdfController.Open(", method, StringComparison.Ordinal);
        Assert.DoesNotContain("SafeShellOpen.TryOpen", method, StringComparison.Ordinal);
        Assert.DoesNotContain("DataPageProtocolPathResolver.ResolveOriginalPdfPaths", method, StringComparison.Ordinal);
        Assert.DoesNotContain("_sp.Dialogs.Info(", method, StringComparison.Ordinal);
        Assert.DoesNotContain("_sp.Dialogs.Warn(", method, StringComparison.Ordinal);
    }

}
