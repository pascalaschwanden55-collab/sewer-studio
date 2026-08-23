using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

using AuswertungPro.Next.Infrastructure.Dossiers;

using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Dossiers;

public sealed class ImageSizeReaderTests
{
    [Fact]
    public void Liest_Breite_und_Hoehe_eines_PNG()
    {
        var png = TestImages.Png(width: 716, height: 297);

        Assert.True(ImageSizeReader.TryRead(png, out var width, out var height));
        Assert.Equal(716, width);
        Assert.Equal(297, height);
    }

    [Fact]
    public void Liest_Breite_und_Hoehe_eines_JPEG()
    {
        var jpeg = TestImages.Jpeg(width: 177, height: 213);

        Assert.True(ImageSizeReader.TryRead(jpeg, out var width, out var height));
        Assert.Equal(177, width);
        Assert.Equal(213, height);
    }

    [Fact]
    public void Unbekannte_Bytes_ergeben_kein_Ergebnis_statt_geratener_Masse()
    {
        var muell = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

        Assert.False(ImageSizeReader.TryRead(muell, out var width, out var height));
        Assert.Equal(0, width);
        Assert.Equal(0, height);
    }
}

/// <summary>
/// Baut die kleinsten gueltigen Bilddateien, die der Groessenleser verstehen
/// muss. Bewusst von Hand zusammengesetzt: die Testbibliothek hat keine
/// Bildbibliothek, und fuer die Kopfdaten braucht es auch keine.
/// </summary>
internal static class TestImages
{
    /// <summary>PNG-Signatur plus IHDR-Block mit Breite und Hoehe.</summary>
    public static byte[] Png(int width, int height)
    {
        var bytes = new List<byte>
        {
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
            0x00, 0x00, 0x00, 0x0D,
            (byte)'I', (byte)'H', (byte)'D', (byte)'R'
        };

        bytes.AddRange(BigEndian(width));
        bytes.AddRange(BigEndian(height));
        bytes.AddRange(new byte[] { 8, 6, 0, 0, 0 });
        return bytes.ToArray();
    }

    /// <summary>JPEG-Start plus ein SOF0-Segment mit Hoehe und Breite.</summary>
    public static byte[] Jpeg(int width, int height)
    {
        var bytes = new List<byte>
        {
            0xFF, 0xD8,
            // APP0-Segment mit 4 Nutzbytes: wird uebersprungen.
            0xFF, 0xE0, 0x00, 0x06, 1, 2, 3, 4,
            // SOF0: Laenge 17, Genauigkeit 8, dann Hoehe und Breite.
            0xFF, 0xC0, 0x00, 0x11, 0x08
        };

        bytes.Add((byte)(height >> 8));
        bytes.Add((byte)(height & 0xFF));
        bytes.Add((byte)(width >> 8));
        bytes.Add((byte)(width & 0xFF));
        bytes.AddRange(new byte[] { 3, 1, 0x22, 0, 2, 0x11, 1, 3, 0x11, 1 });
        return bytes.ToArray();
    }

    private static byte[] BigEndian(int value) => new[]
    {
        (byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value
    };
}

public sealed class DocxImagePlaceholderFillerTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "dossier_bilder_" + Guid.NewGuid().ToString("N"));

    public DocxImagePlaceholderFillerTests() => Directory.CreateDirectory(_root);

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

    [Fact]
    public void Setzt_ein_Bild_ein_und_entfernt_den_Platzhalter()
    {
        var bildPfad = Path.Combine(_root, "logo.png");
        File.WriteAllBytes(bildPfad, TestImages.Png(width: 716, height: 297));

        using var stream = new MemoryStream();
        using (var document = CreateDocument(stream, "{{@Logo}}"))
        {
            DocxImagePlaceholderFiller.Fill(document, new[]
            {
                new DocxImagePlacement("Logo", bildPfad, MaxWidthCm: 4.5)
            });
            document.MainDocumentPart!.Document.Save();
        }

        stream.Position = 0;
        using var reopened = WordprocessingDocument.Open(stream, false);
        var mainPart = reopened.MainDocumentPart!;

        var text = string.Concat(
            mainPart.Document.Body!.Descendants<Text>().Select(t => t.Text));

        Assert.DoesNotContain("{{", text, StringComparison.Ordinal);
        Assert.Single(mainPart.ImageParts);
        Assert.NotEmpty(mainPart.Document.Body!.Descendants<Drawing>());
    }

    [Fact]
    public void Behaelt_das_Seitenverhaeltnis_des_Bildes()
    {
        var bildPfad = Path.Combine(_root, "logo.png");
        File.WriteAllBytes(bildPfad, TestImages.Png(width: 200, height: 100));

        using var stream = new MemoryStream();
        using (var document = CreateDocument(stream, "{{@Logo}}"))
        {
            // 2 cm breit, halb so hoch wie breit -> 1 cm hoch.
            DocxImagePlaceholderFiller.Fill(document, new[]
            {
                new DocxImagePlacement("Logo", bildPfad, MaxWidthCm: 2.0)
            });
            document.MainDocumentPart!.Document.Save();
        }

        stream.Position = 0;
        using var reopened = WordprocessingDocument.Open(stream, false);
        var extent = reopened.MainDocumentPart!.Document.Body!
            .Descendants<DocumentFormat.OpenXml.Drawing.Wordprocessing.Extent>()
            .Single();

        Assert.Equal(720_000L, extent.Cx!.Value);
        Assert.Equal(360_000L, extent.Cy!.Value);
    }

    [Fact]
    public void Fehlende_Bilddatei_laesst_die_Stelle_leer_statt_den_Platzhalter_stehen()
    {
        var fehlt = Path.Combine(_root, "gibtesnicht.png");

        using var stream = new MemoryStream();
        IReadOnlyList<string> nichtGefuellt;
        using (var document = CreateDocument(stream, "Vorne {{@Logo}} hinten"))
        {
            nichtGefuellt = DocxImagePlaceholderFiller.Fill(document, new[]
            {
                new DocxImagePlacement("Logo", fehlt, MaxWidthCm: 4.5)
            });
            document.MainDocumentPart!.Document.Save();
        }

        stream.Position = 0;
        using var reopened = WordprocessingDocument.Open(stream, false);
        var mainPart = reopened.MainDocumentPart!;

        var text = string.Concat(
            mainPart.Document.Body!.Descendants<Text>().Select(t => t.Text));

        Assert.DoesNotContain("{{", text, StringComparison.Ordinal);
        Assert.Contains("Vorne", text, StringComparison.Ordinal);
        Assert.Contains("hinten", text, StringComparison.Ordinal);
        Assert.Empty(mainPart.ImageParts);

        // Der Aufrufer muss erfahren koennen, dass "Logo" nicht gesetzt wurde.
        Assert.Contains("Logo", nichtGefuellt);
    }

    [Fact]
    public void Findet_den_Platzhalter_auch_wenn_Word_ihn_zerlegt_hat()
    {
        var bildPfad = Path.Combine(_root, "wappen.jpg");
        File.WriteAllBytes(bildPfad, TestImages.Jpeg(width: 177, height: 213));

        using var stream = new MemoryStream();
        using (var document = new Func<WordprocessingDocument>(() =>
               {
                   var doc = WordprocessingDocument.Create(
                       stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document);
                   var part = doc.AddMainDocumentPart();
                   part.Document = new Document();
                   var body = part.Document.AppendChild(new Body());
                   var paragraph = body.AppendChild(new Paragraph());
                   paragraph.Append(
                       NewRun("{{@"), NewRun("Wap"), NewRun("pen"), NewRun("}}"));
                   return doc;
               })())
        {
            DocxImagePlaceholderFiller.Fill(document, new[]
            {
                new DocxImagePlacement("Wappen", bildPfad, MaxWidthCm: 2.0)
            });
            document.MainDocumentPart!.Document.Save();
        }

        stream.Position = 0;
        using var reopened = WordprocessingDocument.Open(stream, false);
        var mainPart = reopened.MainDocumentPart!;

        var text = string.Concat(
            mainPart.Document.Body!.Descendants<Text>().Select(t => t.Text));

        Assert.DoesNotContain("{{", text, StringComparison.Ordinal);
        Assert.Single(mainPart.ImageParts);
    }

    private static WordprocessingDocument CreateDocument(MemoryStream stream, string text)
    {
        var document = WordprocessingDocument.Create(
            stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document);
        var mainPart = document.AddMainDocumentPart();
        mainPart.Document = new Document();
        var body = mainPart.Document.AppendChild(new Body());
        body.AppendChild(new Paragraph()).Append(NewRun(text));
        return document;
    }

    private static Run NewRun(string text)
        => new(new Text(text) { Space = DocumentFormat.OpenXml.SpaceProcessingModeValues.Preserve });
}

public sealed class AusgelieferteDossierBilderTests
{
    [Fact]
    public void Logo_und_Wappen_liegen_im_Vorlagenordner_und_sind_lesbare_Bilder()
    {
        var wurzel = new AuswertungPro.Next.Infrastructure.Backup.RepositoryRootFileLocator()
            .Locate(AppContext.BaseDirectory);

        Assert.NotNull(wurzel);

        var logo = Path.Combine(wurzel!, "Export_Vorlage", "Dossier_Logo.png");
        var wappen = Path.Combine(wurzel!, "Export_Vorlage", "Dossier_Wappen.png");

        Assert.True(File.Exists(logo), $"'{logo}' fehlt.");
        Assert.True(File.Exists(wappen), $"'{wappen}' fehlt.");

        // Die Masse belegen zugleich, dass die beiden Dateien nicht vertauscht
        // sind: das Logo ist breiter als hoch, das Wappen hoeher als breit.
        // Bewusst keine exakten Pixelmasse: das Wappen wird fuer eine andere
        // Gemeinde ausgetauscht, ohne dass dabei etwas kaputt geht.
        Assert.True(ImageSizeReader.TryRead(File.ReadAllBytes(logo), out var logoW, out var logoH));
        Assert.True(logoW > logoH, $"Logo sollte breiter als hoch sein ({logoW}x{logoH}).");

        Assert.True(ImageSizeReader.TryRead(
            File.ReadAllBytes(wappen), out var wappenW, out var wappenH));
        Assert.True(wappenH > wappenW, $"Wappen sollte hoeher als breit sein ({wappenW}x{wappenH}).");
    }
}
