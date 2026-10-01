using System.Collections.Generic;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Vsa;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Charakterisierungstests fuer PrimaryDamageParser.
/// Pruefen das IST-Verhalten der reinen Parse-Logik.
/// </summary>
public sealed class PrimaryDamageParserTests
{
    // ── NormalizeCode ─────────────────────────────────────────────────────

    [Theory]
    [InlineData("BAJ.C @0.10m", "BAJC")]
    [InlineData("BAF.A.A", "BAFAA")]
    [InlineData("BCD", "BCD")]
    [InlineData("  bab  ", "BAB")]
    [InlineData("BAB(laengs)", "BAB")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void NormalizeCode_GibtNormalisiertesErgebnis(string? raw, string expected)
    {
        Assert.Equal(expected, PrimaryDamageParser.NormalizeCode(raw));
    }

    // ── ExtractQuantValue ─────────────────────────────────────────────────

    [Theory]
    [InlineData("Riss laengs, Breite = 4mm, geschaetzt", "4")]
    [InlineData("Rohrverbindung Knick, Winkel = 10°, geschaetzt", "10")]
    [InlineData("Einragendes Dichtungsmaterial, Querschnittsreduzierung = 3%", "3")]
    [InlineData("Riss radial, Breite = 1,5 mm", "1.5")]
    [InlineData("kein Messwert vorhanden", null)]
    [InlineData(null, null)]
    public void ExtractQuantValue_LiestMesswertAusRohtext(string? raw, string? expected)
    {
        Assert.Equal(expected, PrimaryDamageParser.ExtractQuantValue(raw));
    }

    // ── ParseFindingsFromPrimaryDamage ────────────────────────────────────

    [Fact]
    public void ParseFindings_LiefertLeereListe_WennNullOderLeer()
    {
        var knownCodes = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase) { "BAB" };
        Assert.Empty(PrimaryDamageParser.ParseFindingsFromPrimaryDamage(null, knownCodes));
        Assert.Empty(PrimaryDamageParser.ParseFindingsFromPrimaryDamage("", knownCodes));
    }

    [Fact]
    public void ParseFindings_ExtrahiertBekanntenCode()
    {
        var knownCodes = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase) { "BAB", "BAF" };
        var raw = "BAF.A.A @12.3m (Korrosion)\nBAB @2.0m (Riss laengs)";
        var findings = PrimaryDamageParser.ParseFindingsFromPrimaryDamage(raw, knownCodes);
        Assert.Equal(2, findings.Count);
        Assert.Equal("BAFAA", findings[0].KanalSchadencode);
        Assert.Equal("BAB", findings[1].KanalSchadencode);
    }

    [Fact]
    public void ParseFindings_UebergehtUnbekanntenCode_WennKnownCodesGefuellt()
    {
        // Nur bekannte Codes sollen extrahiert werden
        var knownCodes = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase) { "BAB" };
        var raw = "ZZZ @1.0m (unbekannt)\nBAB @2.0m (Riss)";
        var findings = PrimaryDamageParser.ParseFindingsFromPrimaryDamage(raw, knownCodes);
        Assert.Single(findings);
        Assert.Equal("BAB", findings[0].KanalSchadencode);
    }

    // ── EnrichFindingsFromPrimaryDamage ───────────────────────────────────

    [Fact]
    public void EnrichFindings_ErgaenztBasiscodeMitVollemCode()
    {
        // BAJ (Basiscode) wird durch BAJ.C aus dem Primaerschadentext ersetzt
        var findings = new List<VsaFinding>
        {
            new()
            {
                KanalSchadencode = "BAJ",
                SchadenlageAnfang = 0.10,
                Raw = "Rohrverbindung Knick, Winkel = 10°, an Verbindung"
            }
        };
        var primaryDamageText = "BAJ.C @0.10m (Rohrverbindung Knick)";

        var result = new List<VsaFinding>(
            PrimaryDamageParser.EnrichFindingsFromPrimaryDamage(findings, primaryDamageText));

        Assert.Single(result);
        Assert.Equal("BAJC", result[0].KanalSchadencode);
        Assert.Equal("10", result[0].Quantifizierung1);
    }

    [Fact]
    public void EnrichFindings_BehaeltOriginalCode_WennKeinPassenderKandidat()
    {
        // BDA hat keinen passenden erweiterten Code im Primaerschadentext
        var findings = new List<VsaFinding>
        {
            new() { KanalSchadencode = "BDA", SchadenlageAnfang = 0.90, Raw = "" }
        };
        var primaryDamageText = "BDA @0.90m (Allgemeinzustand)";

        // BDA wird nicht erweitert (Kandidat hat gleiche Laenge wie Basiscode)
        var result = new List<VsaFinding>(
            PrimaryDamageParser.EnrichFindingsFromPrimaryDamage(findings, primaryDamageText));

        Assert.Single(result);
        Assert.Equal("BDA", result[0].KanalSchadencode);
    }

    [Fact]
    public void EnrichFindings_VerwendetUhrlageNichtAlsMeterstand()
    {
        var findings = new List<VsaFinding>
        {
            new()
            {
                KanalSchadencode = "BAJ",
                MeterStart = null,
                SchadenlageAnfang = 9,
                Raw = "Rohrverbindung Knick"
            }
        };
        var primaryDamageText = "BAJ.C @9.00m (Knick A)\nBAJ.D @4.00m (Knick B)";

        var result = new List<VsaFinding>(
            PrimaryDamageParser.EnrichFindingsFromPrimaryDamage(findings, primaryDamageText));

        Assert.Single(result);
        Assert.Equal("BAJ", result[0].KanalSchadencode);
    }

    // ── CopyFinding ───────────────────────────────────────────────────────

    [Fact]
    public void CopyFinding_KopiertAlleFelder()
    {
        var source = new VsaFinding
        {
            KanalSchadencode = "BAB",
            Quantifizierung1 = "old",
            Quantifizierung2 = "Q2",
            SchadenlageAnfang = 1.0,
            SchadenlageEnde = 5.0,
            LL = 4.0,
            Raw = "raw text",
            MeterStart = 1.0,
            MeterEnd = 5.0,
            EZD = 2,
            EZS = 3,
            EZB = 1
        };

        var copy = PrimaryDamageParser.CopyFinding(source, "BABB", "newQ1");

        Assert.Equal("BABB", copy.KanalSchadencode);
        Assert.Equal("newQ1", copy.Quantifizierung1);
        Assert.Equal("Q2", copy.Quantifizierung2);
        Assert.Equal(1.0, copy.SchadenlageAnfang);
        Assert.Equal(5.0, copy.SchadenlageEnde);
        Assert.Equal(4.0, copy.LL);
        Assert.Equal("raw text", copy.Raw);
        Assert.Equal(2, copy.EZD);
        Assert.Equal(3, copy.EZS);
        Assert.Equal(1, copy.EZB);
    }
}
