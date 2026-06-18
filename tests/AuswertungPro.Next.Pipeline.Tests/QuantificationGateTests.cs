using AuswertungPro.Next.Application.Ai;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class QuantificationGateTests
{
    // Alle SAM-Werte vorhanden (Hoehe, Breite, Ausdehnung%, Querschnitt%, Uhrlage).
    private static QuantificationGate.AvailableValues AllValues =>
        new(HasHeightMm: true, HasWidthMm: true, HasExtentPercent: true,
            HasCrossSectionPercent: true, HasClock: true);

    private static QuantificationGate.ManifestQuantRule Q1Clock =>
        new(HasQ1: true, HasQ2: false, AllowClock: true);
    private static QuantificationGate.ManifestQuantRule Q1Q2Clock =>
        new(HasQ1: true, HasQ2: true, AllowClock: true);
    private static QuantificationGate.ManifestQuantRule NoQClock =>
        new(HasQ1: false, HasQ2: false, AllowClock: true);
    private static QuantificationGate.ManifestQuantRule NoQNoClock =>
        new(HasQ1: false, HasQ2: false, AllowClock: false);

    [Fact]
    public void Infiltration_BBF_schreibt_keine_Mass_nur_Uhrlage()
    {
        // BBF: Manifest hat keine Q (nur Uhrlage). Trotz vorhandener SAM-Werte -> keine mm/%.
        var d = QuantificationGate.Decide("BBF", NoQClock, AllValues);
        Assert.False(d.WriteHeightMm);
        Assert.False(d.WriteWidthMm);
        Assert.False(d.WriteExtentPercent);
        Assert.False(d.WriteCrossSectionPercent);
        Assert.True(d.WriteClock); // Uhrlage laut Manifest erlaubt
    }

    [Fact]
    public void GefaehrlicheAtmosphaere_BDF_schreibt_gar_nichts()
    {
        var d = QuantificationGate.Decide("BDF", NoQNoClock, AllValues);
        Assert.False(d.WritesAnything);
    }

    [Fact]
    public void Riss_BAB_schreibt_nur_Breite_mm()
    {
        var d = QuantificationGate.Decide("BABBA", Q1Clock, AllValues); // BAB, Char1 'B' (kein Haarriss)
        Assert.True(d.WriteWidthMm);
        Assert.False(d.WriteHeightMm);
        Assert.False(d.WriteExtentPercent);
        Assert.False(d.WriteCrossSectionPercent);
    }

    [Fact]
    public void Haarriss_BABAx_schreibt_keine_Quantifizierung()
    {
        // BABAA = Oberflaechenriss (Haarriss): trotz Manifest-Q1 keine Quantifizierung.
        var d = QuantificationGate.Decide("BABAA", Q1Clock, AllValues);
        Assert.False(d.WriteWidthMm);
        Assert.False(d.WriteHeightMm);
        Assert.False(d.WriteExtentPercent);
        Assert.False(d.WriteCrossSectionPercent);
        Assert.True(d.WriteClock); // Uhrlage bleibt erlaubt
    }

    [Fact]
    public void Anschluss_BCA_schreibt_Hoehe_und_Breite()
    {
        var d = QuantificationGate.Decide("BCAEB", Q1Q2Clock, AllValues);
        Assert.True(d.WriteHeightMm);
        Assert.True(d.WriteWidthMm);
        Assert.False(d.WriteExtentPercent);
        Assert.False(d.WriteCrossSectionPercent);
    }

    [Fact]
    public void Wurzeln_BBA_schreibt_nur_Querschnitt_Prozent()
    {
        var d = QuantificationGate.Decide("BBAC", Q1Clock, AllValues);
        Assert.True(d.WriteCrossSectionPercent);
        Assert.False(d.WriteHeightMm);
        Assert.False(d.WriteWidthMm);
        Assert.False(d.WriteExtentPercent);
    }

    [Fact]
    public void Ablagerung_BBC_schreibt_Ausdehnung_Prozent()
    {
        var d = QuantificationGate.Decide("BBCC", Q1Clock, AllValues);
        Assert.True(d.WriteExtentPercent);
        Assert.False(d.WriteCrossSectionPercent);
        Assert.False(d.WriteHeightMm);
    }

    [Fact]
    public void Wasserspiegel_BDD_schreibt_Ausdehnung_Prozent_ohne_Uhrlage()
    {
        // BDD: Manifest Q1 vorhanden, aber KEINE Uhrlage-Params.
        var manifest = new QuantificationGate.ManifestQuantRule(HasQ1: true, HasQ2: false, AllowClock: false);
        var d = QuantificationGate.Decide("BDDC", manifest, AllValues);
        Assert.True(d.WriteExtentPercent);
        Assert.False(d.WriteClock);
    }

    [Fact]
    public void Code_ohne_Manifest_Q_schreibt_keine_Masse_auch_wenn_SAM_Werte_da()
    {
        // BCC (Bogen): Manifest hat keine Q (User-Entscheidung: Manifest folgen) -> nichts schreiben.
        var d = QuantificationGate.Decide("BCCAA", new QuantificationGate.ManifestQuantRule(false, false, false), AllValues);
        Assert.False(d.WritesAnything);
    }

    [Fact]
    public void Fehlender_SAM_Wert_wird_nicht_geschrieben_trotz_Manifest_Q()
    {
        // BCA erlaubt Hoehe+Breite, aber SAM lieferte nur Hoehe -> nur Hoehe geschrieben.
        var onlyHeight = new QuantificationGate.AvailableValues(
            HasHeightMm: true, HasWidthMm: false, HasExtentPercent: false,
            HasCrossSectionPercent: false, HasClock: false);
        var d = QuantificationGate.Decide("BCAEB", Q1Q2Clock, onlyHeight);
        Assert.True(d.WriteHeightMm);
        Assert.False(d.WriteWidthMm);
    }
}
