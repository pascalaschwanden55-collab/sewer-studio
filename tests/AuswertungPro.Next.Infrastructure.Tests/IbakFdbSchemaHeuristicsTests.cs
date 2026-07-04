using AuswertungPro.Next.Infrastructure.Import.Ibak;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Charakterisierungstests für IbakFdbSchemaHeuristics — prüfen das IST-Verhalten
/// aller extrahierten pure-static-Methoden ohne jede Datenbankverbindung.
/// </summary>
public sealed class IbakFdbSchemaHeuristicsTests
{
    // ──────────────────────────────────────────────────────────────────────────
    // ContainsAny
    // ──────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("FILENAME", "FILE", true)]
    [InlineData("FILENAME", "DUMMY", false)]
    [InlineData("photo_path", "PATH", true)]
    [InlineData("other", "PATH", false)]
    public void ContainsAny_matches_expected(string text, string key, bool expected)
    {
        Assert.Equal(expected, IbakFdbSchemaHeuristics.ContainsAny(text, key));
    }

    [Fact]
    public void ContainsAny_case_insensitive()
    {
        Assert.True(IbakFdbSchemaHeuristics.ContainsAny("Filename", "FILE"));
        Assert.True(IbakFdbSchemaHeuristics.ContainsAny("FILE", "file"));
    }

    [Fact]
    public void ContainsAny_multiple_keys_first_match_wins()
    {
        Assert.True(IbakFdbSchemaHeuristics.ContainsAny("ROHR", "FILE", "ROHR"));
        Assert.False(IbakFdbSchemaHeuristics.ContainsAny("OTHER", "FILE", "ROHR"));
    }

    // ──────────────────────────────────────────────────────────────────────────
    // FindColumn
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void FindColumn_returns_first_matching_column()
    {
        var cols = new List<string> { "OBJ_ID", "FILENAME", "HALT_NAME" };
        var result = IbakFdbSchemaHeuristics.FindColumn(cols, "FILE", "NAME");
        Assert.Equal("FILENAME", result);
    }

    [Fact]
    public void FindColumn_returns_null_when_no_match()
    {
        var cols = new List<string> { "ID", "CREATED" };
        var result = IbakFdbSchemaHeuristics.FindColumn(cols, "FILE", "NAME");
        Assert.Null(result);
    }

    [Fact]
    public void FindColumn_case_insensitive_match()
    {
        var cols = new List<string> { "filename" };
        var result = IbakFdbSchemaHeuristics.FindColumn(cols, "FILE");
        Assert.Equal("filename", result);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // ExtractHoldingFromPhoto
    // ──────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("L__100-200_001.jpg", "100-200")]
    [InlineData("L_100-200_001.jpg", "100-200")]
    [InlineData("H__100-200_001.jpg", "100-200")]
    [InlineData("H_100-200_001.jpg", "100-200")]
    [InlineData("H_SS 10081-SS 8993_001.jpg", "10081-8993")]
    [InlineData("L__haltung_a_001.jpg", "haltung_a")]
    [InlineData("no_match.jpg", "")]
    [InlineData("", "")]
    public void ExtractHoldingFromPhoto_extracts_correct_key(string fileName, string expected)
    {
        var result = IbakFdbSchemaHeuristics.ExtractHoldingFromPhoto(fileName);
        // NormalizeIbak macht Uppercase und trimmt; daher expected ebenfalls uppercase vergleichen
        Assert.Equal(expected.ToUpperInvariant(), result.ToUpperInvariant());
    }

    // ──────────────────────────────────────────────────────────────────────────
    // ExtractPhotoIndex
    // ──────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("L__100-200_001.jpg", 1)]
    [InlineData("L__100-200_042.jpg", 42)]
    [InlineData("H__haltung_999.png", 999)]
    [InlineData("noindex.jpg", int.MaxValue)]
    public void ExtractPhotoIndex_returns_expected_index(string fileName, int expected)
    {
        Assert.Equal(expected, IbakFdbSchemaHeuristics.ExtractPhotoIndex(fileName));
    }

    // ──────────────────────────────────────────────────────────────────────────
    // PickPhotoTable
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void PickPhotoTable_picks_table_with_photo_in_name_and_file_column()
    {
        var tables = new List<string> { "RECORDS", "PHOTOS", "DAMAGE" };
        var columns = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["RECORDS"] = new() { "ID", "NAME" },
            ["PHOTOS"] = new() { "ID", "FILENAME", "HALT_NAME" },
            ["DAMAGE"] = new() { "ID", "CODE" }
        };

        var result = IbakFdbSchemaHeuristics.PickPhotoTable(tables, columns);

        Assert.Equal("PHOTOS", result);
    }

    [Fact]
    public void PickPhotoTable_returns_null_when_no_table_scores_high_enough()
    {
        var tables = new List<string> { "RECORDS", "DATA" };
        var columns = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["RECORDS"] = new() { "ID", "NAME" },
            ["DATA"] = new() { "CODE", "METER" }
        };

        var result = IbakFdbSchemaHeuristics.PickPhotoTable(tables, columns);

        Assert.Null(result);
    }

    [Fact]
    public void PickPhotoTable_requires_score_at_least_6()
    {
        // Tabelle "MEDIA" = +3; FILE-Spalte = +4 -> Score 7 >= 6 -> soll gefunden werden
        var tables = new List<string> { "MEDIA" };
        var columns = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["MEDIA"] = new() { "FILENAME" }
        };

        var result = IbakFdbSchemaHeuristics.PickPhotoTable(tables, columns);

        Assert.Equal("MEDIA", result);
    }
}
