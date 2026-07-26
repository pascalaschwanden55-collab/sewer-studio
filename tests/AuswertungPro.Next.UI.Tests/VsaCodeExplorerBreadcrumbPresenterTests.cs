using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Vsa;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Tests;

public sealed class VsaCodeExplorerBreadcrumbPresenterTests
{
    [Fact]
    public void Build_gibt_keine_elemente_fuer_leere_breadcrumbs_zurueck()
    {
        var presentation = VsaCodeExplorerBreadcrumbPresenter.Build([]);

        Assert.Empty(presentation.Elements);
    }

    [Fact]
    public void Build_fuegt_separatoren_zwischen_breadcrumbs_ein()
    {
        var presentation = VsaCodeExplorerBreadcrumbPresenter.Build(
            [
                new BreadcrumbItem("Start", 0),
                new BreadcrumbItem("B", 1),
                new BreadcrumbItem("BA", 2)
            ]);

        Assert.Equal(5, presentation.Elements.Count);
        Assert.Equal(
            [false, true, false, true, false],
            presentation.Elements.Select(element => element.IsSeparator));
        Assert.Equal(
            ["Start", "\u203A", "B", "\u203A", "BA"],
            presentation.Elements.Select(element => element.Text));
    }

    [Fact]
    public void Build_markiert_nur_letzten_breadcrumb_als_aktuell()
    {
        var presentation = VsaCodeExplorerBreadcrumbPresenter.Build(
            [
                new BreadcrumbItem("Start", 0),
                new BreadcrumbItem("B", 1)
            ]);

        var entries = presentation.Elements.Where(element => !element.IsSeparator).ToArray();

        Assert.Equal([0, 1], entries.Select(element => element.Level));
        Assert.Equal([true, false], entries.Select(element => element.CanNavigate));
        Assert.Equal([false, true], entries.Select(element => element.IsCurrent));
    }
}
