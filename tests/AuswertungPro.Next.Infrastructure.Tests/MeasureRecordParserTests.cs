using System;
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Ai;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Charakterisierungstests fuer MeasureRecordParser.
/// Prueft das IST-Verhalten aller extrahierten Pure-Static-Methoden.
/// </summary>
public sealed class MeasureRecordParserTests
{
    // ── NormalizeCode ────────────────────────────────────────────────

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData("bab", "BAB")]
    [InlineData("BAB", "BAB")]
    [InlineData("  BAB  ", "BAB")]
    [InlineData("B", "")]           // zu kurz
    [InlineData("ABCDEFGHIJKLM", "")]  // zu lang (>12)
    [InlineData("123", "")]         // kein Buchstabe
    [InlineData("SCHADEN", "")]     // reserviertes Wort
    [InlineData("SCHAEDEN", "")]    // reserviertes Wort
    [InlineData("KEINE", "")]       // reserviertes Wort
    [InlineData("BA-B", "BAB")]     // Sonderzeichen werden entfernt
    [InlineData("BCC", "BCC")]
    [InlineData("BAB_1", "BAB_1")]  // Unterstrich erlaubt
    public void NormalizeCode_ReturnsExpected(string? input, string expected)
        => Assert.Equal(expected, MeasureRecordParser.NormalizeCode(input));

    // ── NormalizeMeasure ─────────────────────────────────────────────

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("Inliner", "Inliner")]
    [InlineData("  - Inliner  ", "Inliner")]
    [InlineData("* Manschette", "Manschette")]
    [InlineData("- * Kurzliner", "Kurzliner")]
    [InlineData("  Reinigung  ", "Reinigung")]
    public void NormalizeMeasure_ReturnsExpected(string? input, string expected)
        => Assert.Equal(expected, MeasureRecordParser.NormalizeMeasure(input));

    // ── ParseMeasures ────────────────────────────────────────────────

    [Fact]
    public void ParseMeasures_Null_ReturnsEmpty()
        => Assert.Empty(MeasureRecordParser.ParseMeasures(null));

    [Fact]
    public void ParseMeasures_SemicolonSeparated_ReturnsSorted()
    {
        var result = MeasureRecordParser.ParseMeasures("Inliner;Manschette;Reinigung");

        Assert.Equal(3, result.Count);
        Assert.Equal("Inliner", result[0]);
        Assert.Equal("Manschette", result[1]);
        Assert.Equal("Reinigung", result[2]);
    }

    [Fact]
    public void ParseMeasures_Deduplicated()
    {
        var result = MeasureRecordParser.ParseMeasures("Inliner;Inliner;inliner");

        Assert.Single(result);
        Assert.Equal("Inliner", result[0]);
    }

    [Fact]
    public void ParseMeasures_NewlineSeparated_Parsed()
    {
        var result = MeasureRecordParser.ParseMeasures("Inliner\nManschette\r\nKurzliner");

        Assert.Equal(3, result.Count);
    }

    // ── ExtractDamageCodes ───────────────────────────────────────────

    [Fact]
    public void ExtractDamageCodes_EmptyRecord_ReturnsEmpty()
    {
        var rec = new HaltungRecord();
        Assert.Empty(MeasureRecordParser.ExtractDamageCodes(rec));
    }

    [Fact]
    public void ExtractDamageCodes_FromPrimaereSchaeden_ReturnsCodes()
    {
        var rec = new HaltungRecord();
        rec.SetFieldValue("Primaere_Schaeden", "BAB Riss\nBAC Bruch", FieldSource.Manual, userEdited: false);

        var codes = MeasureRecordParser.ExtractDamageCodes(rec);

        Assert.Contains("BAB", codes);
        Assert.Contains("BAC", codes);
    }

    [Fact]
    public void ExtractDamageCodes_FromVsaFindings_ReturnsCodes()
    {
        var rec = new HaltungRecord();
        rec.VsaFindings.Add(new VsaFinding { KanalSchadencode = "BAF" });

        var codes = MeasureRecordParser.ExtractDamageCodes(rec);

        Assert.Contains("BAF", codes);
    }

    [Fact]
    public void ExtractDamageCodes_ReturnsSorted()
    {
        var rec = new HaltungRecord();
        rec.SetFieldValue("Primaere_Schaeden", "BAC\nBAB\nBAA", FieldSource.Manual, userEdited: false);

        var codes = MeasureRecordParser.ExtractDamageCodes(rec);

        Assert.Equal(codes.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList(), codes);
    }

    // ── TryParseDecimal ──────────────────────────────────────────────

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("abc", null)]
    [InlineData("1234.56", 1234.56)]
    [InlineData("1234,56", 1234.56)]   // Komma wird zu Punkt
    [InlineData("  99.0  ", 99.0)]
    public void TryParseDecimal_ReturnsExpected(string? input, double? expected)
    {
        var result = MeasureRecordParser.TryParseDecimal(input);
        if (expected is null)
            Assert.Null(result);
        else
            Assert.Equal((decimal)expected, result);
    }

    // ── TryParseInt ──────────────────────────────────────────────────

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("abc", null)]
    [InlineData("5", 5)]
    [InlineData("3.7", 4)]   // rundet kaufmaennisch
    [InlineData("3,2", 3)]   // Komma als Dezimaltrennzeichen
    public void TryParseInt_ReturnsExpected(string? input, int? expected)
        => Assert.Equal(expected, MeasureRecordParser.TryParseInt(input));

    // ── BuildCodeSignature ───────────────────────────────────────────

    [Fact]
    public void BuildCodeSignature_SortedJoined()
    {
        var codes = new List<string> { "BAC", "BAA", "BAB" };
        var sig = MeasureRecordParser.BuildCodeSignature(codes);
        Assert.Equal("BAA;BAB;BAC", sig);
    }

    [Fact]
    public void BuildCodeSignature_AlreadySorted_SameResult()
    {
        var codes = new List<string> { "BAA", "BAB" };
        Assert.Equal("BAA;BAB", MeasureRecordParser.BuildCodeSignature(codes));
    }

    // ── BuildSampleSignature ─────────────────────────────────────────

    [Fact]
    public void BuildSampleSignature_ContainsRecordId()
    {
        var id = Guid.NewGuid();
        var codes = new List<string> { "BAB" };
        var measures = new List<string> { "Inliner" };
        var costs = new MeasureRecordParser.CostSnapshot(1000m, null, null, null, null, null);

        var sig = MeasureRecordParser.BuildSampleSignature(id, codes, measures, costs);

        Assert.Contains(id.ToString("N"), sig);
        Assert.Contains("BAB", sig);
        Assert.Contains("Inliner", sig);
        Assert.Contains("1000.00", sig);
    }

    [Fact]
    public void BuildSampleSignature_NullCosts_ContainsEmptySegments()
    {
        var id = Guid.NewGuid();
        var sig = MeasureRecordParser.BuildSampleSignature(
            id,
            new List<string> { "BAB" },
            new List<string> { "Inliner" },
            new MeasureRecordParser.CostSnapshot(null, null, null, null, null, null));

        // Leerabschnitte fuer nicht vorhandene Kosten
        Assert.Contains("||", sig);
    }

    // ── AverageDecimal ───────────────────────────────────────────────

    [Fact]
    public void AverageDecimal_ZeroCount_ReturnsNull()
        => Assert.Null(MeasureRecordParser.AverageDecimal(100m, 0, 2));

    [Fact]
    public void AverageDecimal_Rounds()
        => Assert.Equal(33.33m, MeasureRecordParser.AverageDecimal(100m, 3, 2));

    // ── AverageInt ───────────────────────────────────────────────────

    [Fact]
    public void AverageInt_ZeroCount_ReturnsNull()
        => Assert.Null(MeasureRecordParser.AverageInt(10, 0));

    [Fact]
    public void AverageInt_RoundsKaufmaennisch()
        => Assert.Equal(4, MeasureRecordParser.AverageInt(11, 3)); // 3.67 -> 4

    // ── SanitizeCosts ────────────────────────────────────────────────

    [Fact]
    public void SanitizeCosts_ValidValues_RetainedUnchanged()
    {
        var input = new MeasureRecordParser.CostSnapshot(500m, 10m, 2, 1, 1, 1);
        var result = MeasureRecordParser.SanitizeCosts(input);

        Assert.Equal(500m, result.TotalCost);
        Assert.Equal(10m, result.InlinerMeters);
        Assert.Equal(2, result.InlinerStk);
    }

    [Fact]
    public void SanitizeCosts_NegativeTotal_ReturnsNull()
    {
        var input = new MeasureRecordParser.CostSnapshot(-1m, null, null, null, null, null);
        var result = MeasureRecordParser.SanitizeCosts(input);

        Assert.Null(result.TotalCost);
    }

    [Fact]
    public void SanitizeCosts_ExcessiveTotal_ReturnsNull()
    {
        var input = new MeasureRecordParser.CostSnapshot(10_000_001m, null, null, null, null, null);
        var result = MeasureRecordParser.SanitizeCosts(input);

        Assert.Null(result.TotalCost);
    }

    [Fact]
    public void SanitizeCosts_ZeroValues_ReturnsNull()
    {
        var input = new MeasureRecordParser.CostSnapshot(0m, 0m, 0, 0, 0, 0);
        var result = MeasureRecordParser.SanitizeCosts(input);

        Assert.Null(result.TotalCost);
        Assert.Null(result.InlinerMeters);
        Assert.Null(result.InlinerStk);
    }
}
