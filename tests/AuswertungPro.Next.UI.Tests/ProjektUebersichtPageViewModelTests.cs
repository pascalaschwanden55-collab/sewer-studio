using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.ViewModels.Pages;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class ProjektUebersichtPageViewModelTests
{
    [Fact]
    public void Zustandslegende_listet_Z4_bis_Z0_und_nicht_berechnet()
    {
        var zeilen = ProjektUebersichtPageViewModel.BaueZustandLegende(new Dictionary<string, int> { ["4"] = 4, ["3"] = 3, ["2"] = 3, ["1"] = 2, ["0"] = 1, ["ohne"] = 1 });
        Assert.Equal(new[] { "Z4 · kein Handlungsbedarf", "Z3 · langfristig", "Z2 · mittelfristig", "Z1 · kurzfristig", "Z0 · sofort", "nicht berechnet" }, zeilen.Select(z => z.Label).ToArray());
        Assert.Equal(new[] { 4, 3, 3, 2, 1, 1 }, zeilen.Select(z => z.Anzahl).ToArray());
    }

    /// <summary>
    /// Fix-Runde 1: <c>DashboardStatisticsBuilder.NormalizeZustandsklasse</c> liefert fuer
    /// leer/ungueltig "ohne", nicht "". Ohne diesen Test zeigte die Legende fuer eine
    /// Haltung ohne Zustandsklasse "nicht berechnet" immer mit Anzahl 0.
    /// </summary>
    [Fact]
    public void Zaehlung_ueber_echte_Gruppierung_zeigt_nicht_berechnet_bei_leerer_Zustandsklasse()
    {
        var haltungOhneZustand = new HaltungRecord();
        haltungOhneZustand.SetFieldValue(FieldKeys.HoldingName, "H1", FieldSource.Manual, false);

        var anzahlJeKlasse = ProjektUebersichtPageViewModel.ZaehleZustandsklassen(new[] { haltungOhneZustand });
        var zeilen = ProjektUebersichtPageViewModel.BaueZustandLegende(anzahlJeKlasse);

        var nichtBerechnet = Assert.Single(zeilen, z => z.Label == "nicht berechnet");
        Assert.Equal(1, nichtBerechnet.Anzahl);
    }

    [Fact]
    public void KiLaufzeile_fasst_Vorschlaege_zusammen()
    {
        var zeile = ProjektUebersichtPageViewModel.BaueKiLaufZeile("78998-79002", new[] { ("Bogen", "Meter 9.42"), ("Rohrende", "Sekunde 214") });
        Assert.Equal("Bogen · Rohrende", zeile.Badge);
        Assert.Equal("78998-79002 · Meter 9.42, Sekunde 214", zeile.Meta);
    }
}
