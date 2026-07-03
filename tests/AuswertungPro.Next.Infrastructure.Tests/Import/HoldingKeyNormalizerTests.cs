using AuswertungPro.Next.Infrastructure.Import.Common;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

/// <summary>
/// Charakterisierungstests fuer HoldingKeyNormalizer.
/// Sichert das IST-Verhalten der NormalizeHoldingKey-Methoden aus WinCan/IBAK/XTF/M150.
/// </summary>
public class HoldingKeyNormalizerTests
{
    // --- Normalize (Basis) ---

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    public void Normalize_Leer_GibtLeerZurueck(string? input, string erwartet)
        => Assert.Equal(erwartet, HoldingKeyNormalizer.Normalize(input));

    [Fact]
    public void Normalize_Slash_WirdBindestrich()
        => Assert.Equal("100-200", HoldingKeyNormalizer.Normalize("100/200"));

    [Fact]
    public void Normalize_EnDash_WirdBindestrich()
        => Assert.Equal("100-200", HoldingKeyNormalizer.Normalize("100–200"));

    [Fact]
    public void Normalize_EmDash_WirdBindestrich()
        => Assert.Equal("100-200", HoldingKeyNormalizer.Normalize("100—200"));

    [Fact]
    public void Normalize_Whitespace_WirdEntfernt()
        => Assert.Equal("100-200", HoldingKeyNormalizer.Normalize("  100 - 200  "));

    [Fact]
    public void Normalize_InneresLeerzeichen_WirdEntfernt()
        => Assert.Equal("AB200-300", HoldingKeyNormalizer.Normalize("AB 200-300"));

    [Fact]
    public void Normalize_NormaleKombination_Unveraendert()
        => Assert.Equal("1028055-1064892", HoldingKeyNormalizer.Normalize("1028055-1064892"));

    // --- NormalizeIbak (mit Prefix-Strip) ---

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    public void NormalizeIbak_Leer_GibtLeerZurueck(string? input, string erwartet)
        => Assert.Equal(erwartet, HoldingKeyNormalizer.NormalizeIbak(input));

    [Fact]
    public void NormalizeIbak_LDoppelUnterstr_PrefixEntfernt()
        => Assert.Equal("1028055-1064892", HoldingKeyNormalizer.NormalizeIbak("L__1028055-1064892"));

    [Fact]
    public void NormalizeIbak_LEinfachUnterstr_PrefixEntfernt()
        => Assert.Equal("1028055-1064892", HoldingKeyNormalizer.NormalizeIbak("L_1028055-1064892"));

    [Fact]
    public void NormalizeIbak_HDoppelUnterstr_PrefixEntfernt()
        => Assert.Equal("1028055-1064892", HoldingKeyNormalizer.NormalizeIbak("H__1028055-1064892"));

    [Fact]
    public void NormalizeIbak_HEinfachUnterstr_PrefixEntfernt()
        => Assert.Equal("1028055-1064892", HoldingKeyNormalizer.NormalizeIbak("H_1028055-1064892"));

    [Fact]
    public void NormalizeIbak_OhnePrefix_UnveraendertNormalisiert()
        => Assert.Equal("1028055-1064892", HoldingKeyNormalizer.NormalizeIbak("1028055-1064892"));

    [Fact]
    public void NormalizeIbak_SlashUndPrefix_BeideNormalisiert()
        => Assert.Equal("1028055-1064892", HoldingKeyNormalizer.NormalizeIbak("L__1028055/1064892"));

    [Fact]
    public void NormalizeIbak_SchachtSchachtPrefixe_WerdenEntfernt()
        => Assert.Equal("10081-8993", HoldingKeyNormalizer.NormalizeIbak("SS 10081-SS 8993"));

    [Fact]
    public void NormalizeIbak_DateiprefixUndSchachtPrefixe_WerdenKombiniertEntfernt()
        => Assert.Equal("10081-8993", HoldingKeyNormalizer.NormalizeIbak("H_SS 10081-SS 8993"));

    [Fact]
    public void NormalizeIbak_PrefixCaseInsensitive()
    {
        // Gross- und Kleinschreibung wird akzeptiert
        Assert.Equal("abc", HoldingKeyNormalizer.NormalizeIbak("l__abc"));
        Assert.Equal("abc", HoldingKeyNormalizer.NormalizeIbak("L_abc"));
        Assert.Equal("abc", HoldingKeyNormalizer.NormalizeIbak("h__abc"));
        Assert.Equal("abc", HoldingKeyNormalizer.NormalizeIbak("h_abc"));
    }

    [Fact]
    public void NormalizeIbak_NurPrefixKeinRest_GibtLeerZurueck()
        => Assert.Equal("", HoldingKeyNormalizer.NormalizeIbak("L__"));

    [Fact]
    public void Normalize_UndNormalizeIbak_GleichesBasisverhalten()
    {
        // Ohne IBAK-Prefix: identisches Ergebnis
        const string input = "100-200";
        Assert.Equal(HoldingKeyNormalizer.Normalize(input), HoldingKeyNormalizer.NormalizeIbak(input));
    }
}
