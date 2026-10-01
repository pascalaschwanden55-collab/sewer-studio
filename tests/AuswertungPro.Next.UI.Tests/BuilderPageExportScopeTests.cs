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

    /// <summary>
    /// Fallback ohne Namen. Der Text lautet seit der Trennung Haltungen/Schaechte
    /// "&lt;Bauteilart&gt; ohne Bezeichnung" — das benennt den Grund und funktioniert
    /// grammatikalisch fuer beide Bauteilarten.
    /// </summary>
    [Fact]
    public void Single_ohne_haltungsnamen_faellt_auf_neutralen_titel_zurueck()
    {
        var selection = BuilderPageExportScope.Single(Row("   "));

        Assert.Single(selection.Rows);
        Assert.Equal("Kostenzusammenstellung - Haltung ohne Bezeichnung", selection.VariantTitle);
    }

    [Fact]
    public void All_liefert_alle_zeilen_mit_anzahl_im_titel()
    {
        var rows = new[] { Row("A"), Row("B"), Row("C") };

        var selection = BuilderPageExportScope.All(rows);

        Assert.Equal(3, selection.Rows.Count);
        Assert.Equal("Gefilterte Kostenzusammenstellung (3 Haltungen)", selection.VariantTitle);
    }

    /// <summary>
    /// Haltungen und Schaechte werden getrennt gedruckt — der Titel muss sagen, welche
    /// Bauteilart im Dokument steht.
    /// </summary>
    [Fact]
    public void All_nennt_die_Bauteilart_im_Titel()
    {
        var selection = BuilderPageExportScope.All([SchachtRow("S-1"), SchachtRow("S-2")], "Schächte");

        Assert.Equal("Gefilterte Kostenzusammenstellung (2 Schächte)", selection.VariantTitle);
    }

    [Fact]
    public void Single_nennt_die_Bauteilart_im_Titel()
    {
        var selection = BuilderPageExportScope.Single(SchachtRow("80551"), "Schacht");

        Assert.Equal("Kostenzusammenstellung - Schacht 80551", selection.VariantTitle);
    }

    private static DruckcenterRowVm Row(string holding)
        => new() { Holding = holding };

    private static DruckcenterRowVm SchachtRow(string nummer)
        => new() { Holding = nummer, Kind = DruckcenterRowKind.Schacht };
}
