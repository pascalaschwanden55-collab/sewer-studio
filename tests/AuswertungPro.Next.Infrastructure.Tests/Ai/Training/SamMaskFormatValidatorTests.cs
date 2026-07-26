using AuswertungPro.Next.Application.Ai.Training;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Ai.Training;

/// <summary>
/// Verhaltenstests fuer die strenge RLE-Formatpruefung (Application-Schicht).
/// Testbasis: 4x4-Maske (16 Pixel); gueltige RLE "0,5,2,9" = 2 Masken-Pixel in der Mitte.
/// </summary>
public sealed class SamMaskFormatValidatorTests
{
    [Fact]
    public void Gueltige_Rle_wird_akzeptiert()
    {
        var valid = SamMaskFormatValidator.IsValid("0,5,2,9", 4, 4, out var reason);

        Assert.True(valid);
        Assert.Equal(string.Empty, reason);
    }

    [Theory]
    [InlineData("1,3,13")] // erster Pixel ist Maske, letzter Hintergrund
    [InlineData("0,15,1")] // letzter Bildpixel ist Maske
    public void Gueltige_Rle_mit_ungerader_Tokenzahl_wird_akzeptiert(string rle)
    {
        var valid = SamMaskFormatValidator.IsValid(rle, 4, 4, out var reason);

        Assert.True(valid);
        Assert.Equal(string.Empty, reason);
    }

    [Theory]
    [InlineData("abc,def")]        // Start-Token nicht lesbar
    [InlineData("2,1,15")]         // Startwert muss binaer sein
    [InlineData("0,5,x,4")]        // Run-Token nicht lesbar
    [InlineData("0,5,-2,14")]      // negativer Run
    [InlineData("0,0,1,15")]       // echter Encoder erzeugt keine Nullruns
    public void Unlesbare_oder_defekte_Rle_wird_abgelehnt(string rle)
    {
        var valid = SamMaskFormatValidator.IsValid(rle, 4, 4, out var reason);

        Assert.False(valid);
        Assert.Contains("nicht lesbar", reason);
    }

    [Fact]
    public void Laufsumme_ungleich_Bildflaeche_wird_abgelehnt()
    {
        var valid = SamMaskFormatValidator.IsValid("0,5,2,8", 4, 4, out var reason);

        Assert.False(valid);
        Assert.Contains("Bildmassen", reason);
    }

    [Fact]
    public void Leermaske_ohne_Masken_Pixel_wird_abgelehnt()
    {
        var valid = SamMaskFormatValidator.IsValid("0,16", 4, 4, out var reason);

        Assert.False(valid);
        Assert.Contains("Leermaske", reason);
    }

    [Theory]
    [InlineData(null, 4)]
    [InlineData(0, 4)]
    [InlineData(4, 0)]
    public void Fehlende_Bildmasse_wird_abgelehnt(int? width, int? height)
    {
        var valid = SamMaskFormatValidator.IsValid("0,5,2,9", width, height, out var reason);

        Assert.False(valid);
        Assert.Contains("Bildmasse", reason);
    }

    [Fact]
    public void Fehlende_Rle_wird_abgelehnt()
    {
        var valid = SamMaskFormatValidator.IsValid(null, 4, 4, out var reason);

        Assert.False(valid);
        Assert.False(string.IsNullOrWhiteSpace(reason));
    }
}

/// <summary>
/// Signatur-Identitaet: 4-Teiler-Format ohne Box (Legacy) und b:-Geometrie-Teil mit Box
/// (Mehrfachobjekt: gleiche Haltung/Code/Meter, verschiedene Boxen = verschiedene Objekte).
/// </summary>
public sealed class TrainingSampleSignatureTests
{
    [Fact]
    public void Signatur_ohne_Box_bleibt_4_teilig()
    {
        var signature = TrainingSample.BuildCanonicalSignature("H-01", "BAB", 12.34, 12.34);

        Assert.Equal("H-01|BAB|12.3|12.3", signature);
    }

    [Fact]
    public void Signatur_mit_Box_enthaelt_gerundeten_Geometrie_Teil()
    {
        var signature = TrainingSample.BuildCanonicalSignature(
            "H-01", "BAB", 12.34, 12.34, 0.12345, 0.5, 0.2, 0.2);

        Assert.Equal("H-01|BAB|12.3|12.3|b:0.123,0.500,0.200,0.200", signature);
    }

    [Fact]
    public void Signatur_unterscheidet_zwei_Objekte_mit_gleichem_Code_und_Meter()
    {
        var erstes = TrainingSample.BuildCanonicalSignature("H-01", "BAB", 5.0, 5.0, 0.3, 0.5, 0.2, 0.2);
        var zweites = TrainingSample.BuildCanonicalSignature("H-01", "BAB", 5.0, 5.0, 0.7, 0.5, 0.2, 0.2);

        Assert.NotEqual(erstes, zweites);
        Assert.Contains("|b:0.300,", erstes);
        Assert.Contains("|b:0.700,", zweites);
    }

    [Fact]
    public void Signatur_mit_teilweiser_Box_bleibt_4_teilig()
    {
        var signature = TrainingSample.BuildCanonicalSignature("H-01", "BAB", 5.0, 5.0, 0.3, null, 0.2, 0.2);

        Assert.Equal("H-01|BAB|5.0|5.0", signature);
    }
}
