using AuswertungPro.Next.Application.Reports;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Die bestehenden oeffentlichen Layoutoptionen bleiben fuer alte Aufrufer erhalten.
/// Die neue Programmeinstellung darf deren Typen und Standardwerte nicht veraendern.
/// </summary>
public sealed class HaltungsprotokollPdfOptionsCompatibilityTests
{
    [Fact]
    public void Alte_Layoutoptionen_behalten_Typen_und_Standardwerte()
    {
        var options = new HaltungsprotokollPdfOptions();

        Assert.Equal(typeof(int), PropertyType(nameof(HaltungsprotokollPdfOptions.PhotosPerRow)));
        Assert.Equal(typeof(int), PropertyType(nameof(HaltungsprotokollPdfOptions.PhotosPerPage)));
        Assert.Equal(typeof(float), PropertyType(nameof(HaltungsprotokollPdfOptions.PhotoWidth)));
        Assert.Equal(typeof(float), PropertyType(nameof(HaltungsprotokollPdfOptions.PhotoHeight)));
        Assert.Equal(typeof(float), PropertyType(nameof(HaltungsprotokollPdfOptions.PhotoSpacing)));
        Assert.Equal(1, options.PhotosPerRow);
        Assert.Equal(2, options.PhotosPerPage);
        Assert.Equal(500f, options.PhotoWidth);
        Assert.Equal(255f, options.PhotoHeight);
        Assert.Equal(12f, options.PhotoSpacing);
    }

    private static Type? PropertyType(string propertyName)
        => typeof(HaltungsprotokollPdfOptions).GetProperty(propertyName)?.PropertyType;
}
