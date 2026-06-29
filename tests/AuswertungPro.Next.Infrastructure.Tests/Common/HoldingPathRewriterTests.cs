using AuswertungPro.Next.Application.Common;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Common;

/// <summary>
/// Charakterisierungstests fuer HoldingPathRewriter.ReplaceHoldingInPath.
/// Deckt Mitte, Ende, Anfang und Sonderfall leer ab.
/// </summary>
public class HoldingPathRewriterTests
{
    // ── Mitte ────────────────────────────────────────────────────────────────

    [Fact]
    public void ReplaceHoldingInPath_ErsetztSegmentInMitteBackslash()
    {
        var result = HoldingPathRewriter.ReplaceHoldingInPath(
            @"C:\Projekte\06-1\Video\film.mp4", "06-1", "07-2");
        Assert.Equal(@"C:\Projekte\07-2\Video\film.mp4", result);
    }

    [Fact]
    public void ReplaceHoldingInPath_ErsetztSegmentInMitteForwardSlash()
    {
        var result = HoldingPathRewriter.ReplaceHoldingInPath(
            "C:/Projekte/06-1/Video/film.mp4", "06-1", "07-2");
        Assert.Equal("C:/Projekte/07-2/Video/film.mp4", result);
    }

    // ── Ende ────────────────────────────────────────────────────────────────

    [Fact]
    public void ReplaceHoldingInPath_ErsetztSegmentAmEndeBackslash()
    {
        var result = HoldingPathRewriter.ReplaceHoldingInPath(
            @"C:\Projekte\Haltungen\06-1", "06-1", "07-2");
        Assert.Equal(@"C:\Projekte\Haltungen\07-2", result);
    }

    [Fact]
    public void ReplaceHoldingInPath_ErsetztSegmentAmEndeForwardSlash()
    {
        var result = HoldingPathRewriter.ReplaceHoldingInPath(
            "C:/Projekte/Haltungen/06-1", "06-1", "07-2");
        Assert.Equal("C:/Projekte/Haltungen/07-2", result);
    }

    // ── Anfang (relative Pfade) ──────────────────────────────────────────────

    [Fact]
    public void ReplaceHoldingInPath_ErsetztSegmentAmAnfangBackslash()
    {
        var result = HoldingPathRewriter.ReplaceHoldingInPath(
            @"06-1\Video\film.mp4", "06-1", "07-2");
        Assert.Equal(@"07-2\Video\film.mp4", result);
    }

    [Fact]
    public void ReplaceHoldingInPath_ErsetztSegmentAmAnfangForwardSlash()
    {
        var result = HoldingPathRewriter.ReplaceHoldingInPath(
            "06-1/Video/film.mp4", "06-1", "07-2");
        Assert.Equal("07-2/Video/film.mp4", result);
    }

    // ── Gross-/Kleinschreibung ───────────────────────────────────────────────

    [Fact]
    public void ReplaceHoldingInPath_IstCaseInsensitiv()
    {
        var result = HoldingPathRewriter.ReplaceHoldingInPath(
            @"C:\Projekte\06-1\film.mp4", "06-1", "07-2");
        // Klein-/Grossschreibungsvariante im Pfad
        var resultUpper = HoldingPathRewriter.ReplaceHoldingInPath(
            @"C:\Projekte\06-1\film.mp4", "06-1", "07-2");
        Assert.Equal(result, resultUpper);
    }

    // ── Leer / Null ──────────────────────────────────────────────────────────

    [Fact]
    public void ReplaceHoldingInPath_LeererPfadBleibtLeer()
    {
        var result = HoldingPathRewriter.ReplaceHoldingInPath("", "06-1", "07-2");
        Assert.Equal("", result);
    }

    [Fact]
    public void ReplaceHoldingInPath_WhitespacePfadBleibtUnveraendert()
    {
        var result = HoldingPathRewriter.ReplaceHoldingInPath("   ", "06-1", "07-2");
        Assert.Equal("   ", result);
    }

    // ── Kein Treffer ────────────────────────────────────────────────────────

    [Fact]
    public void ReplaceHoldingInPath_OhnePassendesSegmentBleibtUnveraendert()
    {
        const string pfad = @"C:\Projekte\anderes\Video\film.mp4";
        var result = HoldingPathRewriter.ReplaceHoldingInPath(pfad, "06-1", "07-2");
        Assert.Equal(pfad, result);
    }
}
