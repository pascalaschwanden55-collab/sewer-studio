using AuswertungPro.Next.Application.Reports;
using AuswertungPro.Next.Application.Xtf;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Schachtgrafik Stammkarte: die Richtung, unter der eine Leitung den Schacht verlaesst.
/// Die Zahlen des Falls 80409 stammen aus der QGIS-Kopie (19.09.2026).
/// </summary>
public sealed class SchachtAnschlussRichtungTests
{
    private static readonly XtfPunkt Schacht = new(2692630.471, 1192370.448);

    [Fact]
    public void Nach_Norden_ist_null_Grad_nach_Osten_neunzig()
    {
        var s = new XtfPunkt(0, 0);
        Assert.Equal(0d, SchachtAnschlussRichtung.Azimut(s, [s, new XtfPunkt(0, 10)])!.Value, 3);
        Assert.Equal(90d, SchachtAnschlussRichtung.Azimut(s, [s, new XtfPunkt(10, 0)])!.Value, 3);
        Assert.Equal(180d, SchachtAnschlussRichtung.Azimut(s, [s, new XtfPunkt(0, -10)])!.Value, 3);
        Assert.Equal(270d, SchachtAnschlussRichtung.Azimut(s, [s, new XtfPunkt(-10, 0)])!.Value, 3);
    }

    [Fact]
    public void Endet_die_Leitung_am_Schacht_zaehlt_die_Richtung_vom_Schacht_weg()
    {
        // 80547-80409: beginnt beim Nachbarschacht und ENDET bei 80409.
        var verlauf = new[] { new XtfPunkt(2692653.023, 1192412.999), new XtfPunkt(2692640.0, 1192390.0), Schacht };

        var azimut = SchachtAnschlussRichtung.Azimut(Schacht, verlauf);

        Assert.NotNull(azimut);
        Assert.InRange(azimut!.Value, 20, 40);
    }

    [Fact]
    public void Der_Auslauf_80409_80538_zeigt_nach_Westen()
    {
        var verlauf = new[] { Schacht, new XtfPunkt(2692593.472, 1192373.417) };

        var azimut = SchachtAnschlussRichtung.Azimut(Schacht, verlauf);

        Assert.InRange(azimut!.Value, 274, 276);
    }

    [Fact]
    public void Eine_Leitung_die_nicht_am_Schacht_endet_hat_keine_Richtung()
    {
        var verlauf = new[] { new XtfPunkt(2692632.0, 1192372.0), new XtfPunkt(2692700.0, 1192400.0) };

        Assert.Null(SchachtAnschlussRichtung.Azimut(Schacht, verlauf));
        Assert.NotNull(SchachtAnschlussRichtung.Azimut(Schacht, verlauf, toleranzM: 5));
    }

    [Fact]
    public void Doppelte_Stuetzpunkte_am_Ende_werden_uebersprungen()
    {
        var verlauf = new[] { Schacht, Schacht, new XtfPunkt(2692630.471, 1192380.448) };

        Assert.Equal(0d, SchachtAnschlussRichtung.Azimut(Schacht, verlauf)!.Value, 3);
    }

    [Fact]
    public void Ohne_zwei_verschiedene_Punkte_gibt_es_keine_Richtung()
    {
        Assert.Null(SchachtAnschlussRichtung.Azimut(Schacht, null));
        Assert.Null(SchachtAnschlussRichtung.Azimut(Schacht, [Schacht]));
        Assert.Null(SchachtAnschlussRichtung.Azimut(Schacht, [Schacht, Schacht]));
    }

    [Fact]
    public void Relativ_zum_Auslauf_liegt_E2_rechts_unten_und_E3_links_unten()
    {
        // Auslauf 275°, E2 28°, E3 145° — dieselbe Lage wie die Handskizze des Operateurs.
        Assert.Equal(113d, SchachtAnschlussRichtung.Relativ(28, 275), 3);
        Assert.Equal(230d, SchachtAnschlussRichtung.Relativ(145, 275), 3);
        Assert.Equal(0d, SchachtAnschlussRichtung.Relativ(275, 275), 3);
    }

    [Theory]
    [InlineData(0, 12)]
    [InlineData(14, 12)]
    [InlineData(16, 1)]
    [InlineData(90, 3)]
    [InlineData(113, 4)]
    [InlineData(230, 8)]
    [InlineData(359, 12)]
    public void Die_Uhrlage_rundet_auf_die_naechste_Stunde(double relativ, int erwartet)
    {
        Assert.Equal(erwartet, SchachtAnschlussRichtung.Uhr(relativ));
    }
}
