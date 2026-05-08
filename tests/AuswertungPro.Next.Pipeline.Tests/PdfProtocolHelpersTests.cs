using AuswertungPro.Next.Application.Ai.Training.Services;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Tests fuer <see cref="PdfProtocolHelpers"/>. Enthaelt Heuristiken zur
/// Erkennung Nicht-Inspektions-PDF (Rechnungen/DP) und einen Caesar-Decoder
/// fuer IKAS-PDFs mit verschobenen Zeichen aus falschen CMaps.
/// </summary>
[Trait("Category", "Unit")]
public class PdfProtocolHelpersTests
{
    [Fact]
    public void TryDecodeShiftedText_NormalText_PassesThrough()
    {
        // Text enthaelt bereits viele bekannte Worte → kein Decoding noetig
        var input = "Inspektion Haltung 12.34m Material PE Kreisprofil Zustand 3";
        var result = PdfProtocolHelpers.TryDecodeShiftedText(input);
        Assert.Equal(input, result);
    }

    [Fact]
    public void TryDecodeShiftedText_NullOrEmpty_ReturnsAsIs()
    {
        Assert.Null(PdfProtocolHelpers.TryDecodeShiftedText(null!));
        Assert.Equal("", PdfProtocolHelpers.TryDecodeShiftedText(""));
        Assert.Equal("   ", PdfProtocolHelpers.TryDecodeShiftedText("   "));
    }

    [Fact]
    public void TryDecodeShiftedText_ShiftedDownByOne_DecodesBack()
    {
        // IKAS-CMap-Bug: Zeichen werden NACH UNTEN verschoben. Der Decoder
        // korrigiert nach oben (shift +1..+60). Test simuliert den Bug.
        var original = "Inspektion Haltung Material Foto Schacht";
        var shifted = ShiftAllChars(original, -1); // Bug: jedes Zeichen -1

        var result = PdfProtocolHelpers.TryDecodeShiftedText(shifted);

        Assert.Equal(original, result);
    }

    [Fact]
    public void TryDecodeShiftedText_ShiftedDownByThree_DecodesBack()
    {
        var original = "Leitung Video Kanal Schacht Inspektion Material Profil";
        var shifted = ShiftAllChars(original, -3);

        var result = PdfProtocolHelpers.TryDecodeShiftedText(shifted);
        Assert.Equal(original, result);
    }

    [Fact]
    public void TryDecodeShiftedText_NoMatch_ReturnsOriginal()
    {
        // Ein Text ohne erkennbare Worte und auch kein erkennbarer Shift
        var input = "abcdefghijklmnopqrstuvwxyz0123456789";
        var result = PdfProtocolHelpers.TryDecodeShiftedText(input);
        Assert.Equal(input, result);
    }

    [Theory]
    [InlineData("Faktura.pdf", true)]
    [InlineData("Rechnung-2026.pdf", true)]
    [InlineData("Offerte_Maxmuller.pdf", true)]
    [InlineData("Mahnung-2nd.pdf", true)]
    [InlineData("Linerdatenblatt.pdf", true)]
    [InlineData("Dichtheitspruefung_2026.pdf", true)]
    [InlineData("Haltung_DP.pdf", true)]
    [InlineData("123_dp_xy.pdf", true)]
    [InlineData("Inspektionsprotokoll.pdf", false)]
    [InlineData("Haltung_42_protokoll.pdf", false)]
    public void NonProtocolKeywords_KnownFilenames_Match(string filename, bool isNonProtocol)
    {
        var lower = filename.ToLowerInvariant();
        var matches = false;
        foreach (var kw in PdfProtocolHelpers.NonProtocolKeywords)
        {
            if (lower.Contains(kw))
            {
                matches = true;
                break;
            }
        }
        Assert.Equal(isNonProtocol, matches);
    }

    [Theory]
    [InlineData("Dichtheitspruefung gemaess SIA 190", true)]
    [InlineData("Prüfdruck 0.5 bar", true)]            // Marker enthaelt Umlaut
    [InlineData("Luftpruefung an Haltung 12", true)]
    [InlineData("Inspektion Haltung 1, Material PE", false)]
    public void NonProtocolTextMarkers_DetectInPdfText(string content, bool shouldMatch)
    {
        var lower = content.ToLowerInvariant();
        var matches = false;
        foreach (var marker in PdfProtocolHelpers.NonProtocolTextMarkers)
        {
            if (lower.Contains(marker))
            {
                matches = true;
                break;
            }
        }
        Assert.Equal(shouldMatch, matches);
    }

    /// <summary>Helper fuer Caesar-Shift im Test (gleiche Logik wie der Decoder).</summary>
    private static string ShiftAllChars(string text, int shift)
    {
        var sb = new System.Text.StringBuilder(text.Length);
        foreach (var ch in text)
        {
            if (ch == '\r' || ch == '\n' || ch == '\t' || ch == ' ')
                sb.Append(ch);
            else
                sb.Append((char)(ch + shift));
        }
        return sb.ToString();
    }
}
