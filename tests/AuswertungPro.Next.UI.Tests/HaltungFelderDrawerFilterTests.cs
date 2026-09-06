using AuswertungPro.Next.UI.Views.Pages.Haltungsansicht;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>Nova-Etappe 1: Feldsuche in den Eingabefeldern filtert nur die Beschriftung, nie den Wert.</summary>
public sealed class HaltungFelderDrawerFilterTests
{
    private static RecordDetailGroup Gruppe(string titel, params string[] labels)
        => new(titel, string.Empty, labels.Select(l => new RecordDetailItem(l, "wert", _ => { })).ToList());

    [Fact]
    public void Ohne_Suchtext_bleiben_alle_Themen_mit_allen_Feldern()
    {
        var themen = HaltungFelderDrawer.Filtere(
            [Gruppe("Stammdaten", "Baujahr", "Material"), Gruppe("Zustand", "Zustandsklasse")], "  ");
        Assert.Equal(["Stammdaten", "Zustand"], themen.Select(t => t.Title));
        Assert.Equal(2, themen[0].EinzelGruppe[0].Items.Count);
    }

    [Fact]
    public void Suchtext_laesst_nur_passende_Felder_stehen_und_blendet_leere_Themen_aus()
    {
        var themen = HaltungFelderDrawer.Filtere(
            [Gruppe("Stammdaten", "Baujahr", "Material"), Gruppe("Zustand", "Zustandsklasse")], "bauj");
        var thema = Assert.Single(themen);
        Assert.Equal("Stammdaten", thema.Title);
        Assert.Equal("Baujahr", Assert.Single(thema.EinzelGruppe[0].Items).Label);
    }

    [Fact]
    public void Null_Gruppen_ergeben_eine_leere_Liste()
        => Assert.Empty(HaltungFelderDrawer.Filtere(null, "x"));
}
