using System.Collections.Generic;
using AuswertungPro.Next.Application.DataPage;
using AuswertungPro.Next.Domain.Models;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Charakterisierungstests fuer ProtocolPathResolver und PdfCandidateSelector
/// (reine Application-Logik, kein IO).
/// </summary>
public sealed class ProtocolPathResolverTests
{
    // -------------------------------------------------------------------------
    // PdfCandidateSelector.BuildHoldingTokens (Token-Erzeugung aus rohem Namen)
    // -------------------------------------------------------------------------

    [Fact]
    public void BuildHoldingTokens_EinfacherName_GibtEinenToken()
    {
        // Name ohne Sonderzeichen: sanitisiert == roh → Distinct liefert genau einen Token
        var tokens = PdfCandidateSelector.BuildHoldingTokens("HLT123");

        Assert.Single(tokens);
        Assert.Equal("HLT123", tokens[0]);
    }

    [Fact]
    public void BuildHoldingTokens_NameMitDoppelpunkt_SanitisiertUndRohVerschieden()
    {
        // Doppelpunkt ist kein gueltiges Dateinamen-Zeichen → wird zu "_"
        var tokens = PdfCandidateSelector.BuildHoldingTokens("HLT:123");

        Assert.Equal(2, tokens.Count);
        Assert.Contains("HLT_123", tokens);   // sanitisierter Token
        Assert.Contains("HLT:123", tokens);   // Rohname
    }

    [Fact]
    public void BuildHoldingTokens_LeerString_GibtLeereListeZurueck()
    {
        var tokens = PdfCandidateSelector.BuildHoldingTokens("");

        Assert.Empty(tokens);
    }

    [Fact]
    public void BuildHoldingTokens_NullName_GibtLeereListeZurueck()
    {
        var tokens = PdfCandidateSelector.BuildHoldingTokens(null);

        Assert.Empty(tokens);
    }

    // -------------------------------------------------------------------------
    // ProtocolPathResolver.BuildHoldingTokens (Record-Wrapper)
    // -------------------------------------------------------------------------

    [Fact]
    public void ProtocolPathResolver_BuildHoldingTokens_LesbarAusRecord()
    {
        var record = MakeRecord("865-864");

        var tokens = ProtocolPathResolver.BuildHoldingTokens(record);

        Assert.Contains("865-864", tokens);
        Assert.True(tokens.Count >= 1);
    }

    [Fact]
    public void ProtocolPathResolver_BuildHoldingTokens_FehlendesHaltungsnameGibtLeereListe()
    {
        var record = MakeRecord("");

        var tokens = ProtocolPathResolver.BuildHoldingTokens(record);

        Assert.Empty(tokens);
    }

    // -------------------------------------------------------------------------
    // PdfCandidateSelector.PickBest
    // -------------------------------------------------------------------------

    [Fact]
    public void PickBest_KeinKandidat_GibtNullZurueck()
    {
        var result = PdfCandidateSelector.PickBest(
            new List<string>(),
            new[] { "865-864" });

        Assert.Null(result);
    }

    [Fact]
    public void PickBest_ExakterSuffixTreffer_WirdBevorzugt()
    {
        var candidates = new[]
        {
            @"C:\Projekte\Allgemein\irgendwas.pdf",
            @"C:\Projekte\Haltungen\865-864\Protokoll_865-864.pdf",
        };
        var tokens = new[] { "865-864" };

        var result = PdfCandidateSelector.PickBest(candidates, tokens);

        Assert.Equal(@"C:\Projekte\Haltungen\865-864\Protokoll_865-864.pdf", result);
    }

    [Fact]
    public void PickBest_KeinSuffixTreffer_NimmtLexikografischLetzten()
    {
        var candidates = new[]
        {
            @"C:\Projekte\A_Report.pdf",
            @"C:\Projekte\Z_Report.pdf",
            @"C:\Projekte\M_Report.pdf",
        };
        var tokens = new[] { "999-999" };  // kein Treffer

        var result = PdfCandidateSelector.PickBest(candidates, tokens);

        // Lexikografisch letzter Dateiname: Z_Report.pdf
        Assert.Equal(@"C:\Projekte\Z_Report.pdf", result);
    }

    [Fact]
    public void PickBest_MehrereTokens_ErstesMatchGewinnt()
    {
        var candidates = new[]
        {
            @"C:\P\Protokoll_865-864.pdf",
            @"C:\P\Protokoll_sanitized.pdf",
        };
        // Erster Token matcht "sanitized", zweiter "865-864"
        var tokens = new[] { "sanitized", "865-864" };

        var result = PdfCandidateSelector.PickBest(candidates, tokens);

        Assert.Equal(@"C:\P\Protokoll_sanitized.pdf", result);
    }

    [Fact]
    public void PickBest_DuplikatePfade_WerdenIgnoriert()
    {
        var candidates = new[]
        {
            @"C:\P\Protokoll_865-864.pdf",
            @"C:\P\Protokoll_865-864.pdf",  // Duplikat
        };
        var tokens = new[] { "865-864" };

        var result = PdfCandidateSelector.PickBest(candidates, tokens);

        Assert.Equal(@"C:\P\Protokoll_865-864.pdf", result);
    }

    // -------------------------------------------------------------------------
    // PdfCandidateSelector.ParseStoredPathList
    // -------------------------------------------------------------------------

    [Fact]
    public void ParseStoredPathList_LeerString_GibtLeereListe()
    {
        var result = PdfCandidateSelector.ParseStoredPathList("");

        Assert.Empty(result);
    }

    [Fact]
    public void ParseStoredPathList_GueltigesJsonArray_ParsetKorrekt()
    {
        var raw = @"[""/pfad/a.pdf"","" /pfad/b.pdf ""]";

        var result = PdfCandidateSelector.ParseStoredPathList(raw);

        Assert.Equal(2, result.Count);
        Assert.Equal("/pfad/a.pdf", result[0]);
        Assert.Equal("/pfad/b.pdf", result[1]);
    }

    [Fact]
    public void ParseStoredPathList_SemikolonGetrennt_Fallback()
    {
        var raw = @"C:\a.pdf;C:\b.pdf; C:\c.pdf ";

        var result = PdfCandidateSelector.ParseStoredPathList(raw);

        Assert.Equal(3, result.Count);
        Assert.Equal(@"C:\a.pdf", result[0]);
        Assert.Equal(@"C:\b.pdf", result[1]);
        Assert.Equal(@"C:\c.pdf", result[2]);
    }

    [Fact]
    public void ParseStoredPathList_LeereEintraegeWerdenIgnoriert()
    {
        var raw = @"C:\a.pdf;;; C:\b.pdf ";

        var result = PdfCandidateSelector.ParseStoredPathList(raw);

        Assert.Equal(2, result.Count);
    }

    // -------------------------------------------------------------------------
    // Hilfsmethoden
    // -------------------------------------------------------------------------

    private static HaltungRecord MakeRecord(string haltungsname)
    {
        var record = new HaltungRecord();
        record.SetFieldValue("Haltungsname", haltungsname, FieldSource.Manual, false);
        return record;
    }
}
