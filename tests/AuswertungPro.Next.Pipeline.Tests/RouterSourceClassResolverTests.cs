using AuswertungPro.Next.Application.Ai.Evaluation;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Charakterisierungstests fuer RouterSourceClassResolver.
/// </summary>
public sealed class RouterSourceClassResolverTests
{
    // --- NormalizeClassName ---

    [Theory]
    [InlineData("riss_bruch",  "riss_bruch")]
    [InlineData("Riss-Bruch",  "riss_bruch")]
    [InlineData("LEER",        "leer")]
    [InlineData("  leer  ",    "leer")]
    [InlineData("riss bruch",  "riss_bruch")]
    public void NormalizeClassName_NormiertAufKleinbuchstabenUndUnterstrich(string input, string expected)
        => Assert.Equal(expected, RouterSourceClassResolver.NormalizeClassName(input));

    // --- ExtractClassFromFileName ---

    [Theory]
    [InlineData(@"C:\data\leer_001.png",         "LEER")]
    [InlineData(@"C:\data\kein_schaden_01.png",  "LEER")]
    [InlineData(@"C:\data\no_damage_7.png",      "LEER")]
    [InlineData(@"C:\data\BCD_frame_042.jpg",    "BCD")]
    [InlineData(@"C:\data\BABAC_003.png",        "BABAC")]
    [InlineData(@"C:\data\frame_012.png",        null)]   // kein erkennbarer Code
    public void ExtractClassFromFileName_ErkenntCodeAusStemOderKeyword(string path, string? expected)
        => Assert.Equal(expected, RouterSourceClassResolver.ExtractClassFromFileName(path));

    // --- MapSourceClassToRouterClass ---

    [Theory]
    [InlineData("empty",        "leer")]
    [InlineData("negative",     "leer")]
    [InlineData("no_damage",    "leer")]
    [InlineData("kein_schaden", "leer")]
    [InlineData("leer",         "leer")]
    [InlineData("LEER",         "leer")]
    [InlineData("meta",         "beginn_ende")]
    [InlineData("start_ende",   "beginn_ende")]
    [InlineData("anschluss",    "anschluss")]
    [InlineData("riss_bruch",   "riss_bruch")]
    [InlineData("rissbruch",    "riss_bruch")]
    [InlineData("wurzeln",      "wurzeln")]
    [InlineData("deformation",  "deformation")]
    public void MapSourceClassToRouterClass_MapptBekannteKlassen(string input, string expected)
        => Assert.Equal(expected, RouterSourceClassResolver.MapSourceClassToRouterClass(input));

    [Theory]
    [InlineData("BCD",  "beginn_ende")]
    [InlineData("BCE",  "beginn_ende")]
    [InlineData("BAB",  "riss_bruch")]
    [InlineData("BAA",  "deformation")]
    [InlineData("BBC",  "ablagerung")]
    public void MapSourceClassToRouterClass_MapptVsaCodes(string vsaCode, string expectedRouterClass)
        => Assert.Equal(expectedRouterClass, RouterSourceClassResolver.MapSourceClassToRouterClass(vsaCode));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("unbekannt_xyz")]
    public void MapSourceClassToRouterClass_GibtNullFuerUnbekannte(string input)
        => Assert.Null(RouterSourceClassResolver.MapSourceClassToRouterClass(input));
}
