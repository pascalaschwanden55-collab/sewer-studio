using System.IO;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DesignAuditExportCancellationTests
{
    [Fact]
    public void Excel_busy_overlay_exposes_the_real_cancel_command()
    {
        var xaml = File.ReadAllText(
            RepoFile("src", "AuswertungPro.Next.UI", "Views", "Pages", "ExportPage.xaml"));
        var viewModel = File.ReadAllText(
            RepoFile("src", "AuswertungPro.Next.UI", "ViewModels", "Pages", "ExportPageViewModel.ExcelExport.cs"));

        Assert.Contains("CancelCommand=\"{Binding CancelExcelExportCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("cancellationToken: cancellation.Token", viewModel, StringComparison.Ordinal);
        Assert.Contains("catch (OperationCanceledException)", viewModel, StringComparison.Ordinal);
        Assert.Contains("Es wurde keine neue Datei veröffentlicht", viewModel, StringComparison.Ordinal);
    }

    [Fact]
    public void Blocking_validation_errors_use_one_dialog_instead_of_dialog_plus_toast()
    {
        var viewModel = File.ReadAllText(
            RepoFile("src", "AuswertungPro.Next.UI", "ViewModels", "Pages", "ExportPageViewModel.ExcelExport.cs"));

        Assert.DoesNotContain("_toasts.Error(\"Kostendaten sind nicht lesbar", viewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("_toasts.Error($\"{exportName}-Export fehlgeschlagen", viewModel, StringComparison.Ordinal);
    }
}
