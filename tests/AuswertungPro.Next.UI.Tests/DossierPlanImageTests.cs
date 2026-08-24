using System;
using System.IO;
using System.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using AuswertungPro.Next.UI.Services;

using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlanImageAdjusterTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "plan_" + Guid.NewGuid().ToString("N"));

    public PlanImageAdjusterTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
        catch
        {
            // Ein Aufraeumfehler darf den Testlauf nicht rot machen.
        }
    }

    private string SchreibeBild(string name, int breite, int hoehe)
    {
        var pfad = Path.Combine(_root, name);
        Directory.CreateDirectory(Path.GetDirectoryName(pfad)!);

        var stride = breite * 4;
        var pixel = new byte[stride * hoehe];
        for (var i = 0; i < pixel.Length; i++)
            pixel[i] = (byte)(i % 251);

        var quelle = BitmapSource.Create(
            breite, hoehe, 96, 96, PixelFormats.Bgra32, null, pixel, stride);

        var kodierer = new PngBitmapEncoder();
        kodierer.Frames.Add(BitmapFrame.Create(quelle));

        using var strom = new FileStream(pfad, FileMode.Create, FileAccess.Write);
        kodierer.Save(strom);

        return pfad;
    }

    private static (int Breite, int Hoehe) Masse(string pfad)
    {
        var bild = new BitmapImage();
        bild.BeginInit();
        bild.CacheOption = BitmapCacheOption.OnLoad;
        bild.UriSource = new Uri(pfad, UriKind.Absolute);
        bild.EndInit();
        return (bild.PixelWidth, bild.PixelHeight);
    }

    [Fact]
    public void Eine_Vierteldrehung_vertauscht_Breite_und_Hoehe()
    {
        RunOnSta(() =>
        {
            var ziel = Path.Combine(_root, "dossier");
            var quelle = SchreibeBild(Path.Combine("dossier", "plan.png"), 40, 20);

            var ergebnis = new PlanImageAdjuster().Rotate(quelle, ziel, 90);

            Assert.True(ergebnis.Success, ergebnis.Error);
            Assert.Equal((20, 40), Masse(ergebnis.ImagePath!));
        });
    }

    [Fact]
    public void Ein_fremdes_Bild_bleibt_unveraendert()
    {
        // Das Original gehoert dem Benutzer — gedreht wird eine Kopie im
        // Dossierordner.
        RunOnSta(() =>
        {
            var fremd = SchreibeBild("fremd.png", 40, 20);
            var ziel = Path.Combine(_root, "dossier");

            var ergebnis = new PlanImageAdjuster().Rotate(fremd, ziel, 90);

            Assert.True(ergebnis.Success, ergebnis.Error);
            Assert.NotEqual(Path.GetFullPath(fremd), Path.GetFullPath(ergebnis.ImagePath!));
            Assert.Equal((40, 20), Masse(fremd));
            Assert.Equal(
                Path.GetFullPath(ziel),
                Path.GetFullPath(Path.GetDirectoryName(ergebnis.ImagePath!)!));
        });
    }

    [Fact]
    public void Ein_Bild_im_Dossierordner_wird_ersetzt_statt_vervielfacht()
    {
        RunOnSta(() =>
        {
            var ziel = Path.Combine(_root, "dossier");
            var quelle = SchreibeBild(Path.Combine("dossier", "plan.png"), 40, 20);

            var einmal = new PlanImageAdjuster().Rotate(quelle, ziel, 90);
            var zweimal = new PlanImageAdjuster().Rotate(einmal.ImagePath, ziel, 90);

            Assert.True(zweimal.Success, zweimal.Error);
            Assert.Equal(Path.GetFullPath(quelle), Path.GetFullPath(zweimal.ImagePath!));
            Assert.Single(Directory.GetFiles(ziel, "*.png"));

            // Zweimal ein Viertel ergibt die halbe Drehung.
            Assert.Equal((40, 20), Masse(zweimal.ImagePath!));
        });
    }

    [Theory]
    [InlineData(0)]
    [InlineData(45)]
    [InlineData(360)]
    public void Nur_Vierteldrehungen_sind_erlaubt(int grad)
    {
        RunOnSta(() =>
        {
            var ziel = Path.Combine(_root, "dossier");
            var quelle = SchreibeBild(Path.Combine("dossier", "plan.png"), 40, 20);

            var ergebnis = new PlanImageAdjuster().Rotate(quelle, ziel, grad);

            Assert.False(ergebnis.Success);
            Assert.NotNull(ergebnis.Error);
        });
    }

    [Fact]
    public void Ohne_Bild_gibt_es_einen_klaren_Grund()
    {
        RunOnSta(() =>
        {
            var ergebnis = new PlanImageAdjuster().Rotate(null, _root, 90);

            Assert.False(ergebnis.Success);
            Assert.NotNull(ergebnis.Error);
        });
    }

    private static void RunOnSta(Action action)
    {
        Exception? fehler = null;

        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                fehler = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (fehler is not null)
            throw new Xunit.Sdk.XunitException(fehler.ToString());
    }
}
