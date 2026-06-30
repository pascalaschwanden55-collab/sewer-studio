using System.Collections.Generic;
using AuswertungPro.Next.Application.Export;
using AuswertungPro.Next.Infrastructure;
using AuswertungPro.Next.Infrastructure.HoldingDistribution;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>Hilfsmethode fuer Verteilungs-Ergebnis-Tests.</summary>
internal static class DistributionTestFactory
{
    public static HoldingFolderDistributor.DistributionResult MakeResult(
        HoldingFolderDistributor.VideoMatchStatus status,
        bool success = true,
        string src   = "test.pdf",
        string msg   = "ok") =>
        new(success, msg, src, null, null, null, null, null, status);
}

/// <summary>
/// Charakterisierungs-Tests fuer DistributionSummaryBuilder und StoredFileListParser.
/// </summary>
public class DistributionSummaryBuilderTests
{
    // ── IsDataSidecar ────────────────────────────────────────────────────────

    [Theory]
    [InlineData("export.xtf",  true)]
    [InlineData("export.XTF",  true)]
    [InlineData("data.m150",   true)]
    [InlineData("data.M150",   true)]
    [InlineData("db.mdb",      true)]
    [InlineData("meta.xml",    true)]
    [InlineData("bericht.pdf", false)]
    [InlineData("video.mp4",   false)]
    [InlineData("notes.txt",   false)]
    public void IsDataSidecar_erkennt_bekannte_Erweiterungen(string path, bool expected)
    {
        Assert.Equal(expected, DistributionSummaryBuilder.IsDataSidecar(path));
    }

    // ── PreviewRank ───────────────────────────────────────────────────────────

    [Fact]
    public void PreviewRank_Matched_ist_kleiner_als_Ambiguous()
    {
        var matched   = DistributionTestFactory.MakeResult(HoldingFolderDistributor.VideoMatchStatus.Matched);
        var ambiguous = DistributionTestFactory.MakeResult(HoldingFolderDistributor.VideoMatchStatus.Ambiguous);
        Assert.True(DistributionSummaryBuilder.PreviewRank(matched) < DistributionSummaryBuilder.PreviewRank(ambiguous));
    }

    [Fact]
    public void PreviewRank_Ambiguous_ist_kleiner_als_NotFound()
    {
        var ambiguous = DistributionTestFactory.MakeResult(HoldingFolderDistributor.VideoMatchStatus.Ambiguous);
        var notFound  = DistributionTestFactory.MakeResult(HoldingFolderDistributor.VideoMatchStatus.NotFound);
        Assert.True(DistributionSummaryBuilder.PreviewRank(ambiguous) < DistributionSummaryBuilder.PreviewRank(notFound));
    }

    // ── BuildHoldingDistributionSummary ──────────────────────────────────────

    [Fact]
    public void BuildHoldingDistributionSummary_PdfModus_enthaelt_Modus_und_Zaehler()
    {
        var results = new List<HoldingFolderDistributor.DistributionResult>
        {
            DistributionTestFactory.MakeResult(HoldingFolderDistributor.VideoMatchStatus.Matched,  success: true,  src: "A.pdf"),
            DistributionTestFactory.MakeResult(HoldingFolderDistributor.VideoMatchStatus.NotFound, success: false, src: "B.pdf"),
        };

        var text = DistributionSummaryBuilder.BuildHoldingDistributionSummary(results, useTxtImport: false);

        Assert.Contains("Modus: PDF-Import", text);
        Assert.Contains("Verarbeitet: 2", text);
        Assert.Contains("OK: 1", text);
        Assert.Contains("Fehler: 1", text);
        Assert.Contains("Matched 1", text);
        Assert.Contains("Missing 1", text);
    }

    [Fact]
    public void BuildHoldingDistributionSummary_TxtModus_enthaelt_TxtImport()
    {
        var results = new List<HoldingFolderDistributor.DistributionResult>
        {
            DistributionTestFactory.MakeResult(HoldingFolderDistributor.VideoMatchStatus.Matched, success: true, src: "kins.txt"),
        };

        var text = DistributionSummaryBuilder.BuildHoldingDistributionSummary(results, useTxtImport: true);

        Assert.Contains("Modus: TXT-Import", text);
        Assert.Contains("Verarbeitet: 1", text);
    }

    [Fact]
    public void BuildHoldingDistributionSummary_Sidecar_wird_als_XTF_Zeile_ausgegeben()
    {
        var results = new List<HoldingFolderDistributor.DistributionResult>
        {
            DistributionTestFactory.MakeResult(HoldingFolderDistributor.VideoMatchStatus.Matched,    success: true, src: "H1.pdf"),
            DistributionTestFactory.MakeResult(HoldingFolderDistributor.VideoMatchStatus.NotChecked, success: true, src: "layer.xtf"),
        };

        var text = DistributionSummaryBuilder.BuildHoldingDistributionSummary(results, useTxtImport: false);

        Assert.Contains("XTF/M150/MDB/XML:", text);
    }

    [Fact]
    public void BuildHoldingDistributionSummary_leere_Liste_ergibt_OK_0_Fehler_0()
    {
        var text = DistributionSummaryBuilder.BuildHoldingDistributionSummary(
            new List<HoldingFolderDistributor.DistributionResult>(), useTxtImport: false);

        Assert.Contains("OK: 0", text);
        Assert.Contains("Fehler: 0", text);
    }

    // ── BuildShaftDistributionSummary ─────────────────────────────────────────

    [Fact]
    public void BuildShaftDistributionSummary_enthaelt_Schachtprotokolle_Kopfzeile()
    {
        var results = new List<HoldingFolderDistributor.DistributionResult>
        {
            DistributionTestFactory.MakeResult(HoldingFolderDistributor.VideoMatchStatus.NotChecked, success: true,  src: "S1.pdf"),
            DistributionTestFactory.MakeResult(HoldingFolderDistributor.VideoMatchStatus.NotChecked, success: false, src: "S2.pdf"),
        };

        var text = DistributionSummaryBuilder.BuildShaftDistributionSummary(results);

        Assert.Contains("Schachtprotokolle: 2", text);
        Assert.Contains("OK: 1", text);
        Assert.Contains("Fehler: 1", text);
    }

    // ── BuildDichtheitDistributionSummary ────────────────────────────────────

    [Fact]
    public void BuildDichtheitDistributionSummary_enthaelt_Dichtheitspruefung_Kopfzeile()
    {
        var results = new List<HoldingFolderDistributor.DistributionResult>
        {
            DistributionTestFactory.MakeResult(HoldingFolderDistributor.VideoMatchStatus.NotChecked, success: true, src: "DP1.pdf"),
        };

        var text = DistributionSummaryBuilder.BuildDichtheitDistributionSummary(results);

        Assert.Contains("Dichtheitsprüfung: 1", text);
        Assert.Contains("OK: 1", text);
        Assert.Contains("Fehler: 0", text);
    }
}

/// <summary>
/// Charakterisierungs-Tests fuer StoredFileListParser.
/// </summary>
public class StoredFileListParserTests
{
    [Fact]
    public void Parse_null_gibt_leere_Liste_zurueck()
    {
        var result = StoredFileListParser.Parse(null);
        Assert.Empty(result);
    }

    [Fact]
    public void Parse_leer_gibt_leere_Liste_zurueck()
    {
        var result = StoredFileListParser.Parse("   ");
        Assert.Empty(result);
    }

    [Fact]
    public void Parse_gueltige_Json_Liste_wird_korrekt_deserialisiert()
    {
        var raw    = """["Imports/PDF/A.pdf","Imports/PDF/B.pdf"]""";
        var result = StoredFileListParser.Parse(raw);
        Assert.Equal(2, result.Count);
        Assert.Contains("Imports/PDF/A.pdf", result);
        Assert.Contains("Imports/PDF/B.pdf", result);
    }

    [Fact]
    public void Parse_Json_Liste_filtert_leere_Eintraege()
    {
        var raw    = """["Imports/PDF/A.pdf","","  ","Imports/PDF/B.pdf"]""";
        var result = StoredFileListParser.Parse(raw);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void Parse_Semikolon_Fallback_wird_korrekt_gesplittet()
    {
        var raw    = "Imports/PDF/A.pdf;Imports/PDF/B.pdf";
        var result = StoredFileListParser.Parse(raw);
        Assert.Equal(2, result.Count);
        Assert.Contains("Imports/PDF/A.pdf", result);
    }

    [Fact]
    public void Parse_Semikolon_Fallback_filtert_leere_Segmente()
    {
        var raw    = "Imports/PDF/A.pdf;;Imports/PDF/B.pdf;";
        var result = StoredFileListParser.Parse(raw);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void Parse_trimmte_Leerzeichen_aus_Eintraegen()
    {
        var raw    = """["  Imports/PDF/A.pdf  ","Imports/PDF/B.pdf"]""";
        var result = StoredFileListParser.Parse(raw);
        Assert.Contains("Imports/PDF/A.pdf", result);
    }
}

