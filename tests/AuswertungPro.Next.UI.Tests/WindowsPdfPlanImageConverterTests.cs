using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.UI.Services;

using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class WindowsPdfPlanImageConverterTests : IDisposable
{
    private static readonly byte[] PngSignatur =
        [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a];

    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "plan_import_" + Guid.NewGuid().ToString("N"));

    public WindowsPdfPlanImageConverterTests() => Directory.CreateDirectory(_root);

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

    [Theory]
    [InlineData("plan.pdf")]
    [InlineData("plan.png")]
    [InlineData("plan.jpg")]
    [InlineData("plan.jpeg")]
    [InlineData("plan.bmp")]
    [InlineData("plan.gif")]
    public void Jede_ausgewaehlte_Datei_wird_durch_den_Import_gefuehrt(string pfad)
    {
        var converter = new WindowsPdfPlanImageConverter();

        Assert.True(converter.NeedsConversion(pfad));
        Assert.False(converter.NeedsConversion(null));
        Assert.False(converter.NeedsConversion("  "));
    }

    [Theory]
    [InlineData(".png")]
    [InlineData(".jpg")]
    [InlineData(".jpeg")]
    [InlineData(".bmp")]
    public void Planbild_wird_als_neue_Png_Kopie_gespeichert(string erweiterung)
    {
        RunOnSta(() =>
        {
            var quelle = SchreibeBild(
                Path.Combine("quelle", "plan" + erweiterung),
                erweiterung);
            var original = File.ReadAllBytes(quelle);

            var zielordner = Path.Combine(_root, "dossier");
            Directory.CreateDirectory(zielordner);
            var vorhandenesZiel = Path.Combine(zielordner, "plan.png");
            var vorhandenerInhalt = Encoding.UTF8.GetBytes("bereits vorhanden");
            File.WriteAllBytes(vorhandenesZiel, vorhandenerInhalt);

            var ergebnis = new WindowsPdfPlanImageConverter()
                .ConvertAsync(quelle, zielordner)
                .GetAwaiter()
                .GetResult();

            Assert.True(ergebnis.Success, ergebnis.Error);
            Assert.Equal("plan (2).png", Path.GetFileName(ergebnis.ImagePath));
            Assert.Equal(original, File.ReadAllBytes(quelle));
            Assert.Equal(vorhandenerInhalt, File.ReadAllBytes(vorhandenesZiel));
            AssertPng(ergebnis.ImagePath!);
        });
    }

    [Fact]
    public void Planbild_im_Zielordner_bleibt_unveraendert()
    {
        RunOnSta(() =>
        {
            var zielordner = Path.Combine(_root, "dossier");
            var quelle = SchreibeBild(
                Path.Combine("dossier", "plan.png"),
                ".png");
            var original = File.ReadAllBytes(quelle);

            var ergebnis = new WindowsPdfPlanImageConverter()
                .ConvertAsync(quelle, zielordner)
                .GetAwaiter()
                .GetResult();

            Assert.True(ergebnis.Success, ergebnis.Error);
            Assert.Equal("plan (2).png", Path.GetFileName(ergebnis.ImagePath));
            Assert.Equal(original, File.ReadAllBytes(quelle));
            AssertPng(ergebnis.ImagePath!);
            Assert.Equal(2, Directory.GetFiles(zielordner, "*.png").Length);
        });
    }

    [Fact]
    public void Pdf_wird_als_neue_Png_Kopie_gespeichert()
    {
        RunOnSta(() =>
        {
            var quellordner = Path.Combine(_root, "quelle");
            Directory.CreateDirectory(quellordner);
            var quelle = Path.Combine(quellordner, "plan.pdf");
            File.WriteAllBytes(quelle, ErzeugeEinseitigesPdf());
            var original = File.ReadAllBytes(quelle);

            var zielordner = Path.Combine(_root, "dossier");
            var ergebnis = new WindowsPdfPlanImageConverter()
                .ConvertAsync(quelle, zielordner)
                .GetAwaiter()
                .GetResult();

            Assert.True(ergebnis.Success, ergebnis.Error);
            Assert.Equal("plan.png", Path.GetFileName(ergebnis.ImagePath));
            Assert.Equal(original, File.ReadAllBytes(quelle));
            AssertPng(ergebnis.ImagePath!);
        });
    }

    [Fact]
    public async Task Nicht_erlaubte_Datei_wird_abgelehnt()
    {
        var quelle = Path.Combine(_root, "plan.gif");
        File.WriteAllText(quelle, "kein Planbild");
        var zielordner = Path.Combine(_root, "dossier");

        var ergebnis = await new WindowsPdfPlanImageConverter()
            .ConvertAsync(quelle, zielordner);

        Assert.False(ergebnis.Success);
        Assert.Contains("PDF, PNG, JPG, JPEG und BMP", ergebnis.Error);
        Assert.False(Directory.Exists(zielordner));
    }

    private string SchreibeBild(string name, string erweiterung)
    {
        var pfad = Path.Combine(_root, name);
        Directory.CreateDirectory(Path.GetDirectoryName(pfad)!);

        const int breite = 8;
        const int hoehe = 5;
        const int stride = breite * 3;
        var pixel = new byte[stride * hoehe];

        for (var i = 0; i < pixel.Length; i++)
            pixel[i] = (byte)(i * 17 % 251);

        var bild = BitmapSource.Create(
            breite,
            hoehe,
            96,
            96,
            PixelFormats.Bgr24,
            null,
            pixel,
            stride);

        BitmapEncoder encoder = erweiterung.ToLowerInvariant() switch
        {
            ".png" => new PngBitmapEncoder(),
            ".jpg" or ".jpeg" => new JpegBitmapEncoder(),
            ".bmp" => new BmpBitmapEncoder(),
            _ => throw new ArgumentOutOfRangeException(nameof(erweiterung))
        };

        encoder.Frames.Add(BitmapFrame.Create(bild));

        using var strom = new FileStream(pfad, FileMode.CreateNew, FileAccess.Write);
        encoder.Save(strom);

        return pfad;
    }

    private static byte[] ErzeugeEinseitigesPdf()
    {
        using var strom = new MemoryStream();
        var objektPositionen = new long[5];

        SchreibeAscii(strom, "%PDF-1.4\n");

        objektPositionen[1] = strom.Position;
        SchreibeAscii(strom, "1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");

        objektPositionen[2] = strom.Position;
        SchreibeAscii(
            strom,
            "2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n");

        objektPositionen[3] = strom.Position;
        SchreibeAscii(
            strom,
            "3 0 obj\n"
                + "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 100] "
                + "/Resources << >> /Contents 4 0 R >>\nendobj\n");

        objektPositionen[4] = strom.Position;
        SchreibeAscii(
            strom,
            "4 0 obj\n<< /Length 4 >>\nstream\nq\nQ\nendstream\nendobj\n");

        var xrefPosition = strom.Position;
        SchreibeAscii(strom, "xref\n0 5\n0000000000 65535 f \n");

        for (var i = 1; i <= 4; i++)
        {
            SchreibeAscii(strom, $"{objektPositionen[i]:D10} 00000 n \n");
        }

        SchreibeAscii(
            strom,
            "trailer\n<< /Size 5 /Root 1 0 R >>\n"
                + $"startxref\n{xrefPosition}\n%%EOF\n");

        return strom.ToArray();
    }

    private static void SchreibeAscii(Stream strom, string text)
    {
        var bytes = Encoding.ASCII.GetBytes(text);
        strom.Write(bytes, 0, bytes.Length);
    }

    private static void AssertPng(string pfad)
    {
        var bytes = File.ReadAllBytes(pfad);

        Assert.True(bytes.Length > PngSignatur.Length);
        Assert.Equal(PngSignatur, bytes[..PngSignatur.Length]);
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
