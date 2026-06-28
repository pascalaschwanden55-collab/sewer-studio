using AuswertungPro.Next.Application.Reports;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>Charakterisierungs-Tests fuer ProtocolZustandText (IST-Verhalten).</summary>
public sealed class ProtocolZustandTextTests
{
    // --- Shorten ---

    [Fact]
    public void Shorten_kurzer_text_unveraendert()
        => Assert.Equal("Hallo", ProtocolZustandText.Shorten("Hallo", 10));

    [Fact]
    public void Shorten_exakt_max_unveraendert()
        => Assert.Equal("12345", ProtocolZustandText.Shorten("12345", 5));

    [Fact]
    public void Shorten_laengerer_text_wird_mit_ellipse_abgeschnitten()
    {
        var result = ProtocolZustandText.Shorten("Abcdefghij", 5);
        Assert.Equal("Abcd…", result);
    }

    [Fact]
    public void Shorten_leer_gibt_leerstring()
        => Assert.Equal("", ProtocolZustandText.Shorten("", 10));

    [Fact]
    public void Shorten_whitespace_gibt_leerstring()
        => Assert.Equal("", ProtocolZustandText.Shorten("   ", 10));

    // --- NormalizeZustandDescription ---

    [Fact]
    public void NormalizeZustandDescription_null_liefert_leerstring()
        => Assert.Equal("", ProtocolZustandText.NormalizeZustandDescription(null, null));

    [Fact]
    public void NormalizeZustandDescription_leer_liefert_leerstring()
        => Assert.Equal("", ProtocolZustandText.NormalizeZustandDescription("", "BAB"));

    [Fact]
    public void NormalizeZustandDescription_klammermuster_extrahiert_inhalt()
    {
        // "BAB @3.50m (Laengsriss)" -> "Laengsriss"
        var result = ProtocolZustandText.NormalizeZustandDescription("BAB @3.50m (Laengsriss)", "BAB");
        Assert.Equal("Laengsriss", result);
    }

    [Fact]
    public void NormalizeZustandDescription_entfernt_fuehrenden_code_token()
    {
        var result = ProtocolZustandText.NormalizeZustandDescription("BAB Laengsriss", "BAB");
        Assert.Equal("Laengsriss", result);
    }

    [Fact]
    public void NormalizeZustandDescription_entfernt_meter_prefix()
    {
        var result = ProtocolZustandText.NormalizeZustandDescription("3.50m Laengsriss", null);
        Assert.Equal("Laengsriss", result);
    }

    [Fact]
    public void NormalizeZustandDescription_entfernt_richtungsaenderung_redundant()
    {
        var result = ProtocolZustandText.NormalizeZustandDescription("Bogen Richtungsänderung", null);
        // "Bogen" bleibt, "Richtungsänderung" wird entfernt
        Assert.DoesNotContain("Richtungsänderung", result);
        Assert.Contains("Bogen", result);
    }

    [Fact]
    public void NormalizeZustandDescription_entfernt_import_artefakt_trailing_hash()
    {
        var result = ProtocolZustandText.NormalizeZustandDescription("Schaden -80631_6e c06c5c-c9", null);
        Assert.Equal("Schaden", result);
    }

    [Fact]
    public void NormalizeZustandDescription_normaler_text_bleibt_erhalten()
    {
        var result = ProtocolZustandText.NormalizeZustandDescription("Laengsriss an der Sohle", "BAB");
        Assert.Equal("Laengsriss an der Sohle", result);
    }

    // --- BuildHaltungsgrafikZustandText ---

    [Fact]
    public void BuildHaltungsgrafikZustandText_kein_text_liefert_strich()
    {
        var entry = new ProtocolEntry { Code = "BAB", Beschreibung = "" };
        Assert.Equal("-", ProtocolZustandText.BuildHaltungsgrafikZustandText(entry));
    }

    [Fact]
    public void BuildHaltungsgrafikZustandText_langer_text_wird_auf_120_zeichen_gekuerzt()
    {
        var lang = new string('A', 200);
        var entry = new ProtocolEntry { Code = "BAB", Beschreibung = lang };
        var result = ProtocolZustandText.BuildHaltungsgrafikZustandText(entry);
        Assert.True(result.Length <= 120);
        Assert.EndsWith("…", result);
    }

    [Fact]
    public void BuildHaltungsgrafikZustandText_kurzer_text_bleibt_vollstaendig()
    {
        var entry = new ProtocolEntry { Code = "BAB", Beschreibung = "Laengsriss" };
        Assert.Equal("Laengsriss", ProtocolZustandText.BuildHaltungsgrafikZustandText(entry));
    }

    // --- BuildObservationZustandTextLong ---

    [Fact]
    public void BuildObservationZustandTextLong_kein_text_liefert_strich()
    {
        var entry = new ProtocolEntry { Code = "BAB", Beschreibung = "" };
        Assert.Equal("-", ProtocolZustandText.BuildObservationZustandTextLong(entry));
    }

    [Fact]
    public void BuildObservationZustandTextLong_langer_text_wird_nicht_gekuerzt()
    {
        var lang = new string('A', 200);
        var entry = new ProtocolEntry { Code = "BAB", Beschreibung = lang };
        var result = ProtocolZustandText.BuildObservationZustandTextLong(entry);
        Assert.DoesNotContain("…", result);
        Assert.Equal(200, result.Length);
    }

    [Fact]
    public void BuildObservationZustandTextLong_normaler_text_bleibt_erhalten()
    {
        var entry = new ProtocolEntry { Code = "BAB", Beschreibung = "Laengsriss an der Sohle" };
        Assert.Equal("Laengsriss an der Sohle", ProtocolZustandText.BuildObservationZustandTextLong(entry));
    }
}
