using AuswertungPro.Next.UI.ViewModels.Pages;

namespace AuswertungPro.Next.UI.Tests;

public sealed class BuilderPageExportScopeTests
{
    [Fact]
    public void Single_liefert_genau_die_eine_haltung_mit_namen_im_titel()
    {
        var row = Row("HAL-5");

        var selection = BuilderPageExportScope.Single(row);

        Assert.Same(row, Assert.Single(selection.Rows));
        Assert.Equal("Kostenzusammenstellung - Haltung HAL-5", selection.VariantTitle);
    }

    [Fact]
    public void Single_ohne_haltungsnamen_faellt_auf_neutralen_titel_zurueck()
    {
        var selection = BuilderPageExportScope.Single(Row("   "));

        Assert.Single(selection.Rows);
        Assert.Equal("Kostenzusammenstellung - einzelne Haltung", selection.VariantTitle);
    }

    [Fact]
    public void All_liefert_alle_zeilen_mit_anzahl_im_titel()
    {
        var rows = new[] { Row("A"), Row("B"), Row("C") };

        var selection = BuilderPageExportScope.All(rows);

        Assert.Equal(3, selection.Rows.Count);
        Assert.Equal("Gefilterte Kostenzusammenstellung (3 Haltungen)", selection.VariantTitle);
    }

    private static DruckcenterRowVm Row(string holding)
        => new() { Holding = holding };
}
