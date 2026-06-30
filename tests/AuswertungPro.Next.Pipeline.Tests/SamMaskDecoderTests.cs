using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Charakterisierungstests fuer SamMaskDecoder.
/// Stellt sicher dass DecodeRle, Downsample und HasOverlap das exakte
/// Ist-Verhalten beibehalten (verhaltensneutrale Extraktion).
/// </summary>
public class SamMaskDecoderTests
{
    // ── DecodeRle ────────────────────────────────────────────────────

    [Fact]
    public void DecodeRle_EmptyString_LiefertLeereAberDimensionierteMaske()
    {
        var mask = SamMaskDecoder.DecodeRle("", 4, 3);
        Assert.Equal(3, mask.GetLength(0));
        Assert.Equal(4, mask.GetLength(1));
        Assert.False(mask[0, 0]);
    }

    [Fact]
    public void DecodeRle_NullOrWhitespace_LiefertLeereAberDimensionierteMaske()
    {
        var mask = SamMaskDecoder.DecodeRle("   ", 4, 3);
        Assert.Equal(3, mask.GetLength(0));
        Assert.Equal(4, mask.GetLength(1));
    }

    [Fact]
    public void DecodeRle_NegativeDimension_LiefertLeereMaske0x0()
    {
        var mask = SamMaskDecoder.DecodeRle("0,4", -1, 3);
        Assert.Equal(0, mask.GetLength(0));
        Assert.Equal(0, mask.GetLength(1));
    }

    [Fact]
    public void DecodeRle_UeberMaxPixels_LiefertLeereMaske0x0()
    {
        // 100.000 x 1000 = 100M > MaxMaskPixels (50M)
        var mask = SamMaskDecoder.DecodeRle("0,1", 100_000, 1_000);
        Assert.Equal(0, mask.GetLength(0));
        Assert.Equal(0, mask.GetLength(1));
    }

    [Fact]
    public void DecodeRle_NurEinToken_LiefertLeereAberDimensionierteMaske()
    {
        // Weniger als 2 Tokens -> keine runs -> leere Maske
        var mask = SamMaskDecoder.DecodeRle("0", 2, 2);
        Assert.Equal(2, mask.GetLength(0));
        Assert.Equal(2, mask.GetLength(1));
        Assert.False(mask[0, 0]);
    }

    [Fact]
    public void DecodeRle_StartValue0_ErsteRun_IstFalse()
    {
        // StartValue=0 -> false, run1=2 -> 2 false-Pixel, run2=2 -> 2 true-Pixel
        // Maske 1x4: [F, F, T, T]
        var mask = SamMaskDecoder.DecodeRle("0,2,2", 4, 1);
        Assert.False(mask[0, 0]);
        Assert.False(mask[0, 1]);
        Assert.True(mask[0, 2]);
        Assert.True(mask[0, 3]);
    }

    [Fact]
    public void DecodeRle_StartValue1_ErsteRun_IstTrue()
    {
        // StartValue=1 -> true, run1=2 -> 2 true-Pixel, run2=2 -> 2 false-Pixel
        // Maske 1x4: [T, T, F, F]
        var mask = SamMaskDecoder.DecodeRle("1,2,2", 4, 1);
        Assert.True(mask[0, 0]);
        Assert.True(mask[0, 1]);
        Assert.False(mask[0, 2]);
        Assert.False(mask[0, 3]);
    }

    [Fact]
    public void DecodeRle_RowMajorOrdnung_KorrektMapping()
    {
        // 2x3 Maske, StartValue=0, run1=3, run2=3
        // Pixel 0,1,2 -> false; 3,4,5 -> true
        // row=0: col0=F, col1=F, col2=F; row=1: col0=T, col1=T, col2=T
        var mask = SamMaskDecoder.DecodeRle("0,3,3", 3, 2);
        Assert.False(mask[0, 0]);
        Assert.False(mask[0, 2]);
        Assert.True(mask[1, 0]);
        Assert.True(mask[1, 2]);
    }

    [Fact]
    public void DecodeRle_KorruptesRunToken_BrichtAb_BehaeltBisherige()
    {
        // "0,2,abc,2" -> nach run1=2 (false) kommt ungueltig -> Abbruch
        // Pixel 0,1 bleiben false; Pixel 2,3 bleiben false (kein weiteres run)
        var mask = SamMaskDecoder.DecodeRle("0,2,abc,2", 4, 1);
        Assert.Equal(1, mask.GetLength(0));
        Assert.Equal(4, mask.GetLength(1));
        // Keine Exception -> Abbruch-Verhalten: kein Crash
        Assert.False(mask[0, 0]);
    }

    [Fact]
    public void DecodeRle_NegativesRunToken_BrichtAb()
    {
        // Negatives Run-Token -> Abbruch, kein Crash
        var mask = SamMaskDecoder.DecodeRle("1,2,-1,2", 4, 1);
        Assert.Equal(1, mask.GetLength(0));
        // Nach run1=2 (true) Abbruch -> pixel 0,1 true, 2,3 unberuehrt (false)
        Assert.True(mask[0, 0]);
        Assert.True(mask[0, 1]);
        Assert.False(mask[0, 2]);
    }

    [Fact]
    public void DecodeRle_RiesenRunToken_BegrenztAufTotalPixels()
    {
        // Grosses Run-Token das ueber die Maskengroesse hinausgeht -> kein Crash
        var mask = SamMaskDecoder.DecodeRle("1,999999", 3, 2);
        // Alle 6 Pixel werden true gesetzt, kein Exception
        Assert.True(mask[0, 0]);
        Assert.True(mask[1, 2]);
    }

    [Fact]
    public void DecodeRle_UngueltigesStartToken_LiefertDimensionierteMaske()
    {
        var mask = SamMaskDecoder.DecodeRle("X,2,2", 4, 1);
        // Kein Exception, alle Pixel bleiben false (Start-Parse fehlgeschlagen)
        Assert.Equal(4, mask.GetLength(1));
        Assert.False(mask[0, 0]);
    }

    // ── Downsample ───────────────────────────────────────────────────

    [Fact]
    public void Downsample_GleicheGroesse_GibtOriginalZurueck()
    {
        var src = new bool[2, 3] { { true, false, true }, { false, true, false } };
        var result = SamMaskDecoder.Downsample(src, 2, 3, 2, 3);
        // Gleiche Instanz bei identischen Dimensionen
        Assert.Same(src, result);
    }

    [Fact]
    public void Downsample_HalbeGroesse_NearestNeighbour()
    {
        // 4x4 Quelle, alle true in oberer linker 2x2
        var src = new bool[4, 4];
        src[0, 0] = src[0, 1] = src[1, 0] = src[1, 1] = true;

        var dst = SamMaskDecoder.Downsample(src, 4, 4, 2, 2);
        Assert.Equal(2, dst.GetLength(0));
        Assert.Equal(2, dst.GetLength(1));
        Assert.True(dst[0, 0]);
    }

    // ── HasOverlap ───────────────────────────────────────────────────

    [Fact]
    public void HasOverlap_NegativeZeile_GibtFalse()
    {
        var ds = new bool[3, 3];
        ds[0, 0] = true;
        Assert.False(SamMaskDecoder.HasOverlap(ds, -1, 0, 3));
    }

    [Fact]
    public void HasOverlap_ZeileAusserhalbBereich_GibtFalse()
    {
        var ds = new bool[3, 3];
        Assert.False(SamMaskDecoder.HasOverlap(ds, 5, 0, 3));
    }

    [Fact]
    public void HasOverlap_GesetzterPixelImBereich_GibtTrue()
    {
        var ds = new bool[3, 3];
        ds[1, 2] = true;
        Assert.True(SamMaskDecoder.HasOverlap(ds, 1, 1, 3));
    }

    [Fact]
    public void HasOverlap_KeinPixelImBereich_GibtFalse()
    {
        var ds = new bool[3, 3];
        ds[1, 0] = true;
        // Bereich [1,3) enthaelt Spalte 0 nicht
        Assert.False(SamMaskDecoder.HasOverlap(ds, 1, 1, 3));
    }

    [Fact]
    public void HasOverlap_LeereRange_GibtFalse()
    {
        var ds = new bool[3, 3];
        ds[1, 1] = true;
        // colStart >= colEnd -> leerer Bereich
        Assert.False(SamMaskDecoder.HasOverlap(ds, 1, 2, 2));
    }
}
