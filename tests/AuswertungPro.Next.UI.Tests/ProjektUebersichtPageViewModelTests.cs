using System.Linq;
using AuswertungPro.Next.UI.ViewModels.Pages;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class ProjektUebersichtPageViewModelTests
{
    [Fact]
    public void Zustandslegende_listet_Z4_bis_Z0_und_nicht_berechnet()
    {
        var zeilen = ProjektUebersichtPageViewModel.BaueZustandLegende(new System.Collections.Generic.Dictionary<string, int> { ["4"] = 4, ["3"] = 3, ["2"] = 3, ["1"] = 2, ["0"] = 1, [""] = 1 });
        Assert.Equal(new[] { "Z4 · kein Handlungsbedarf", "Z3 · langfristig", "Z2 · mittelfristig", "Z1 · kurzfristig", "Z0 · sofort", "nicht berechnet" }, zeilen.Select(z => z.Label).ToArray());
        Assert.Equal(new[] { 4, 3, 3, 2, 1, 1 }, zeilen.Select(z => z.Anzahl).ToArray());
    }

    [Fact]
    public void KiLaufzeile_fasst_Vorschlaege_zusammen()
    {
        var zeile = ProjektUebersichtPageViewModel.BaueKiLaufZeile("78998-79002", new[] { ("Bogen", "Meter 9,42"), ("Rohrende", "Sekunde 214") });
        Assert.Equal("Bogen · Rohrende", zeile.Badge);
        Assert.Equal("78998-79002 · Meter 9,42, Sekunde 214", zeile.Meta);
    }
}
