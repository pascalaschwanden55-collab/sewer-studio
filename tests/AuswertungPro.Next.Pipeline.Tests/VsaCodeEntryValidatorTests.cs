using System;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Domain.VsaCatalog;

/// <summary>
/// Charakterisierungs-Tests fuer VsaCodeEntryValidator.
/// </summary>
public sealed class VsaCodeEntryValidatorTests
{
    // ── ValidateQuantField ────────────────────────────────────────────

    [Fact]
    public void ValidateQuantField_null_regel_gibt_null()
    {
        Assert.Null(VsaCodeEntryValidator.ValidateQuantField("anything", null));
    }

    [Fact]
    public void ValidateQuantField_pflichtfeld_leer_gibt_fehler()
    {
        var rule = new QuantField { Pflicht = "P" };
        Assert.Equal("Pflichtfeld", VsaCodeEntryValidator.ValidateQuantField("", rule));
        Assert.Equal("Pflichtfeld", VsaCodeEntryValidator.ValidateQuantField("  ", rule));
    }

    [Fact]
    public void ValidateQuantField_optionales_feld_leer_gibt_null()
    {
        var rule = new QuantField { Pflicht = "O" };
        Assert.Null(VsaCodeEntryValidator.ValidateQuantField("", rule));
    }

    [Fact]
    public void ValidateQuantField_ungueltige_zahl_gibt_fehler()
    {
        var rule = new QuantField { Pflicht = "O" };
        Assert.Equal("Ungueltige Zahl", VsaCodeEntryValidator.ValidateQuantField("abc", rule));
    }

    [Fact]
    public void ValidateQuantField_komma_als_trennzeichen_erlaubt()
    {
        var rule = new QuantField { Pflicht = "O", Min = 0, Max = 100 };
        Assert.Null(VsaCodeEntryValidator.ValidateQuantField("50,5", rule));
    }

    [Fact]
    public void ValidateQuantField_wert_unter_minimum_gibt_fehler()
    {
        var rule = new QuantField { Pflicht = "O", Min = 5.0 };
        var result = VsaCodeEntryValidator.ValidateQuantField("3", rule);
        Assert.NotNull(result);
        Assert.Contains("5", result!);
    }

    [Fact]
    public void ValidateQuantField_wert_ueber_maximum_gibt_fehler()
    {
        var rule = new QuantField { Pflicht = "O", Max = 100.0 };
        var result = VsaCodeEntryValidator.ValidateQuantField("120", rule);
        Assert.NotNull(result);
        Assert.Contains("100", result!);
    }

    [Fact]
    public void ValidateQuantField_wert_im_bereich_gibt_null()
    {
        var rule = new QuantField { Pflicht = "P", Min = 0, Max = 100 };
        Assert.Null(VsaCodeEntryValidator.ValidateQuantField("50", rule));
    }

    [Fact]
    public void ValidateQuantField_grenzwert_exakt_erlaubt()
    {
        var rule = new QuantField { Min = 0, Max = 100 };
        Assert.Null(VsaCodeEntryValidator.ValidateQuantField("0", rule));
        Assert.Null(VsaCodeEntryValidator.ValidateQuantField("100", rule));
    }

    // ── IsValidClock ─────────────────────────────────────────────────

    [Fact]
    public void IsValidClock_gueltige_werte_0_bis_12()
    {
        for (int i = 0; i <= 12; i++)
            Assert.True(VsaCodeEntryValidator.IsValidClock(i.ToString()), $"Uhr {i} sollte gueltig sein");
    }

    [Fact]
    public void IsValidClock_wert_13_ungueltig()
    {
        Assert.False(VsaCodeEntryValidator.IsValidClock("13"));
    }

    [Fact]
    public void IsValidClock_negativ_ungueltig()
    {
        Assert.False(VsaCodeEntryValidator.IsValidClock("-1"));
    }

    [Fact]
    public void IsValidClock_nicht_numerisch_ungueltig()
    {
        Assert.False(VsaCodeEntryValidator.IsValidClock("abc"));
    }

    [Fact]
    public void IsValidClock_whitespace_wird_getrimmt()
    {
        Assert.True(VsaCodeEntryValidator.IsValidClock(" 6 "));
    }

    // ── TryParseDouble ────────────────────────────────────────────────

    [Fact]
    public void TryParseDouble_punkt_als_trennzeichen()
    {
        Assert.True(VsaCodeEntryValidator.TryParseDouble("12.5", out var v));
        Assert.Equal(12.5, v, 6);
    }

    [Fact]
    public void TryParseDouble_komma_als_trennzeichen()
    {
        Assert.True(VsaCodeEntryValidator.TryParseDouble("12,5", out var v));
        Assert.Equal(12.5, v, 6);
    }

    [Fact]
    public void TryParseDouble_text_gibt_false()
    {
        Assert.False(VsaCodeEntryValidator.TryParseDouble("nicht", out _));
    }

    // ── TryParseTime ─────────────────────────────────────────────────

    [Fact]
    public void TryParseTime_mmss_format()
    {
        Assert.True(VsaCodeEntryValidator.TryParseTime("01:30", out var ts));
        Assert.Equal(TimeSpan.FromSeconds(90), ts);
    }

    [Fact]
    public void TryParseTime_hhmmss_format()
    {
        Assert.True(VsaCodeEntryValidator.TryParseTime("01:02:03", out var ts));
        Assert.Equal(new TimeSpan(1, 2, 3), ts);
    }

    [Fact]
    public void TryParseTime_ungueltig_gibt_false()
    {
        Assert.False(VsaCodeEntryValidator.TryParseTime("keinzeit", out _));
    }
}
