using System.IO;
using AuswertungPro.Next.Application.UseCases.VsaFotos;
using AuswertungPro.Next.UI.Ai.Vsa;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Ein Befundfoto ist Kundendokumentation. Es darf nie im Windows-Temp-Ordner
/// enden: den darf Windows jederzeit leeren, und der Verlust faellt erst
/// Wochen spaeter auf (Sicherungsmeldung 12.09.2026, 34 tote Fotoverweise).
/// </summary>
public sealed class VsaFotoAblagePolicyTests
{
    [Fact]
    public void Ziel_legt_das_foto_neben_das_video_statt_in_den_temp_ordner()
    {
        var ziel = VsaFotoAblagePolicy.Ziel(
            videoPath: @"D:\Projekte\Bauen\100-200\video.mp4",
            photoIndex: 0,
            now: new DateTimeOffset(2026, 9, 12, 14, 5, 9, TimeSpan.Zero),
            existiert: _ => false);

        Assert.Equal(
            @"D:\Projekte\Bauen\100-200\Fotos\vsa_foto1_20260912_140509.png",
            ziel);
    }

    [Fact]
    public void Ziel_faellt_ohne_bekanntes_video_auf_den_temp_ordner_zurueck()
    {
        var ziel = VsaFotoAblagePolicy.Ziel(
            videoPath: null,
            photoIndex: 0,
            now: new DateTimeOffset(2026, 9, 12, 14, 5, 9, TimeSpan.Zero),
            existiert: _ => false);

        Assert.Equal(
            Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar),
            Path.GetDirectoryName(ziel));
    }

    [Fact]
    public void Ziel_waehlt_einen_freien_namen_statt_ein_vorhandenes_foto_zu_ersetzen()
    {
        const string belegt = @"D:\Projekte\Bauen\100-200\Fotos\vsa_foto1_20260912_140509.png";

        var ziel = VsaFotoAblagePolicy.Ziel(
            videoPath: @"D:\Projekte\Bauen\100-200\video.mp4",
            photoIndex: 0,
            now: new DateTimeOffset(2026, 9, 12, 14, 5, 9, TimeSpan.Zero),
            existiert: pfad => pfad == belegt);

        Assert.Equal(
            @"D:\Projekte\Bauen\100-200\Fotos\vsa_foto1_20260912_140509_2.png",
            ziel);
    }
}
