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

    [Fact]
    public void DataPage_context_menus_can_reveal_haltung_folder()
    {
        var root = FindRepositoryRoot();
        var pageXaml = File.ReadAllText(Path.Combine(root, "src", "AuswertungPro.Next.UI", "Views", "Pages", "DataPage.xaml"));
        var pageCode = File.ReadAllText(Path.Combine(root, "src", "AuswertungPro.Next.UI", "Views", "Pages", "DataPage.xaml.cs"));
        var ansichtXaml = File.ReadAllText(Path.Combine(root, "src", "AuswertungPro.Next.UI", "Views", "Pages", "Haltungsansicht", "HaltungsansichtView.xaml"));
        var ansichtCode = File.ReadAllText(Path.Combine(root, "src", "AuswertungPro.Next.UI", "Views", "Pages", "Haltungsansicht", "HaltungsansichtView.xaml.cs"));
        var viewModel = File.ReadAllText(Path.Combine(root, "src", "AuswertungPro.Next.UI", "ViewModels", "Pages", "DataPageViewModel.cs"));

        Assert.Contains("Header=\"Gehe zu Ordner\"", pageXaml, StringComparison.Ordinal);
        Assert.Contains("Click=\"OpenContainingFolderMenu_Click\"", pageXaml, StringComparison.Ordinal);
        Assert.Contains("Header=\"Gehe zu Ordner\"", ansichtXaml, StringComparison.Ordinal);
        Assert.Contains("CtxOpenFolder_Click", ansichtXaml, StringComparison.Ordinal);
        Assert.Contains("RaiseAction(\"openfolder\")", ansichtCode, StringComparison.Ordinal);
        Assert.Contains("case \"openfolder\": OpenContainingFolderMenu_Click(this, e); break;", pageCode, StringComparison.Ordinal);
        Assert.Contains("OpenContainingFolderCommand", viewModel, StringComparison.Ordinal);
        Assert.Contains("ExplorerRevealService.TryReveal", viewModel, StringComparison.Ordinal);
    }
}
