using System;
using System.IO;
using static AuswertungPro.Next.UI.Tests.SourceTextTestHelpers;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataPagePrintArchitectureTests
{
    [Fact]
    public void DataPageViewModel_delegiert_awu_haltungsprotokoll_druck_an_print_controller()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(root, "src", "AuswertungPro.Next.UI", "ViewModels", "Pages", "DataPageViewModel.cs"));

        var method = ExtractMethod(source, "private void PrintAwuHaltungsprotokollPdf(HaltungRecord? record)");

        Assert.Contains("_printController.PrintAwuHaltungsprotokollPdf(", method, StringComparison.Ordinal);
        Assert.DoesNotContain("BuildHaltungsprotokollPdf(", method, StringComparison.Ordinal);
        Assert.DoesNotContain("File.WriteAllBytes(", method, StringComparison.Ordinal);
        Assert.DoesNotContain("_sp.Dialogs.SaveFile(", method, StringComparison.Ordinal);
        Assert.DoesNotContain("Haltungsprotokoll_AWU_", method, StringComparison.Ordinal);
    }

    [Fact]
    public void DataPageViewModel_delegiert_hydraulik_pdf_druck_an_print_controller()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(root, "src", "AuswertungPro.Next.UI", "ViewModels", "Pages", "DataPageViewModel.cs"));

        var method = ExtractMethod(source, "private async void PrintHydraulikPdf(HaltungRecord? record)");

        Assert.Contains("_printController.PrintHydraulikPdfAsync(", method, StringComparison.Ordinal);
        Assert.DoesNotContain("new HydraulikPrintDialog(", method, StringComparison.Ordinal);
        Assert.DoesNotContain("HydraulikPdfBuilder.Build(", method, StringComparison.Ordinal);
        Assert.DoesNotContain("File.WriteAllBytes(", method, StringComparison.Ordinal);
        Assert.DoesNotContain("_sp.Dialogs.SaveFile(", method, StringComparison.Ordinal);
        Assert.DoesNotContain("Hydraulik_", method, StringComparison.Ordinal);
    }

    [Fact]
    public void DataPageViewModel_delegiert_dossier_pdf_druck_an_print_controller()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(root, "src", "AuswertungPro.Next.UI", "ViewModels", "Pages", "DataPageViewModel.cs"));

        var method = ExtractMethod(source, "private async void PrintDossierPdf(HaltungRecord? record)");

        Assert.Contains("_printController.PrintDossierPdfAsync(", method, StringComparison.Ordinal);
        Assert.DoesNotContain("new DossierPrintDialog(", method, StringComparison.Ordinal);
        Assert.DoesNotContain("HaltungsDossierPdfBuilder.Build(", method, StringComparison.Ordinal);
        Assert.DoesNotContain("PdfMergeHelper.", method, StringComparison.Ordinal);
        Assert.DoesNotContain("ProjectCostStoreRepository", method, StringComparison.Ordinal);
        Assert.DoesNotContain("DataPageProtocolPathResolver.ResolveOriginalPdfPaths", method, StringComparison.Ordinal);
        Assert.DoesNotContain("File.WriteAllBytes(", method, StringComparison.Ordinal);
        Assert.DoesNotContain("_sp.Dialogs.SaveFile(", method, StringComparison.Ordinal);
        Assert.DoesNotContain("Dossier_", method, StringComparison.Ordinal);
    }

}
