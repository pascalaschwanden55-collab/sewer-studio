using AuswertungPro.Next.UI.Ai;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

// Slice 8a.6.E 2026-05-10: Tests fuer
// AutoCalibrationService.DetectPillarboxPadding — die Heuristik die
// schwarze Pillarbox-Balken im Frame erkennt, damit der Edge-Algorithmus
// nicht die Balken-Kanten als Rohrwand interpretiert.
[Trait("Category", "Unit")]
public sealed class AutoCalibrationPillarboxTests
{
    [Fact]
    public void DetectPillarboxPadding_NullGray_ReturnsZero()
    {
        var (l, r) = AutoCalibrationService.DetectPillarboxPadding(null!, 100, 100);
        Assert.Equal(0, l);
        Assert.Equal(0, r);
    }

    [Fact]
    public void DetectPillarboxPadding_ZeroSize_ReturnsZero()
    {
        var (l, r) = AutoCalibrationService.DetectPillarboxPadding(new byte[0], 0, 0);
        Assert.Equal(0, l);
        Assert.Equal(0, r);
    }

    [Fact]
    public void DetectPillarboxPadding_AllWhite_ReturnsZero()
    {
        // 100x100, alle Pixel hell — kein Pillarbox.
        var gray = MakeUniformGray(100, 100, 200);
        var (l, r) = AutoCalibrationService.DetectPillarboxPadding(gray, 100, 100);
        Assert.Equal(0, l);
        Assert.Equal(0, r);
    }

    [Fact]
    public void DetectPillarboxPadding_AllBlack_ReturnsZero()
    {
        // Alle schwarz: Heuristik erkennt das als "kein gueltiger Frame" und
        // gibt (0,0) zurueck — Sanity-Check verhindert Falscherkennung.
        var gray = MakeUniformGray(100, 100, 0);
        var (l, r) = AutoCalibrationService.DetectPillarboxPadding(gray, 100, 100);
        Assert.Equal(0, l);
        Assert.Equal(0, r);
    }

    [Fact]
    public void DetectPillarboxPadding_LeftPillarOnly_ReturnsLeftPad()
    {
        // 200x100. Linke 30 Spalten schwarz, Rest hell.
        var gray = MakePillarbox(200, 100, leftPad: 30, rightPad: 0,
            backgroundLuma: 200);
        var (l, r) = AutoCalibrationService.DetectPillarboxPadding(gray, 200, 100);
        Assert.Equal(30, l);
        Assert.Equal(0, r);
    }

    [Fact]
    public void DetectPillarboxPadding_RightPillarOnly_ReturnsRightPad()
    {
        var gray = MakePillarbox(200, 100, leftPad: 0, rightPad: 25,
            backgroundLuma: 200);
        var (l, r) = AutoCalibrationService.DetectPillarboxPadding(gray, 200, 100);
        Assert.Equal(0, l);
        Assert.Equal(25, r);
    }

    [Fact]
    public void DetectPillarboxPadding_SymmetricPillarbox_ReturnsBoth()
    {
        // Hochformat in Querformat-Container: typische Inspektions-Konfig.
        var gray = MakePillarbox(400, 100, leftPad: 50, rightPad: 50,
            backgroundLuma: 200);
        var (l, r) = AutoCalibrationService.DetectPillarboxPadding(gray, 400, 100);
        Assert.Equal(50, l);
        Assert.Equal(50, r);
    }

    [Fact]
    public void DetectPillarboxPadding_PillarboxOver35Percent_Cap()
    {
        // Wenn linke 50% schwarz und rechte 50% schwarz → das ist kein
        // Pillarbox sondern ein dunkles Bild. Heuristik begrenzt auf 35%
        // pro Seite UND verlangt dass Content-Fenster mind. width/2 hat.
        var gray = MakePillarbox(200, 100, leftPad: 100, rightPad: 100,
            backgroundLuma: 200);
        var (l, r) = AutoCalibrationService.DetectPillarboxPadding(gray, 200, 100);
        // Sanity-Check greift: leftPad + rightPad > width/2, also (0,0).
        Assert.Equal(0, l);
        Assert.Equal(0, r);
    }

    [Fact]
    public void DetectPillarboxPadding_DarkBackground_NotPillar()
    {
        // Wenn das ganze Bild dunkel-grau ist (nicht schwarz, sondern Luma=20),
        // soll der Algorithmus kein Pillarbox detektieren — sonst hauen
        // Inspektions-Frames mit leichtem Schatten in die Falle.
        var gray = MakeUniformGray(200, 100, 20);
        var (l, r) = AutoCalibrationService.DetectPillarboxPadding(gray, 200, 100);
        // 20 ist ueber BlackThreshold (15), also kein Pillar.
        Assert.Equal(0, l);
        Assert.Equal(0, r);
    }

    // ─── Helpers ─────────────────────────────────────────────────────

    private static byte[] MakeUniformGray(int width, int height, byte luma)
    {
        var pixels = new byte[width * height];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = luma;
        return pixels;
    }

    private static byte[] MakePillarbox(
        int width, int height, int leftPad, int rightPad, byte backgroundLuma)
    {
        var pixels = new byte[width * height];
        for (int y = 0; y < height; y++)
        {
            int rowStart = y * width;
            for (int x = 0; x < width; x++)
            {
                bool inLeftPillar = x < leftPad;
                bool inRightPillar = x >= width - rightPad;
                pixels[rowStart + x] = (inLeftPillar || inRightPillar)
                    ? (byte)0
                    : backgroundLuma;
            }
        }
        return pixels;
    }
}
