using AuswertungPro.Next.Domain.VsaCatalog;

namespace AuswertungPro.Next.Pipeline.Tests;

// Charakterisierungs-Tests fuer VsaLabelBuilder.LookupLabel (IST-Verhalten)
public sealed class VsaLabelBuilderTests
{
    // 2-stelliger Code = Gruppenname
    [Fact]
    public void LookupLabel_gibt_gruppenname_fuer_2_zeichen()
    {
        Assert.Equal("Struktur der Rohrleitungen", VsaLabelBuilder.LookupLabel("BA"));
        Assert.Equal("Betrieb der Rohrleitungen", VsaLabelBuilder.LookupLabel("BB"));
        Assert.Equal("Bestandsaufnahme der Rohrleitungen", VsaLabelBuilder.LookupLabel("BC"));
    }

    // 3-stelliger Hauptcode
    [Fact]
    public void LookupLabel_gibt_hauptcode_label_fuer_3_zeichen()
    {
        Assert.Equal("Risse", VsaLabelBuilder.LookupLabel("BAB"));
        Assert.Equal("Rohranfang", VsaLabelBuilder.LookupLabel("BCD"));
        Assert.Equal("Rohrende", VsaLabelBuilder.LookupLabel("BCE"));
        Assert.Equal("Seitl. Anschluss", VsaLabelBuilder.LookupLabel("BCA"));
    }

    // 4-stelliger Code = Hauptcode + Char1
    [Fact]
    public void LookupLabel_haengt_char1_an_fuer_4_zeichen()
    {
        // BAB = Risse, A = Haarriss
        Assert.Equal("Risse, Haarriss", VsaLabelBuilder.LookupLabel("BABA"));
        // BAB = Risse, B = Riss
        Assert.Equal("Risse, Riss", VsaLabelBuilder.LookupLabel("BABB"));
    }

    // 5-stelliger Code = Hauptcode + Char1 + Char2
    [Fact]
    public void LookupLabel_haengt_char2_an_fuer_5_zeichen()
    {
        // BABA = Risse, Haarriss; A = laengs (aus globalem Char2)
        Assert.Equal("Risse, Haarriss, laengs", VsaLabelBuilder.LookupLabel("BABAA"));
        // BABAL = kein Char2 L -> nur Hauptcode+Char1
        Assert.Equal("Risse, Haarriss", VsaLabelBuilder.LookupLabel("BABAL"));
    }

    // Char2PerChar1 hat Vorrang vor globalem Char2
    [Fact]
    public void LookupLabel_bevorzugt_char2_per_char1()
    {
        // BAI: Char2PerChar1["A"] hat eigene Definitionen
        // BAIAA = Einrag. Dichtungsmaterial, Dichtring, verschoben
        Assert.Equal("Einrag. Dichtungsmaterial, Dichtring, verschoben", VsaLabelBuilder.LookupLabel("BAIAA"));
    }

    // CharDef-eigenes Char2 (auf Char1-Ebene) hat zweiten Vorrang
    [Fact]
    public void LookupLabel_nutzt_chardef_char2_wenn_kein_char2_per_char1()
    {
        // BAK D = Faltenbildung, hat eigenes Char2: A=laengs
        Assert.Equal("Innenauskleidung, Faltenbildung, laengs", VsaLabelBuilder.LookupLabel("BAKDA"));
    }

    // Unbekannte Codes geben null
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("X")]
    [InlineData("XY")]
    [InlineData("XYZ")]
    public void LookupLabel_gibt_null_fuer_unbekannte_und_leere_codes(string? code)
    {
        Assert.Null(VsaLabelBuilder.LookupLabel(code!));
    }

    // Unbekannte Gruppe
    [Fact]
    public void LookupLabel_gibt_null_fuer_unbekannte_gruppe()
    {
        Assert.Null(VsaLabelBuilder.LookupLabel("XX"));
        Assert.Null(VsaLabelBuilder.LookupLabel("XXX"));
    }
}
