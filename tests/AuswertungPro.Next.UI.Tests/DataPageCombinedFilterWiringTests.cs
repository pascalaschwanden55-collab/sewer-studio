using System.IO;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataPageCombinedFilterWiringTests
{
    [Fact]
    public void Dashboardfilter_ist_sichtbar_und_hat_eigenen_resetweg()
    {
        var controlXaml = File.ReadAllText(RepoFile(
            "src", "AuswertungPro.Next.UI", "Controls", "FilterChipBar.xaml"));
        var controlCode = File.ReadAllText(RepoFile(
            "src", "AuswertungPro.Next.UI", "Controls", "FilterChipBar.xaml.cs"));
        var pageCode = File.ReadAllText(RepoFile(
            "src", "AuswertungPro.Next.UI", "Views", "Pages", "DataPage.xaml.cs"));
        var interactions = File.ReadAllText(RepoFile(
            "src", "AuswertungPro.Next.UI", "Views", "Pages", "DataPage.RecordInteractions.cs"));

        Assert.Contains("x:Name=\"StartFilterResetButton\"", controlXaml);
        Assert.Contains("Click=\"StartFilterReset_Click\"", controlXaml);
        Assert.Contains("Dashboard: {filter.DisplayText}", controlCode);
        Assert.Contains("StartFilterZurueckgesetzt?.Invoke();", controlCode);
        Assert.Contains("FilterChips.StartFilterZurueckgesetzt += EntferneStartFilter;", pageCode);
        Assert.Contains(".WithoutStartFilter();", interactions);
        Assert.Contains("FilterChips.SetStartFilter(null);", interactions);
        Assert.Contains("if (_startFilterApplied || DataContext is not DataPageViewModel vm)", interactions);
        Assert.Contains("_startFilterApplied = true;", interactions);
    }

    [Fact]
    public void Suche_chips_und_dashboard_verwenden_dieselbe_grid_filterstelle()
    {
        var pageCode = File.ReadAllText(RepoFile(
            "src", "AuswertungPro.Next.UI", "Views", "Pages", "DataPage.xaml.cs"));
        var interactions = File.ReadAllText(RepoFile(
            "src", "AuswertungPro.Next.UI", "Views", "Pages", "DataPage.RecordInteractions.cs"));

        Assert.Contains("ApplyCombinedFilter(vm);", pageCode);
        Assert.Contains("ApplyStartFilter();", pageCode);
        Assert.Contains("ApplyCombinedFilter(vm);", interactions);
        Assert.Contains("DataGridSearchFilterController.ApplyFilter(", interactions);
        Assert.DoesNotContain("view.Filter =", interactions, StringComparison.Ordinal);
        Assert.Contains("Grid.AllowDrop = !_combinedFilter.IstAktiv;", interactions);
    }

    [Fact]
    public void Nur_der_gemeinsame_Filterdienst_darf_die_Gridsicht_filtern()
    {
        // Der Anzeigefehler entstand, weil drei Stellen unabhaengig auf dasselbe
        // view.Filter schrieben und sich gegenseitig loeschten. Ausser dem
        // gemeinsamen Dienst darf das keine Seite mehr selbst tun.
        foreach (var datei in new[]
                 {
                     "DataPage.xaml.cs",
                     "DataPage.RecordInteractions.cs",
                     "SchaechtePage.xaml.cs",
                 })
        {
            var quelle = File.ReadAllText(RepoFile(
                "src", "AuswertungPro.Next.UI", "Views", "Pages", datei));

            Assert.DoesNotContain("view.Filter =", quelle, StringComparison.Ordinal);
            Assert.DoesNotContain(".Filter = obj =>", quelle, StringComparison.Ordinal);
            Assert.DoesNotContain(".Filter = null", quelle, StringComparison.Ordinal);
        }
    }
}
