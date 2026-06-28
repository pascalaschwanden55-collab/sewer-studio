using AuswertungPro.Next.Infrastructure.Import.Xtf;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

/// <summary>
/// Charakterisierungstests fuer M150ValueExtractor.
/// Sichert das IST-Verhalten der aus M150MdbImportHelper extrahierten Methoden.
/// </summary>
public sealed class M150ValueExtractorTests
{
    // -----------------------------------------------------------------------
    // NormalizeDirection
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    public void NormalizeDirection_Leer_GibtLeerZurueck(string? input, string expected)
        => Assert.Equal(expected, M150ValueExtractor.NormalizeDirection(input));

    [Theory]
    [InlineData("d")]
    [InlineData("D")]
    [InlineData("down")]
    [InlineData("DOWN")]
    [InlineData("1")]
    public void NormalizeDirection_Downstream_GibtObenUnten(string input)
        => Assert.Equal("oben -> unten", M150ValueExtractor.NormalizeDirection(input));

    [Theory]
    [InlineData("u")]
    [InlineData("U")]
    [InlineData("up")]
    [InlineData("UP")]
    [InlineData("2")]
    public void NormalizeDirection_Upstream_GibtUntenOben(string input)
        => Assert.Equal("unten -> oben", M150ValueExtractor.NormalizeDirection(input));

    [Fact]
    public void NormalizeDirection_VonUntenNachOben_GibtUntenOben()
        => Assert.Equal("unten -> oben", M150ValueExtractor.NormalizeDirection("von unten nach oben"));

    [Fact]
    public void NormalizeDirection_VonObenNachUnten_GibtObenUnten()
        => Assert.Equal("oben -> unten", M150ValueExtractor.NormalizeDirection("von oben nach unten"));

    [Fact]
    public void NormalizeDirection_UnbekannterText_GibtOriginalZurueck()
        => Assert.Equal("Fliessrichtung Nord", M150ValueExtractor.NormalizeDirection("Fliessrichtung Nord"));

    // -----------------------------------------------------------------------
    // ShouldReverseWinCanDirection
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("U")]
    [InlineData("UP")]
    [InlineData("UPSTREAM")]
    [InlineData("upstream")]
    [InlineData("2")]
    public void ShouldReverseWinCanDirection_Upstream_GibtTrue(string dir)
        => Assert.True(M150ValueExtractor.ShouldReverseWinCanDirection(dir));

    [Theory]
    [InlineData("D")]
    [InlineData("DOWN")]
    [InlineData("DOWNSTREAM")]
    [InlineData("1")]
    [InlineData("")]
    [InlineData(null)]
    public void ShouldReverseWinCanDirection_Downstream_GibtFalse(string? dir)
        => Assert.False(M150ValueExtractor.ShouldReverseWinCanDirection(dir));

    // -----------------------------------------------------------------------
    // NormalizeWinCanDirection
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("D", "oben -> unten")]
    [InlineData("DOWN", "oben -> unten")]
    [InlineData("DOWNSTREAM", "oben -> unten")]
    [InlineData("1", "oben -> unten")]
    [InlineData("U", "unten -> oben")]
    [InlineData("UP", "unten -> oben")]
    [InlineData("UPSTREAM", "unten -> oben")]
    [InlineData("2", "unten -> oben")]
    [InlineData(null, "")]
    [InlineData("", "")]
    public void NormalizeWinCanDirection_BekannteCodes_KorrektGemappt(string? input, string expected)
        => Assert.Equal(expected, M150ValueExtractor.NormalizeWinCanDirection(input));

    // -----------------------------------------------------------------------
    // BuildHoldingFromWinCanSection
    // -----------------------------------------------------------------------

    [Fact]
    public void BuildHoldingFromWinCanSection_Downstream_GibtStartEndZurueck()
    {
        var result = M150ValueExtractor.BuildHoldingFromWinCanSection("100", "200", "D");
        Assert.Equal("100-200", result);
    }

    [Fact]
    public void BuildHoldingFromWinCanSection_Upstream_GibtEndStartZurueck()
    {
        var result = M150ValueExtractor.BuildHoldingFromWinCanSection("100", "200", "U");
        Assert.Equal("200-100", result);
    }

    [Fact]
    public void BuildHoldingFromWinCanSection_LeereKnoten_GibtLeerZurueck()
    {
        var result = M150ValueExtractor.BuildHoldingFromWinCanSection("", "200", "D");
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void BuildHoldingFromWinCanSection_NormierteIds_FunktionierenderBuildup()
    {
        // Schraegsttrich als Trennzeichen soll normiert werden
        var result = M150ValueExtractor.BuildHoldingFromWinCanSection("1.10", "1.20", "1");
        Assert.Equal("1.10-1.20", result);
    }

    // -----------------------------------------------------------------------
    // ExtractPointId
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    public void ExtractPointId_Leer_GibtLeerZurueck(string? input, string expected)
        => Assert.Equal(expected, M150ValueExtractor.ExtractPointId(input));

    [Fact]
    public void ExtractPointId_EinfacheId_GibtDirectZurueck()
        => Assert.Equal("100", M150ValueExtractor.ExtractPointId("100"));

    [Fact]
    public void ExtractPointId_PunktNotation_GibtDirectZurueck()
        => Assert.Equal("1.10", M150ValueExtractor.ExtractPointId("1.10"));

    [Fact]
    public void ExtractPointId_AlphanumerischMitUnterstrich_GibtUnveraendertZurueck()
    {
        // PointRx = ^[A-Za-z0-9][A-Za-z0-9._-]*$ erlaubt auch Unterstrich (steht im character class).
        // "Node_1234" ist daher eine gueltige Punkt-ID -> wird direkt unveraendert zurueckgegeben.
        var result = M150ValueExtractor.ExtractPointId("Node_1234");
        Assert.Equal("Node_1234", result);
    }

    // -----------------------------------------------------------------------
    // IsHoldingId
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("100-200", true)]
    [InlineData("1.10-1.20", true)]
    [InlineData("865-864", true)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("100", false)]
    [InlineData("ABC", false)]
    public void IsHoldingId_VarianteWerte_KorrektErkannt(string? input, bool expected)
        => Assert.Equal(expected, M150ValueExtractor.IsHoldingId(input));

    // -----------------------------------------------------------------------
    // IsPointId
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("100", true)]
    [InlineData("1.10", true)]
    [InlineData("ABC", true)]
    [InlineData("A1-B2", true)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData(" ", false)]
    public void IsPointId_VarianteWerte_KorrektErkannt(string? input, bool expected)
        => Assert.Equal(expected, M150ValueExtractor.IsPointId(input));

    // -----------------------------------------------------------------------
    // TryNormalizeDate
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    public void TryNormalizeDate_Leer_GibtNullZurueck(string? input, string? expected)
        => Assert.Equal(expected, M150ValueExtractor.TryNormalizeDate(input));

    [Fact]
    public void TryNormalizeDate_IsoFormat_GibtDeutschesFormat()
        => Assert.Equal("15.03.2023", M150ValueExtractor.TryNormalizeDate("2023-03-15"));

    [Fact]
    public void TryNormalizeDate_DeutschesFormat_GibtGleichesDatum()
        => Assert.Equal("15.03.2023", M150ValueExtractor.TryNormalizeDate("15.03.2023"));

    [Fact]
    public void TryNormalizeDate_ZweistelligesJahr_GibtVierstelligesJahr()
        => Assert.Equal("15.03.2023", M150ValueExtractor.TryNormalizeDate("15.03.23"));

    [Fact]
    public void TryNormalizeDate_DatumInText_ExtrahiertDatum()
        => Assert.Equal("15.03.2023", M150ValueExtractor.TryNormalizeDate("Inspektion 15.03.2023 Uri"));

    [Fact]
    public void TryNormalizeDate_UngueltigerText_GibtNull()
        => Assert.Null(M150ValueExtractor.TryNormalizeDate("kein-datum-hier"));

    // -----------------------------------------------------------------------
    // NormalizeNumberText
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    public void NormalizeNumberText_Leer_GibtLeerZurueck(string? input, string expected)
        => Assert.Equal(expected, M150ValueExtractor.NormalizeNumberText(input));

    [Fact]
    public void NormalizeNumberText_KommaAlsDezimaltrennzeichen_WirdPunkt()
        => Assert.Equal("45.30", M150ValueExtractor.NormalizeNumberText("45,30"));

    [Fact]
    public void NormalizeNumberText_GanzeZahl_GibtUnveraendertZurueck()
        => Assert.Equal("300", M150ValueExtractor.NormalizeNumberText("300"));

    [Fact]
    public void NormalizeNumberText_ZahlMitText_ExtrahiertZahl()
        => Assert.Equal("45.30", M150ValueExtractor.NormalizeNumberText("45.30 m"));

    // -----------------------------------------------------------------------
    // LooksLikeVideoLink
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("keinlink", false)]
    public void LooksLikeVideoLink_Leer_GibtFalse(string input, bool expected)
        => Assert.Equal(expected, M150ValueExtractor.LooksLikeVideoLink(input));

    [Theory]
    [InlineData("video.mp4", true)]
    [InlineData("clip.avi", true)]
    [InlineData("film.mkv", true)]
    [InlineData("AUFNAHME.MP4", true)]
    public void LooksLikeVideoLink_VideoExtension_GibtTrue(string input, bool expected)
        => Assert.Equal(expected, M150ValueExtractor.LooksLikeVideoLink(input));

    [Fact]
    public void LooksLikeVideoLink_ZeitstempelMuster_GibtTrue()
        => Assert.True(M150ValueExtractor.LooksLikeVideoLink("1_2_3_20230315_120000"));

    [Fact]
    public void LooksLikeVideoLink_TextDatei_GibtFalse()
        => Assert.False(M150ValueExtractor.LooksLikeVideoLink("protokoll.pdf"));

    // -----------------------------------------------------------------------
    // PickValue
    // -----------------------------------------------------------------------

    [Fact]
    public void PickValue_TrifftAufHint_GibtWertZurueck()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "datum", "01.01.2020" }
        };
        var result = M150ValueExtractor.PickValue(map, new[] { "datum" }, _ => true);
        Assert.Equal("01.01.2020", result);
    }

    [Fact]
    public void PickValue_KeinTreffer_GibtLeerZurueck()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "something_else", "Wert" }
        };
        var result = M150ValueExtractor.PickValue(map, new[] { "datum" }, _ => true);
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void PickValue_ValidatorSchlagtFehl_GibtLeerZurueck()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "datum", "kein-gueltig-wert" }
        };
        var result = M150ValueExtractor.PickValue(map, new[] { "datum" }, v => v.Contains("2020"));
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void PickValue_MehrereHints_NimmtErstenTreffenden()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "datum", "A" },
            { "date", "B" }
        };
        var result = M150ValueExtractor.PickValue(map, new[] { "datum", "date" }, _ => true);
        Assert.Equal("A", result);
    }

    // -----------------------------------------------------------------------
    // NormalizeKey
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    public void NormalizeKey_Leer_GibtLeerZurueck(string? input, string expected)
        => Assert.Equal(expected, M150ValueExtractor.NormalizeKey(input));

    [Fact]
    public void NormalizeKey_GrosskleinSchreibung_GibtKleinbuchstaben()
        => Assert.Equal("haltung", M150ValueExtractor.NormalizeKey("Haltung"));

    [Fact]
    public void NormalizeKey_SonderzeichenEntfernt()
        => Assert.Equal("hg001", M150ValueExtractor.NormalizeKey("HG_001"));

    [Fact]
    public void NormalizeKey_NurAlphanumerisch_GibtSauberenSchluessel()
        => Assert.Equal("hi003", M150ValueExtractor.NormalizeKey("HI-003"));
}
