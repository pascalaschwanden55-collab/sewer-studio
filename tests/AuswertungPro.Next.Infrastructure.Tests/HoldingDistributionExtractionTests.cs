using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using AuswertungPro.Next.Infrastructure.HoldingDistribution;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Charakterisierungs-Tests fuer die aus HoldingFolderDistributor extrahierten Klassen:
/// HoldingTextParser, ShaftCandidateScanner, PhotoTokenNormalizer, HoldingKeyUtils, KinsTxtHeaderParser.
/// </summary>
public sealed class HoldingDistributionExtractionTests
{
    // ── HoldingTextParser ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData("02.05.2023", 2023, 5, 2)]
    [InlineData("2023-05-02", 2023, 5, 2)]
    public void HoldingTextParser_TryFindInspectionDate_FindsDateInText(string dateStr, int year, int month, int day)
    {
        var text = $"Haltungsinspektion - {dateStr} - 12345-67890";
        var result = HoldingTextParser.TryFindInspectionDate(text);
        Assert.NotNull(result);
        Assert.Equal(new DateTime(year, month, day), result!.Value);
    }

    [Fact]
    public void HoldingTextParser_TryFindInspectionDate_ReturnsNull_WhenNoDate()
    {
        var result = HoldingTextParser.TryFindInspectionDate("Kein Datum hier.");
        Assert.Null(result);
    }

    [Fact]
    public void HoldingTextParser_TryFindSchachtDate_FindsLabeledDate()
    {
        var text = "Datum: 15.03.2021\nSonstige Zeile";
        var result = HoldingTextParser.TryFindSchachtDate(text);
        Assert.NotNull(result);
        Assert.Equal(new DateTime(2021, 3, 15), result!.Value);
    }

    [Fact]
    public void HoldingTextParser_TryFindSchachtDate_ReturnsNull_WhenNoDate()
    {
        var result = HoldingTextParser.TryFindSchachtDate("Kein Datum hier.");
        Assert.Null(result);
    }

    [Fact]
    public void HoldingTextParser_FindNearbyDate_FindsDateInAdjacentLine()
    {
        var dateRx = new Regex(@"(\d{2}\.\d{2}\.\d{2,4})", RegexOptions.Compiled);
        var lines = new[] { "Inspektionsdatum", "15.03.2021" };
        var result = HoldingTextParser.FindNearbyDate(lines, 1, 1, 3, dateRx);
        Assert.NotNull(result);
        Assert.Equal(new DateTime(2021, 3, 15), result!.Value);
    }

    [Fact]
    public void HoldingTextParser_TryFindSchachtNumber_FindsSchachtnummer()
    {
        var text = "Zustandsaufnahme Schacht Nr: 12345\nAndere Zeile";
        var result = HoldingTextParser.TryFindSchachtNumber(text);
        Assert.Equal("12345", result);
    }

    [Fact]
    public void HoldingTextParser_TryFindSchachtNumber_ReturnsNull_WhenMissing()
    {
        var result = HoldingTextParser.TryFindSchachtNumber("Kein Schacht hier.");
        Assert.Null(result);
    }

    [Fact]
    public void HoldingTextParser_NormalizeShaftNumberKey_StripsNonDigitsAndLeadingZeros()
    {
        Assert.Equal("123", HoldingTextParser.NormalizeShaftNumberKey("00123"));
        Assert.Equal("456", HoldingTextParser.NormalizeShaftNumberKey("AB456"));
        Assert.Equal(string.Empty, HoldingTextParser.NormalizeShaftNumberKey(null));
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("kein-paar.pdf", null)]
    public void HoldingTextParser_TryExtractHaltungFromPdfPath_ReturnsNull_WhenNoValidPair(string? path, string? expected)
    {
        var result = HoldingTextParser.TryExtractHaltungFromPdfPath(path);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void HoldingTextParser_IsSuspiciousShaftPair_DetectsRepeatedNode()
    {
        Assert.True(HoldingTextParser.IsSuspiciousShaftPair("12345-12345", "12345-67890"));
    }

    [Fact]
    public void HoldingTextParser_IsSuspiciousShaftPair_ReturnsFalse_WhenValid()
    {
        Assert.False(HoldingTextParser.IsSuspiciousShaftPair("12345-67890", "12345-67890"));
    }

    [Fact]
    public void HoldingTextParser_TryParseKsCompactHoldingDigits_ParsesTenDigit()
    {
        // 10 Ziffern: erste 5 = Knoten A, letzte 5 = Knoten B
        var result = HoldingTextParser.TryParseKsCompactHoldingDigits("2163220523");
        // Muss ein gueltiges Haltungs-Paar sein (5-5 Aufteilung)
        Assert.NotNull(result);
    }

    [Fact]
    public void HoldingTextParser_TryParseKsCompactHoldingDigits_ReturnsNull_ForShortInput()
    {
        var result = HoldingTextParser.TryParseKsCompactHoldingDigits("123");
        Assert.Null(result);
    }

    [Fact]
    public void HoldingTextParser_TryFindHaltungId_FindsHaltungInObererUntererPattern()
    {
        var text = "Oberer Punkt 42046\nUnterer Punkt 41412";
        var result = HoldingTextParser.TryFindHaltungId(text);
        Assert.NotNull(result);
        Assert.Contains("42046", result);
        Assert.Contains("41412", result);
    }

    // ── ShaftCandidateScanner ─────────────────────────────────────────────────────

    [Fact]
    public void ShaftCandidateScanner_IsNoiseLine_ReturnsTrueForTelefonzeile()
    {
        Assert.True(ShaftCandidateScanner.IsNoiseLine("Telefon: +41 (0)41 440 42 02"));
        Assert.True(ShaftCandidateScanner.IsNoiseLine("GPS: 47.123 8.456"));
        Assert.True(ShaftCandidateScanner.IsNoiseLine("Prüfdruck: 350 mbar"));
    }

    [Fact]
    public void ShaftCandidateScanner_IsNoiseLine_ReturnsFalseForNormalzeile()
    {
        Assert.False(ShaftCandidateScanner.IsNoiseLine("Schacht oben: 42046"));
        Assert.False(ShaftCandidateScanner.IsNoiseLine("Haltung: 42046-41412"));
    }

    [Fact]
    public void ShaftCandidateScanner_AddNumberTokens_ExtractsNumbers()
    {
        var nums = new List<string>();
        ShaftCandidateScanner.AddNumberTokens("Schacht 42046 und 41412", nums);
        Assert.Contains("42046", nums);
        Assert.Contains("41412", nums);
    }

    [Fact]
    public void ShaftCandidateScanner_AddNumberTokens_SkipsNoiseLine()
    {
        var nums = new List<string>();
        ShaftCandidateScanner.AddNumberTokens("Telefon: +41 (0)41 440 42 02", nums);
        Assert.Empty(nums);
    }

    [Fact]
    public void ShaftCandidateScanner_GatherShaftCandidates_FindsNumbersOnHaltungLine()
    {
        var text = "Haltung: 42046-41412\nSonstige Zeile";
        var result = ShaftCandidateScanner.GatherShaftCandidates(text);
        Assert.NotEmpty(result);
        Assert.Contains("42046", result);
    }

    [Fact]
    public void ShaftCandidateScanner_GatherAllNumberCandidates_FindsAllNumbers()
    {
        var text = "Zeile 1: 42046\nZeile 2: 41412";
        var result = ShaftCandidateScanner.GatherAllNumberCandidates(text);
        Assert.Contains("42046", result);
        Assert.Contains("41412", result);
    }

    [Fact]
    public void ShaftCandidateScanner_TryExtractFromHeader_ParsesHaltungsinspektion()
    {
        var text = "Haltungsinspektion - 15.03.2021 - 42046-41412";
        var result = ShaftCandidateScanner.TryExtractFromHeader(text);
        Assert.NotNull(result);
        Assert.Contains("42046", result);
    }

    [Fact]
    public void ShaftCandidateScanner_TryExtractFromHeader_ReturnsNull_WhenNotHeader()
    {
        var result = ShaftCandidateScanner.TryExtractFromHeader("Kein Header hier");
        Assert.Null(result);
    }

    [Fact]
    public void ShaftCandidateScanner_TryExtractFromShafts_ParsesObererUnterer()
    {
        var text = "Oberer Schacht: 42046\nUnterer Schacht: 41412";
        var result = ShaftCandidateScanner.TryExtractFromShafts(text);
        Assert.NotNull(result);
        Assert.Contains("42046", result);
        Assert.Contains("41412", result);
    }

    [Fact]
    public void ShaftCandidateScanner_LooksLikeDateFragment_DetectsDateFragment()
    {
        Assert.True(ShaftCandidateScanner.LooksLikeDateFragment("09.2025-80638"));
        Assert.False(ShaftCandidateScanner.LooksLikeDateFragment("42046-41412"));
    }

    [Fact]
    public void ShaftCandidateScanner_FindNextToken_FindsInNextLine()
    {
        var lines = new[] { "Leitung", "42046-41412", "Sonstiges" };
        var result = ShaftCandidateScanner.FindNextToken(lines, 1, @"\d+");
        Assert.Equal("42046", result);
    }

    [Fact]
    public void ShaftCandidateScanner_TryFindPoint_FindsObererPunkt()
    {
        var lines = new[] { "Oberer Punkt:", "42046" };
        var result = ShaftCandidateScanner.TryFindPoint(lines, "Oberer");
        Assert.Equal("42046", result);
    }

    // ── PhotoTokenNormalizer ──────────────────────────────────────────────────────

    [Fact]
    public void PhotoTokenNormalizer_TrimLeadingZerosValue_TrimsZeros()
    {
        Assert.Equal("123", PhotoTokenNormalizer.TrimLeadingZerosValue("0000123"));
        Assert.Equal("0", PhotoTokenNormalizer.TrimLeadingZerosValue("000"));
        Assert.Equal("1", PhotoTokenNormalizer.TrimLeadingZerosValue("001"));
    }

    [Fact]
    public void PhotoTokenNormalizer_NormalizePhotoToken_ReturnsNull_WhenPatternMismatch()
    {
        Assert.Null(PhotoTokenNormalizer.NormalizePhotoToken("kein_muster"));
        Assert.Null(PhotoTokenNormalizer.NormalizePhotoToken(null));
    }

    [Fact]
    public void PhotoTokenNormalizer_NormalizePhotoToken_NormalizesToken()
    {
        // Muster: a_b_c_d mit fuehrenden Nullen und Klein-Buchstabe
        var result = PhotoTokenNormalizer.NormalizePhotoToken("0042_0003_0000001_a");
        Assert.Equal("42_3_1_A", result);
    }

    [Fact]
    public void PhotoTokenNormalizer_EnumeratePhotoLookupKeys_YieldsKeysForToken()
    {
        var keys = new List<string>(PhotoTokenNormalizer.EnumeratePhotoLookupKeys("0042_0003_0000001_a"));
        Assert.Contains("42_3_1_A", keys);
    }

    [Fact]
    public void PhotoTokenNormalizer_AddPhotoLookupKeys_AddsWithoutDuplicates()
    {
        var keys = new List<string>();
        PhotoTokenNormalizer.AddPhotoLookupKeys("0042_0003_0000001_a", keys);
        PhotoTokenNormalizer.AddPhotoLookupKeys("0042_0003_0000001_a", keys);
        var count42 = keys.FindAll(k => string.Equals(k, "42_3_1_A", StringComparison.OrdinalIgnoreCase)).Count;
        Assert.Equal(1, count42);
    }

    // ── HoldingKeyUtils ───────────────────────────────────────────────────────────

    [Fact]
    public void HoldingKeyUtils_GetSuffixFromFirstUnderscore_ReturnsSuffix()
    {
        Assert.Equal("_58875-10.1089399.mpg", HoldingKeyUtils.GetSuffixFromFirstUnderscore("L_58875-10.1089399.mpg"));
    }

    [Fact]
    public void HoldingKeyUtils_GetSuffixFromFirstUnderscore_ReturnsNull_WhenNoUnderscore()
    {
        Assert.Null(HoldingKeyUtils.GetSuffixFromFirstUnderscore("L58875-10.1089399.mpg"));
    }

    // ── KinsTxtHeaderParser ───────────────────────────────────────────────────────

    [Fact]
    public void KinsTxtHeaderParser_TryParseTxtHeader_ParsesValidLine()
    {
        var line = "SW 42046.3 -> 41412.2 Name @Datei=Video1.mpg";
        var ok = KinsTxtHeaderParser.TryParseTxtHeader(line, out var haltung, out var video);
        Assert.True(ok);
        Assert.Equal("42046.3-41412.2", haltung);
        Assert.Equal("Video1.mpg", video);
    }

    [Fact]
    public void KinsTxtHeaderParser_TryParseTxtHeader_ReturnsFalse_WhenInvalidLine()
    {
        var ok = KinsTxtHeaderParser.TryParseTxtHeader("Kein Header", out _, out _);
        Assert.False(ok);
    }

    [Fact]
    public void KinsTxtHeaderParser_KinsTxtHeaderRegex_IsNotNull()
    {
        Assert.NotNull(KinsTxtHeaderParser.KinsTxtHeaderRegex);
    }
}
