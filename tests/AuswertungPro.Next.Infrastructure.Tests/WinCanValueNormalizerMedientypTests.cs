using AuswertungPro.Next.Infrastructure.Import.WinCan;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Leerer OMM_FileType kommt im echten WinCan-Bestand vor (gemessen: in zwei von fuenf
/// Projekten war genau die Videozeile ohne Typ). Dann muss die Dateiendung entscheiden,
/// sonst verschwindet ein vorhandenes Video still aus der Haltung.
/// </summary>
public sealed class WinCanValueNormalizerMedientypTests
{
    [Theory]
    [InlineData(null, "H6_00001.mp4", true)]
    [InlineData("", "H6_00001.mp4", true)]
    [InlineData("   ", "H10_00002.MP4", true)]
    [InlineData(null, "H6_BDA_00002.jpg", false)]
    public void LeererTyp_WirdAusDerEndungAbgeleitet(string? typ, string datei, bool istVideo)
    {
        var ermittelt = WinCanValueNormalizer.MedientypOderEndung(typ, datei);

        Assert.Equal(istVideo, WinCanValueNormalizer.IsVideo(ermittelt));
        Assert.Equal(!istVideo, WinCanValueNormalizer.IsImage(ermittelt));
    }

    [Fact]
    public void VorhandenerTyp_BleibtUnveraendert()
    {
        Assert.Equal("MP4", WinCanValueNormalizer.MedientypOderEndung("MP4", "irgendwas.jpg"));
        Assert.Equal("JPG", WinCanValueNormalizer.MedientypOderEndung("JPG", "irgendwas.mp4"));
    }

    [Fact]
    public void OhneTypUndOhneEndung_BleibtUnbekannt()
    {
        var ermittelt = WinCanValueNormalizer.MedientypOderEndung(null, "datei_ohne_endung");

        Assert.False(WinCanValueNormalizer.IsVideo(ermittelt));
        Assert.False(WinCanValueNormalizer.IsImage(ermittelt));
    }
}
