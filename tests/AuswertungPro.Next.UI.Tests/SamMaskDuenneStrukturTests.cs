using System.Threading;
using System.Windows;
using System.Windows.Media;
using AuswertungPro.Next.UI.Ai.Pipeline;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// B04 (Audit 23.09.2026): Fuer die Anzeige wird eine SAM-Maske auf hoechstens 480 Punkte
/// Breite verkleinert. Ein 1 Pixel breiter Riss darf dabei nicht verschwinden, egal auf
/// welcher Spalte oder Zeile er liegt. Die gespeicherte Maske und ihre Vermessung sind
/// davon nicht betroffen; es geht nur um die Zeichnung.
/// </summary>
public sealed class SamMaskDuenneStrukturTests
{
    private const int Breite = 960;
    private const int Hoehe = 540;

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(501)]
    public void Kontur_zeigt_einen_senkrechten_Riss_von_einem_Pixel_Breite(int spalte)
    {
        RunOnStaThread(() =>
        {
            var maske = new bool[Hoehe, Breite];
            for (var zeile = 100; zeile < 440; zeile++)
                maske[zeile, spalte] = true;

            var kontur = SamMaskRenderer.ExtractContourGeometry(maske, Breite, Hoehe, Breite, Hoehe);

            AssertLiegtBei(kontur, spalte, spalte + 1, 100, 440);
        });
    }

    [Fact]
    public void Kontur_zeigt_einen_waagrechten_Riss_auf_ungerader_Zeile()
    {
        RunOnStaThread(() =>
        {
            var maske = new bool[Hoehe, Breite];
            for (var spalte = 100; spalte < 440; spalte++)
                maske[301, spalte] = true;

            var kontur = SamMaskRenderer.ExtractContourGeometry(maske, Breite, Hoehe, Breite, Hoehe);

            AssertLiegtBei(kontur, 100, 440, 301, 302);
        });
    }

    [Fact]
    public void Kontur_zeigt_einen_schraegen_Riss_von_einem_Pixel_Breite()
    {
        RunOnStaThread(() =>
        {
            var maske = new bool[Hoehe, Breite];
            for (var i = 100; i < 400; i++)
                maske[i + 1, i] = true;

            var kontur = SamMaskRenderer.ExtractContourGeometry(maske, Breite, Hoehe, Breite, Hoehe);

            AssertLiegtBei(kontur, 100, 400, 101, 401);
        });
    }

    [Fact]
    public void Kontur_zeigt_einen_einzelnen_Punkt_auf_ungerader_Stelle()
    {
        RunOnStaThread(() =>
        {
            var maske = new bool[Hoehe, Breite];
            maske[301, 501] = true;

            var kontur = SamMaskRenderer.ExtractContourGeometry(maske, Breite, Hoehe, Breite, Hoehe);

            AssertLiegtBei(kontur, 501, 502, 301, 302);
        });
    }

    [Fact]
    public void Kontur_zeigt_einen_duennen_Riss_auch_im_HD_Bild()
    {
        // 1920 Pixel werden auf 480 verkleinert: Jeder vierte Pixel wuerde als Stichprobe
        // genommen, die Spalte 2 dazwischen fiele weg.
        RunOnStaThread(() =>
        {
            var maske = new bool[1080, 1920];
            for (var zeile = 200; zeile < 800; zeile++)
                maske[zeile, 2] = true;

            var kontur = SamMaskRenderer.ExtractContourGeometry(maske, 1920, 1080, 1920, 1080);

            AssertLiegtBei(kontur, 2, 3, 200, 800);
        });
    }

    [Fact]
    public void Fuellung_zeigt_einen_senkrechten_Riss_von_einem_Pixel_Breite()
    {
        RunOnStaThread(() =>
        {
            var maske = new bool[Hoehe, Breite];
            for (var zeile = 100; zeile < 440; zeile++)
                maske[zeile, 1] = true;

            var fuellung = SamMaskRenderer.ExtractFillGeometry(maske, Breite, Hoehe, Breite, Hoehe);

            AssertLiegtBei(fuellung, 1, 2, 100, 440);
        });
    }

    [Fact]
    public void Kontur_folgt_der_Zeichenflaeche()
    {
        // Dieselbe Maske auf halber Anzeigegroesse: der Riss bleibt an seiner Bildstelle.
        RunOnStaThread(() =>
        {
            var maske = new bool[Hoehe, Breite];
            for (var zeile = 100; zeile < 440; zeile++)
                maske[zeile, 501] = true;

            var kontur = SamMaskRenderer.ExtractContourGeometry(maske, Breite, Hoehe, Breite / 2.0, Hoehe / 2.0);

            AssertLiegtBei(kontur, 250.5, 251, 50, 220);
        });
    }

    /// <summary>
    /// Die Zeichnung muss die echte Stelle abdecken und darf hoechstens um eine
    /// Anzeigezelle (bei 960 -> 480 zwei Bildpunkte) daneben beginnen oder enden.
    /// </summary>
    private static void AssertLiegtBei(Geometry geometrie, double links, double rechts, double oben, double unten)
    {
        Assert.False(geometrie.IsEmpty(), "Die Struktur ist in der Anzeige verschwunden.");

        var b = geometrie.Bounds;
        const double toleranz = 4.0;
        Assert.InRange(b.Left, links - toleranz, links + 0.001);
        Assert.InRange(b.Right, rechts - 0.001, rechts + toleranz);
        Assert.InRange(b.Top, oben - toleranz, oben + 0.001);
        Assert.InRange(b.Bottom, unten - 0.001, unten + toleranz);
    }

    private static void RunOnStaThread(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception is not null)
            throw exception;
    }
}
