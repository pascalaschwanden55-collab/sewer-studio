using System;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AuswertungPro.Next.UI.Ai;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Qualitaetstests fuer die Inversions-Erkennung und -Korrektur.
/// Testet beide Heuristiken:
/// 1) Luminanz > 170 (klassisches helles Negativ)
/// 2) Blau > Rot + 20 bei Luminanz > 100 (lila-invertiertes WinCan-PDF)
/// </summary>
[Trait("Category", "Recommendation")]
[Trait("Category", "Slow")]
public sealed class ImageInversionHelperTests
{
    /// <summary>Erzeugt ein synthetisches PNG mit bestimmten RGB-Werten.</summary>
    private static byte[] CreateTestPng(byte r, byte g, byte b, int width = 40, int height = 30)
    {
        int stride = width * 4;
        byte[] pixels = new byte[stride * height];

        for (int i = 0; i < pixels.Length; i += 4)
        {
            pixels[i] = b;       // B (WPF: BGRA)
            pixels[i + 1] = g;   // G
            pixels[i + 2] = r;   // R
            pixels[i + 3] = 255; // A
        }

        var bmp = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bmp));

        using var ms = new MemoryStream();
        encoder.Save(ms);
        return ms.ToArray();
    }

    /// <summary>Erzeugt ein Graustufen-PNG (R=G=B).</summary>
    private static byte[] CreateGrayPng(byte luminance, int width = 40, int height = 30)
        => CreateTestPng(luminance, luminance, luminance, width, height);

    // === Heuristik 1: Luminanz-basiert ===

    [Fact]
    public void DunklesBild_WirdNichtAlsInvertiertErkannt()
    {
        // Typisches Kanalbild: dunkel (Luminanz ~60)
        var darkPng = CreateGrayPng(60);
        Assert.False(ImageInversionHelper.IsLikelyInverted(darkPng));
    }

    [Fact]
    public void HellesBild_WirdAlsInvertiertErkannt_Luminanz()
    {
        // Klassisches helles Negativ (Luminanz ~200)
        var brightPng = CreateGrayPng(200);
        Assert.True(ImageInversionHelper.IsLikelyInverted(brightPng));
    }

    [Fact]
    public void Schwellenwert_170_IstGrenze_Luminanz()
    {
        var below = CreateGrayPng(169);
        Assert.False(ImageInversionHelper.IsLikelyInverted(below));

        var above = CreateGrayPng(172);
        Assert.True(ImageInversionHelper.IsLikelyInverted(above));
    }

    // === Heuristik 2: Farbkanal-basiert (Blau > Rot) ===

    [Fact]
    public void LilasBild_WirdAlsInvertiertErkannt_Farbkanal()
    {
        // Lila-invertiertes WinCan-PDF: hoher Blau-Anteil, niedriger Rot-Anteil
        // avgLum = (80*299 + 70*587 + 140*114)/1000 ≈ 81 → ueber 100? Nein.
        // Besser: R=100, G=110, B=150 → avgLum ≈ 111, avgB-avgR=50
        var lilaPng = CreateTestPng(r: 100, g: 110, b: 150);
        Assert.True(ImageInversionHelper.IsLikelyInverted(lilaPng));
    }

    [Fact]
    public void NormalesBraunBild_WirdNichtInvertiert()
    {
        // Normales Kanalbild: braun/grau, Rot >= Blau
        // R=130, G=110, B=90 → typisch fuer Faserzement-Rohr
        var brownPng = CreateTestPng(r: 130, g: 110, b: 90);
        Assert.False(ImageInversionHelper.IsLikelyInverted(brownPng));
    }

    [Fact]
    public void BlauUeberhang_AberZuDunkel_WirdNichtInvertiert()
    {
        // Blau > Rot, aber Luminanz < 100 → kein Trigger
        // R=30, G=40, B=80 → avgLum ≈ 41, zu dunkel
        var darkBluePng = CreateTestPng(r: 30, g: 40, b: 80);
        Assert.False(ImageInversionHelper.IsLikelyInverted(darkBluePng));
    }

    [Fact]
    public void LeichterBlauUeberhang_UnterSchwelle_WirdNichtInvertiert()
    {
        // avgB - avgR = 15 (unter Schwelle 20) bei Luminanz > 100
        // R=110, G=120, B=125 → Differenz nur 15
        var slightBluePng = CreateTestPng(r: 110, g: 120, b: 125);
        Assert.False(ImageInversionHelper.IsLikelyInverted(slightBluePng));
    }

    // === Inversion und AutoCorrect ===

    [Fact]
    public void InvertColors_InvertiertPixelWerte()
    {
        byte origR = 60, origG = 70, origB = 80;
        var inputPng = CreateTestPng(origR, origG, origB);
        var invertedPng = ImageInversionHelper.InvertColors(inputPng);

        using var ms = new MemoryStream(invertedPng);
        var decoder = new PngBitmapDecoder(ms, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        var wb = new WriteableBitmap(decoder.Frames[0]);
        int stride = wb.PixelWidth * 4;
        byte[] pixels = new byte[stride * wb.PixelHeight];
        wb.CopyPixels(pixels, stride, 0);

        Assert.Equal((byte)(255 - origB), pixels[0]); // B invertiert
        Assert.Equal((byte)(255 - origG), pixels[1]); // G invertiert
        Assert.Equal((byte)(255 - origR), pixels[2]); // R invertiert
        Assert.Equal(255, pixels[3]);                   // A bleibt
    }

    [Fact]
    public void AutoCorrect_KorrigiertLilasBild()
    {
        // Lila-invertiert: Blau >> Rot bei mittlerer Luminanz
        var lilaPng = CreateTestPng(r: 100, g: 110, b: 150);
        var corrected = ImageInversionHelper.AutoCorrect(lilaPng);

        // Nach Korrektur: Rot > Blau (normal) → nicht mehr als invertiert erkannt
        Assert.False(ImageInversionHelper.IsLikelyInverted(corrected));
    }

    [Fact]
    public void AutoCorrect_LasstNormalesBildUnveraendert()
    {
        // Normales braunes Kanalbild — keine Aenderung
        var brownPng = CreateTestPng(r: 130, g: 110, b: 90);
        var result = ImageInversionHelper.AutoCorrect(brownPng);
        Assert.Equal(brownPng, result);
    }

    [Fact]
    public void AutoCorrect_NullOderLeer_GibtInputZurueck()
    {
        Assert.Equal(Array.Empty<byte>(), ImageInversionHelper.AutoCorrect(Array.Empty<byte>()));
    }

    [Fact]
    public void DoppelteInversion_GibtOriginalZurueck()
    {
        var inputPng = CreateTestPng(r: 80, g: 90, b: 70);
        var inverted = ImageInversionHelper.InvertColors(inputPng);
        var doubleInverted = ImageInversionHelper.InvertColors(inverted);

        using var ms = new MemoryStream(doubleInverted);
        var decoder = new PngBitmapDecoder(ms, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        var wb = new WriteableBitmap(decoder.Frames[0]);
        int stride = wb.PixelWidth * 4;
        byte[] pixels = new byte[stride * wb.PixelHeight];
        wb.CopyPixels(pixels, stride, 0);

        // Toleranz von 1 wegen PNG-Compression
        Assert.InRange(pixels[0], (byte)69, (byte)71); // B ≈ 70
        Assert.InRange(pixels[1], (byte)89, (byte)91); // G ≈ 90
        Assert.InRange(pixels[2], (byte)79, (byte)81); // R ≈ 80
    }
}
