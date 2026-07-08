using AuswertungPro.Next.UI.ViewModels.Pages;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SanierungMatrixOptionDeriverTests
{
    private const string Vd = "VORARBEIT_VD";
    private const string Wasser = "VORARBEIT_WASSERHALTUNG";
    private const string Fraesen = "VORARBEIT_FRAESEN";
    private const string Dicht = "QK_DICHTHEITSPRUEFUNG";
    private const string Doku = "QK_DOKUMENTATION";

    [Fact]
    public void Derive_setzt_nur_haekchen_fuer_ausgewaehlte_keys()
    {
        var lines = new (string?, bool)[]
        {
            (Doku, true),
            (Vd, false),                          // vorhanden, aber nicht ausgewaehlt
            (Dicht, true),
            ("SCHLAUCHLINER_NADELFILZ_OPENEND", true), // Hauptmassnahme, kein Zusatz-Key
        };

        var f = SanierungMatrixOptionDeriver.Derive(lines, Vd, Wasser, Fraesen, Dicht, Doku);

        Assert.True(f.Doku);
        Assert.True(f.Dichtheit);
        Assert.False(f.Vd);
        Assert.False(f.Wasser);
        Assert.False(f.Fraesen);
    }

    [Fact]
    public void Derive_key_ohne_auswahl_bleibt_false()
    {
        var lines = new (string?, bool)[] { (Doku, false) };
        var f = SanierungMatrixOptionDeriver.Derive(lines, Vd, Wasser, Fraesen, Dicht, Doku);
        Assert.False(f.Doku);
    }

    [Fact]
    public void Derive_matcht_key_case_insensitive_und_getrimmt()
    {
        var lines = new (string?, bool)[] { ("  qk_dokumentation ", true) };
        var f = SanierungMatrixOptionDeriver.Derive(lines, Vd, Wasser, Fraesen, Dicht, Doku);
        Assert.True(f.Doku);
    }
}
