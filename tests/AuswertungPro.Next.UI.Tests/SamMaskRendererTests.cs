using AuswertungPro.Next.UI.Ai.Pipeline;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Tests fuer die defensive RLE-Dekodierung in <see cref="SamMaskRenderer"/>.
/// Die RLE-Strings kommen ungeprueft vom Python-Sidecar — kaputte Werte duerfen
/// weder werfen noch durch absurde Dimensionen den Speicher sprengen.
/// </summary>
public class SamMaskRendererTests
{
    [Fact]
    public void DecodeRle_ValidRle_DecodesCorrectly()
    {
        // "1,2,2" auf 2x2: Start=Vordergrund, Lauf 2 (obere Zeile), Lauf 2 (untere, Hintergrund)
        var mask = SamMaskRenderer.DecodeRle("1,2,2", width: 2, height: 2);

        Assert.True(mask[0, 0]);
        Assert.True(mask[0, 1]);
        Assert.False(mask[1, 0]);
        Assert.False(mask[1, 1]);
    }

    [Fact]
    public void DecodeRle_InvalidStartToken_ReturnsEmptyMaskWithoutThrow()
    {
        var mask = SamMaskRenderer.DecodeRle("x,2,2", width: 2, height: 2);

        // Dimensionen bleiben gueltig, aber nichts ist gesetzt
        Assert.Equal(2, mask.GetLength(0));
        Assert.Equal(2, mask.GetLength(1));
        Assert.False(mask[0, 0]);
        Assert.False(mask[1, 1]);
    }

    [Fact]
    public void DecodeRle_InvalidRunToken_DecodesValidPrefixWithoutThrow()
    {
        // "1,2,abc,1": erster Lauf (2 Pixel Vordergrund) gueltig, danach Abbruch
        var mask = SamMaskRenderer.DecodeRle("1,2,abc,1", width: 2, height: 2);

        Assert.True(mask[0, 0]);
        Assert.True(mask[0, 1]);
        Assert.False(mask[1, 0]);
    }

    [Fact]
    public void DecodeRle_NegativeDimensions_ReturnsEmptyWithoutThrow()
    {
        var mask = SamMaskRenderer.DecodeRle("1,2,2", width: -1, height: -5);

        Assert.Equal(0, mask.GetLength(0));
        Assert.Equal(0, mask.GetLength(1));
    }

    [Fact]
    public void DecodeRle_HugeRunLength_DoesNotOverflowOrThrow()
    {
        // "1,3,2147483647,5": gueltiger Praefix (3 Pixel), dann ein Riesen-Run nahe int.MaxValue.
        // Ohne long-Arithmetik wuerde pos += runLength ueberlaufen → negativer Index → Crash.
        var mask = SamMaskRenderer.DecodeRle("1,3,2147483647,5", width: 2, height: 2);

        Assert.True(mask[0, 0]);
        Assert.True(mask[0, 1]);
        Assert.True(mask[1, 0]);
        Assert.False(mask[1, 1]);
    }

    [Fact]
    public void DecodeRle_OversizedDimensions_ReturnsEmptyWithoutAllocating()
    {
        // 100000 x 100000 waeren 10^10 bool = ~10 GB → muss abgewiesen werden
        var mask = SamMaskRenderer.DecodeRle("1,4,4", width: 100_000, height: 100_000);

        Assert.Equal(0, mask.GetLength(0));
        Assert.Equal(0, mask.GetLength(1));
    }
}
