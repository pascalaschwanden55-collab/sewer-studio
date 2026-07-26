using System.Collections.Generic;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Costs;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Charakterisierungstests fuer MeasureImportDefaultsResolver.
/// </summary>
public sealed class MeasureImportDefaultsResolverTests
{
    [Fact]
    public void Resolve_ParsesDnAndLength()
    {
        var record = MakeRecord(new Dictionary<string, string>
        {
            ["DN_mm"] = "300",
            ["Haltungslaenge_m"] = "45.30"
        });

        var defaults = MeasureImportDefaultsResolver.Resolve(record);

        Assert.Equal(300, defaults.Dn);
        Assert.Equal(45.30m, defaults.LengthMeters);
    }

    [Fact]
    public void Resolve_LengthWithComma_ParsedCorrectly()
    {
        // Kulturunabhaengig: "45,30" soll zu 45.30, nicht 4530
        var record = MakeRecord(new Dictionary<string, string>
        {
            ["DN_mm"] = "200",
            ["Haltungslaenge_m"] = "45,30"
        });

        var defaults = MeasureImportDefaultsResolver.Resolve(record);

        Assert.Equal(45.30m, defaults.LengthMeters);
    }

    [Theory]
    [InlineData("45.678", "45.678")]
    [InlineData("45,678", "45.678")]
    [InlineData("1'300.50", "1300.50")]
    public void Resolve_Length_akzeptiert_praezise_und_gruppierte_Messwerte(
        string raw,
        string expected)
    {
        var record = MakeRecord(new Dictionary<string, string>
        {
            ["Haltungslaenge_m"] = raw
        });

        var defaults = MeasureImportDefaultsResolver.Resolve(record);

        Assert.Equal(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), defaults.LengthMeters);
    }

    [Fact]
    public void Resolve_MissingDn_ReturnsNullDn()
    {
        var record = MakeRecord(new Dictionary<string, string>
        {
            ["Haltungslaenge_m"] = "30.00"
        });

        var defaults = MeasureImportDefaultsResolver.Resolve(record);

        Assert.Null(defaults.Dn);
        Assert.Equal(30.00m, defaults.LengthMeters);
    }

    [Fact]
    public void Resolve_MissingLength_ReturnsNullLength()
    {
        var record = MakeRecord(new Dictionary<string, string>
        {
            ["DN_mm"] = "150"
        });

        var defaults = MeasureImportDefaultsResolver.Resolve(record);

        Assert.Equal(150, defaults.Dn);
        Assert.Null(defaults.LengthMeters);
        Assert.False(defaults.LengthIsInvalid);
    }

    [Theory]
    [InlineData("45'30")]
    [InlineData("0")]
    [InlineData("-2.5")]
    public void Resolve_NonEmptyInvalidLength_IsReported(string raw)
    {
        var record = MakeRecord(new Dictionary<string, string>
        {
            ["Haltungslaenge_m"] = raw
        });

        var defaults = MeasureImportDefaultsResolver.Resolve(record);

        Assert.Null(defaults.LengthMeters);
        Assert.True(defaults.LengthIsInvalid);
    }

    [Fact]
    public void Resolve_EmptyRecord_ReturnsAllNullsAndZeroConnections()
    {
        var record = MakeRecord(new Dictionary<string, string>());

        var defaults = MeasureImportDefaultsResolver.Resolve(record);

        Assert.Null(defaults.Dn);
        Assert.Null(defaults.LengthMeters);
        Assert.Equal(0, defaults.Connections);
    }

    [Fact]
    public void Resolve_InvalidDn_ReturnsNullDn()
    {
        var record = MakeRecord(new Dictionary<string, string>
        {
            ["DN_mm"] = "abc",
            ["Haltungslaenge_m"] = "20.00"
        });

        var defaults = MeasureImportDefaultsResolver.Resolve(record);

        Assert.Null(defaults.Dn);
    }

    // -------------------------------------------------------------------------
    // Hilfsfunktionen
    // -------------------------------------------------------------------------

    private static HaltungRecord MakeRecord(Dictionary<string, string> fields)
    {
        var record = new HaltungRecord();
        foreach (var kv in fields)
            record.SetFieldValue(kv.Key, kv.Value, FieldSource.Pdf, userEdited: false);
        return record;
    }
}
