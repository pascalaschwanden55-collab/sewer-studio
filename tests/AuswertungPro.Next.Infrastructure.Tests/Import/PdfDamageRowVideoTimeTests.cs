using System;
using System.Linq;
using AuswertungPro.Next.Infrastructure.Import.Pdf;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

/// <summary>
/// Der Videozaehlerstand ist die Sekunde ab Dateianfang (SN EN 13508-2, 3.1.10).
/// Er stand in den Protokollzeilen, wurde vom Parser erkannt und dann verworfen.
/// </summary>
public sealed class PdfDamageRowVideoTimeTests
{
    private static readonly PdfParser Parser = new();

    [Fact]
    public void Fretzformat_die_zeit_vor_dem_meterwert_wird_gelesen()
    {
        var rows = Parser.ParseDamageRows("1777 00:00:09 0.00 BCD Rohranfang");

        var row = Assert.Single(rows);
        Assert.Equal("BCD", row.Code);
        Assert.Equal("0.00", row.Meter);
        Assert.Equal(TimeSpan.FromSeconds(9), row.VideoTime);
    }

    [Fact]
    public void Zeit_hinter_der_beschreibung_wird_gelesen_und_bleibt_aus_dem_text_draussen()
    {
        var rows = Parser.ParseDamageRows("12.30 BAB Riss laengs 00:05:09 weiteres");

        var row = Assert.Single(rows);
        Assert.Equal(new TimeSpan(0, 5, 9), row.VideoTime);
        Assert.Equal("Riss laengs", row.Description);
    }

    [Fact]
    public void Ohne_zeit_bleibt_der_wert_leer_statt_geraten()
    {
        var rows = Parser.ParseDamageRows("27.70 BCE Rohrende");

        Assert.Null(Assert.Single(rows).VideoTime);
    }

    [Fact]
    public void Die_zeit_erreicht_den_befund()
    {
        var text = string.Join("\n",
            "1777 00:00:09 0.00 BCD Rohranfang",
            "2001 00:05:09 12.30 BAB Riss laengs",
            "2050 00:09:41 27.70 BCE Rohrende");

        var findings = PdfPrimaryDamageFindingBuilder.Build(
            PdfParserTestHilfe.PrimaereSchaeden(text), Parser.ParseDamageRows(text));

        Assert.Equal("00:00:09", findings.Single(f => f.KanalSchadencode == "BCD").MPEG);
        Assert.Equal("00:05:09", findings.Single(f => f.KanalSchadencode == "BAB").MPEG);
        Assert.Equal("00:09:41", findings.Single(f => f.KanalSchadencode == "BCE").MPEG);
    }

    [Fact]
    public void Ohne_zeilen_bleibt_alles_wie_bisher()
    {
        var text = "0.00 BCD Rohranfang";

        var findings = PdfPrimaryDamageFindingBuilder.Build(
            PdfParserTestHilfe.PrimaereSchaeden(text), null);

        Assert.Null(Assert.Single(findings).MPEG);
    }

    [Fact]
    public void Zwei_gleiche_codes_am_selben_meter_bleiben_ohne_zeit()
    {
        // Nicht unterscheidbar — dann lieber keine Zeit als die falsche.
        var text = string.Join("\n",
            "00:01:00 5.00 BAB Riss A",
            "",
            "00:02:00 5.00 BAB Riss B");

        var findings = PdfPrimaryDamageFindingBuilder.Build(
            PdfParserTestHilfe.PrimaereSchaeden(text), Parser.ParseDamageRows(text));

        Assert.All(findings, f => Assert.Null(f.MPEG));
    }
}

internal static class PdfParserTestHilfe
{
    /// <summary>Baut das Textfeld genauso wie der Import es ablegen wuerde.</summary>
    internal static string PrimaereSchaeden(string text)
        => PrimaryDamageRowParser.ExtractPrimaryDamages(text.Replace("\r\n", "\n").Split('\n'));
}
