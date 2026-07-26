using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Ai.Pipeline;

/// <summary>
/// Verhaltenstests fuer die zentrale Masken-Wahrheitspruefung am Gold-Gate.
/// Testbasis: 4x4-Maske (16 Pixel), Box (0.5/0.5/0.5/0.5) deckt nach
/// Pixelmittelpunkt-Regel die Spalten/Zeilen 1 und 2 ab.
/// </summary>
public sealed class SamMaskValidatorTests
{
    private static readonly BoundingBox Box = new(0.5, 0.5, 0.5, 0.5);

    // 16 Pixel: 5 Hintergrund, 2 Maske (Zeile 1, Spalte 1-2), 9 Hintergrund — in der Box.
    private const string GueltigeRle = "0,5,2,9";

    [Fact]
    public void Gueltige_Maske_wird_akzeptiert()
    {
        var valid = SamMaskValidator.IsValid(GueltigeRle, 4, 4, Box, degraded: false, out var reason);

        Assert.True(valid);
        Assert.Equal(string.Empty, reason);
    }

    [Theory]
    [InlineData("abc,def")]        // Start-Token nicht lesbar
    [InlineData("0,5,x,4")]        // Run-Token nicht lesbar
    [InlineData("0,5,-2,14")]      // negativer Run
    public void Unlesbares_Rle_wird_abgelehnt(string rle)
    {
        var valid = SamMaskValidator.IsValid(rle, 4, 4, Box, degraded: false, out var reason);

        Assert.False(valid);
        Assert.Contains("nicht lesbar", reason);
    }

    [Fact]
    public void Leermaske_ohne_Pixel_wird_abgelehnt()
    {
        // 16 Hintergrund-Pixel, kein einziges Masken-Pixel.
        var valid = SamMaskValidator.IsValid("0,16", 4, 4, Box, degraded: false, out var reason);

        Assert.False(valid);
        Assert.Contains("Leermaske", reason);
    }

    [Fact]
    public void Rle_Laenge_inkonsistent_zu_den_Bildmassen_wird_abgelehnt()
    {
        // Laufsumme 15 statt 4x4=16 Pixel.
        var valid = SamMaskValidator.IsValid("0,5,2,8", 4, 4, Box, degraded: false, out var reason);

        Assert.False(valid);
        Assert.Contains("Bildmassen", reason);
    }

    [Theory]
    [InlineData(null, 4)]          // Breite fehlt
    [InlineData(0, 4)]             // Breite 0
    [InlineData(4, 0)]             // Hoehe 0
    public void Fehlende_Bildmasse_werden_abgelehnt(int? width, int? height)
    {
        var valid = SamMaskValidator.IsValid(GueltigeRle, width, height, Box, degraded: false, out var reason);

        Assert.False(valid);
        Assert.Contains("Bildmasse", reason);
    }

    [Fact]
    public void Maske_ausserhalb_der_Box_wird_abgelehnt()
    {
        // Einziges Masken-Pixel oben links (Zeile 0, Spalte 0) — die Box beginnt bei Pixel 1.
        var valid = SamMaskValidator.IsValid("1,1,15", 4, 4, Box, degraded: false, out var reason);

        Assert.False(valid);
        Assert.Contains("kein Vordergrundpixel", reason);
    }

    [Fact]
    public void Diagonale_Randpixel_ohne_echten_Pixel_in_der_Box_werden_abgelehnt()
    {
        // Pixel (0,0) und (3,3) spannen zwar eine Huellbox ueber das ganze Bild,
        // aber kein Vordergrundpixel liegt in der kleinen mittigen Hand-Box.
        var middleBox = new BoundingBox(0.5, 0.5, 0.25, 0.25);

        var valid = SamMaskValidator.IsValid(
            "1,1,14,1",
            4,
            4,
            middleBox,
            degraded: false,
            out var reason);

        Assert.False(valid);
        Assert.Contains("kein Vordergrundpixel", reason);
    }

    [Theory]
    [InlineData("1,1,15", 0.125, 0.125)]
    [InlineData("0,15,1", 0.875, 0.875)]
    public void Vordergrundpixel_am_Bildrand_in_passender_Box_wird_akzeptiert(
        string rle,
        double xCenter,
        double yCenter)
    {
        var edgeBox = new BoundingBox(xCenter, yCenter, 0.25, 0.25);

        var valid = SamMaskValidator.IsValid(
            rle,
            4,
            4,
            edgeBox,
            degraded: false,
            out var reason);

        Assert.True(valid);
        Assert.Equal(string.Empty, reason);
    }

    [Theory]
    [InlineData("0,5,1,10")] // Pixelzentrum (0.375, 0.375) auf linker/oberer Boxgrenze
    [InlineData("0,10,1,5")] // Pixelzentrum (0.625, 0.625) auf rechter/unterer Boxgrenze
    public void Vordergrundpixel_auf_Boxgrenze_wird_akzeptiert(string rle)
    {
        var boundaryBox = new BoundingBox(0.5, 0.5, 0.25, 0.25);

        var valid = SamMaskValidator.IsValid(
            rle,
            4,
            4,
            boundaryBox,
            degraded: false,
            out var reason);

        Assert.True(valid);
        Assert.Equal(string.Empty, reason);
    }

    [Fact]
    public void Degradierte_Maske_wird_abgelehnt()
    {
        var valid = SamMaskValidator.IsValid(GueltigeRle, 4, 4, Box, degraded: true, out var reason);

        Assert.False(valid);
        Assert.Contains("Degraded", reason);
    }

    [Fact]
    public void Fehlende_Rle_wird_abgelehnt()
    {
        var valid = SamMaskValidator.IsValid(null, 4, 4, Box, degraded: false, out var reason);

        Assert.False(valid);
        Assert.False(string.IsNullOrWhiteSpace(reason));
    }
}
