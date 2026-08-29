using AuswertungPro.Next.Application.Reports;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>Charakterisierungs-Tests fuer ProtocolTextHelpers (IST-Verhalten).</summary>
public sealed class ProtocolTextHelpersTests
{
    // --- ExtractSingleDate ---

    [Fact]
    public void ExtractSingleDate_datumsbereich_liefert_erstes_datum()
        => Assert.Equal("05.11.2025", ProtocolTextHelpers.ExtractSingleDate("05.11.2025 - 11.11.2025"));

    [Fact]
    public void ExtractSingleDate_einzel_datum_unveraendert()
        => Assert.Equal("05.11.2025", ProtocolTextHelpers.ExtractSingleDate("05.11.2025"));

    [Fact]
    public void ExtractSingleDate_bis_trennzeichen_wird_erkannt()
        => Assert.Equal("05.11.2025", ProtocolTextHelpers.ExtractSingleDate("05.11.2025 bis 11.11.2025"));

    [Fact]
    public void ExtractSingleDate_leer_gibt_leer_zurueck()
        => Assert.Equal("", ProtocolTextHelpers.ExtractSingleDate(""));

    [Fact]
    public void ExtractSingleDate_null_gibt_null_zurueck()
        => Assert.Null(ProtocolTextHelpers.ExtractSingleDate(null!));

    // --- IsAbortCode ---

    [Theory]
    [InlineData("BDC",   true)]
    [InlineData("BDCA",  true)]
    [InlineData("BDCB",  true)]
    [InlineData("bdc",   true)]  // case-insensitiv
    public void IsAbortCode_bdc_codes_sind_abbruch(string code, bool expected)
    {
        var entry = new ProtocolEntry { Code = code };
        Assert.Equal(expected, ProtocolTextHelpers.IsAbortCode(entry));
    }

    [Theory]
    [InlineData("BAB")]
    [InlineData("BCD")]
    [InlineData("")]
    public void IsAbortCode_andere_codes_sind_kein_abbruch(string code)
    {
        var entry = new ProtocolEntry { Code = code };
        Assert.False(ProtocolTextHelpers.IsAbortCode(entry));
    }

    // --- IsLateralConnection ---

    [Theory]
    [InlineData("BAG",  true)]
    [InlineData("BAGA", true)]
    [InlineData("BAH",  true)]
    [InlineData("BCA",  true)]
    [InlineData("BCAA", true)]
    [InlineData("BCAB", true)]
    public void IsLateralConnection_anschluss_codes_sind_lateral(string code, bool expected)
    {
        var entry = new ProtocolEntry { Code = code };
        Assert.Equal(expected, ProtocolTextHelpers.IsLateralConnection(entry));
    }

    [Theory]
    [InlineData("BAB")]
    [InlineData("BCD")]
    [InlineData("BCE")]
    [InlineData("BBC")]
    public void IsLateralConnection_strukturelle_schaeden_sind_nicht_lateral(string code)
    {
        var entry = new ProtocolEntry { Code = code };
        Assert.False(ProtocolTextHelpers.IsLateralConnection(entry));
    }

    [Fact]
    public void IsLateralConnection_eindeutiger_Rohranfang_Code_geht_vor_Beschreibung()
    {
        var entry = new ProtocolEntry { Code = "BCD", Beschreibung = "Seiteneinlauf vorhanden" };
        Assert.False(ProtocolTextHelpers.IsLateralConnection(entry));
    }

    [Fact]
    public void IsLateralConnection_ohne_Code_nutzt_Beschreibung_als_Rueckfall()
    {
        var entry = new ProtocolEntry { Code = "", Beschreibung = "Seiteneinlauf vorhanden" };
        Assert.True(ProtocolTextHelpers.IsLateralConnection(entry));
    }

    // --- ExtractClockHour ---

    [Fact]
    public void ExtractClockHour_ohne_parameters_gibt_null_zurueck()
    {
        var entry = new ProtocolEntry { Code = "BAB" };
        Assert.Null(ProtocolTextHelpers.ExtractClockHour(entry));
    }

    [Fact]
    public void ExtractClockHour_vsa_uhr_von_wird_bevorzugt()
    {
        var entry = new ProtocolEntry
        {
            Code = "BAB",
            CodeMeta = new ProtocolEntryCodeMeta
            {
                Parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    { "vsa.uhr.von", "3" },
                    { "ClockPos1", "9" }
                }
            }
        };
        Assert.Equal(3, ProtocolTextHelpers.ExtractClockHour(entry));
    }

    [Fact]
    public void ExtractClockHour_ungueltiger_Meterwert_faellt_auf_WinCan_Uhrlage_zurueck()
    {
        var entry = new ProtocolEntry
        {
            Code = "BCAAA",
            Beschreibung = "Anschluss mit Formstueck, offen, bei 9 Uhr",
            CodeMeta = new ProtocolEntryCodeMeta
            {
                Parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["vsa.uhr.von"] = "2.62136",
                    ["ClockPos1"] = "9"
                }
            }
        };

        Assert.Equal(9, ProtocolTextHelpers.ExtractClockHour(entry));
    }

    [Theory]
    [InlineData("3",        3)]
    [InlineData("3 Uhr",    3)]
    [InlineData("03:00",    3)]
    [InlineData("3.00",     3)]
    [InlineData("12",       12)]
    public void ExtractClockHour_gueltiger_uhrzeitwert_wird_erkannt(string rawValue, int expectedHour)
    {
        var entry = new ProtocolEntry
        {
            Code = "BAB",
            CodeMeta = new ProtocolEntryCodeMeta
            {
                Parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    { "vsa.uhr.von", rawValue }
                }
            }
        };
        Assert.Equal(expectedHour, ProtocolTextHelpers.ExtractClockHour(entry));
    }

    [Fact]
    public void ExtractClockHour_ungueltiger_wert_gibt_null_zurueck()
    {
        var entry = new ProtocolEntry
        {
            Code = "BAB",
            CodeMeta = new ProtocolEntryCodeMeta
            {
                Parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    { "vsa.uhr.von", "ungueltig" }
                }
            }
        };
        Assert.Null(ProtocolTextHelpers.ExtractClockHour(entry));
    }

    [Theory]
    [InlineData("2.2")]
    [InlineData("2.62136")]
    [InlineData("0")]
    [InlineData("13")]
    public void ExtractClockHour_messwert_oder_unmoegliche_stunde_wird_abgelehnt(string rawValue)
    {
        var entry = new ProtocolEntry
        {
            Code = "BAB",
            CodeMeta = new ProtocolEntryCodeMeta
            {
                Parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["vsa.uhr.von"] = rawValue
                }
            }
        };

        Assert.Null(ProtocolTextHelpers.ExtractClockHour(entry));
    }

    [Fact]
    public void ExtractClockHour_Meterwert_ohne_Alternative_wird_nicht_als_Uhrlage_verwendet()
    {
        var entry = new ProtocolEntry
        {
            Code = "BCAAA",
            CodeMeta = new ProtocolEntryCodeMeta
            {
                Parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["vsa.uhr.von"] = "2.62"
                }
            }
        };

        Assert.Null(ProtocolTextHelpers.ExtractClockHour(entry));
    }

    [Fact]
    public void ExtractClockHour_Quantifizierung_wird_nicht_als_Uhrlage_erfunden()
    {
        var entry = new ProtocolEntry
        {
            Code = "BCAAA",
            CodeMeta = new ProtocolEntryCodeMeta
            {
                Parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Quantifizierung1"] = "10"
                }
            }
        };

        Assert.Null(ProtocolTextHelpers.ExtractClockHour(entry));
    }

    [Fact]
    public void ExtractClockHour_Rohrende_mit_Anschlussbemerkung_bleibt_ohne_Anschlussuhrlage()
    {
        var entry = new ProtocolEntry
        {
            Code = "BCE",
            Beschreibung = "Rohrende, Anschluss von 12 Uhr in Schmutzleitung"
        };

        Assert.Null(ProtocolTextHelpers.ExtractClockHour(entry));
    }

    [Theory]
    [InlineData("Anschluss von 4 Uhr bis 8 Uhr", 8)]
    [InlineData("Anschluss von 4 bis 8 Uhr", 8)]
    [InlineData("Anschluss bei 9 Uhr", null)]
    public void ExtractClockHourEndFromText_liest_nur_Bereichsende(string text, int? expected)
        => Assert.Equal(expected, ProtocolTextHelpers.ExtractClockHourEndFromText(text));

    // --- EscapeSvgText ---

    [Fact]
    public void EscapeSvgText_amp_wird_escaped()
        => Assert.Equal("&amp;", ProtocolTextHelpers.EscapeSvgText("&"));

    [Fact]
    public void EscapeSvgText_kleiner_als_wird_escaped()
        => Assert.Equal("&lt;", ProtocolTextHelpers.EscapeSvgText("<"));

    [Fact]
    public void EscapeSvgText_groesser_als_wird_escaped()
        => Assert.Equal("&gt;", ProtocolTextHelpers.EscapeSvgText(">"));

    [Fact]
    public void EscapeSvgText_anfuehrungszeichen_wird_escaped()
        => Assert.Equal("&quot;", ProtocolTextHelpers.EscapeSvgText("\""));

    [Fact]
    public void EscapeSvgText_apostroph_wird_escaped()
        => Assert.Equal("&apos;", ProtocolTextHelpers.EscapeSvgText("'"));

    [Fact]
    public void EscapeSvgText_normaler_text_bleibt_unveraendert()
        => Assert.Equal("Schaden", ProtocolTextHelpers.EscapeSvgText("Schaden"));

    [Fact]
    public void EscapeSvgText_leer_gibt_leerstring()
        => Assert.Equal("", ProtocolTextHelpers.EscapeSvgText(""));

    [Fact]
    public void EscapeSvgText_kombination_aller_sonderzeichen()
        => Assert.Equal("&lt;a href=&quot;x&quot;&gt;Test &amp; &apos;y&apos;&lt;/a&gt;",
            ProtocolTextHelpers.EscapeSvgText("<a href=\"x\">Test & 'y'</a>"));
}
