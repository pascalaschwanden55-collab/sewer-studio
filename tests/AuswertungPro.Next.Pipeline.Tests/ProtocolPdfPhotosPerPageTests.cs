using AuswertungPro.Next.Application.Reports;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using UglyToad.PdfPig;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Die Anzahl Fotos je Seite ist einstellbar (1, 2, 4 oder 6). Geprueft wird am fertigen
/// PDF: nie mehr Fotos je Seite als eingestellt, alle Fotos gleich gross (ein am
/// Seitenende beschnittenes Foto waere niedriger) und keines ueber der Blattkante.
/// </summary>
public sealed class ProtocolPdfPhotosPerPageTests : IDisposable
{
    // 1x1 PNG - reicht QuestPDF als gueltiges Bild.
    private static readonly byte[] PngBytes = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");

    private const int PhotoCount = 7;

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "protokoll-fotos-je-seite",
        Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(6)]
    public void Eine_Seite_traegt_hoechstens_die_eingestellte_Anzahl_Fotos(int perPage)
    {
        var bytes = BuildProtocol(perPage);

        using var pdf = PdfDocument.Open(bytes);
        var jeSeite = pdf.GetPages().Select(p => p.GetImages().Count()).ToList();

        Assert.All(jeSeite, count => Assert.InRange(count, 0, perPage));
        Assert.Equal(PhotoCount, jeSeite.Sum());

        // Erste Seite ist die Protokollseite, danach genau die noetigen Fotoseiten.
        var erwarteteFotoseiten = (int)Math.Ceiling(PhotoCount / (double)perPage);
        Assert.Equal(erwarteteFotoseiten + 1, jeSeite.Count);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(6)]
    public void Alle_Fotos_werden_gleich_gross_gezeichnet(int perPage)
    {
        var bytes = BuildProtocol(perPage);

        using var pdf = PdfDocument.Open(bytes);
        var hoehen = pdf.GetPages()
            .SelectMany(p => p.GetImages())
            .Select(i => Math.Round(i.Bounds.Height, 1))
            .ToList();

        Assert.Equal(PhotoCount, hoehen.Count);
        Assert.True(
            hoehen.Max() - hoehen.Min() <= 0.5,
            $"Fotohoehen weichen ab: {string.Join(", ", hoehen)}");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(6)]
    public void Kein_Foto_ragt_ueber_die_Blattkante_hinaus(int perPage)
    {
        var bytes = BuildProtocol(perPage);

        using var pdf = PdfDocument.Open(bytes);
        foreach (var page in pdf.GetPages())
        {
            foreach (var image in page.GetImages())
            {
                Assert.True(image.Bounds.Bottom >= -0.5, $"Foto unten ausserhalb: {image.Bounds}");
                Assert.True(image.Bounds.Top <= page.Height + 0.5, $"Foto oben ausserhalb: {image.Bounds}");
                Assert.True(image.Bounds.Left >= -0.5, $"Foto links ausserhalb: {image.Bounds}");
                Assert.True(image.Bounds.Right <= page.Width + 0.5, $"Foto rechts ausserhalb: {image.Bounds}");
            }
        }
    }

    [Fact]
    public void Ohne_Einstellung_bleiben_es_zwei_Fotos_je_Seite()
    {
        // Waechter: der Standard darf das bisherige Protokoll nicht veraendern.
        var ohne = BuildProtocol(photosPerPage: null);
        var mitZwei = BuildProtocol(2);

        using var pdfOhne = PdfDocument.Open(ohne);
        using var pdfZwei = PdfDocument.Open(mitZwei);

        Assert.Equal(
            pdfZwei.GetPages().Select(p => p.GetImages().Count()),
            pdfOhne.GetPages().Select(p => p.GetImages().Count()));
        Assert.Equal(
            pdfZwei.GetPages().SelectMany(p => p.GetImages()).Select(i => Math.Round(i.Bounds.Height, 1)),
            pdfOhne.GetPages().SelectMany(p => p.GetImages()).Select(i => Math.Round(i.Bounds.Height, 1)));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(6)]
    public void Jede_Fotoseite_traegt_ihre_eigene_Kopfzeile(int perPage)
    {
        // Jede Fotogruppe zeichnet Titel und Kopftabelle selbst. Rutschen Fotos statt
        // dessen durch einen automatischen Umbruch auf eine Folgeseite, fehlt dort der Kopf.
        var bytes = BuildProtocol(perPage);

        using var pdf = PdfDocument.Open(bytes);
        var fotoseiten = pdf.GetPages().Where(p => p.GetImages().Any()).ToList();

        Assert.NotEmpty(fotoseiten);
        Assert.All(fotoseiten, page =>
            Assert.Contains("Haltung", page.Text, StringComparison.Ordinal));
    }

    [Fact]
    public void Vier_Fotos_je_Seite_stehen_in_zwei_Spalten()
    {
        var bytes = BuildProtocol(4);

        using var pdf = PdfDocument.Open(bytes);
        var ersteFotoseite = pdf.GetPage(2).GetImages().ToList();

        Assert.Equal(4, ersteFotoseite.Count);
        var spalten = ersteFotoseite.Select(i => Math.Round(i.Bounds.Left, 0)).Distinct().Count();
        Assert.Equal(2, spalten);
    }

    private byte[] BuildProtocol(int? photosPerPage)
    {
        Directory.CreateDirectory(_root);

        var entries = new List<ProtocolEntry>();
        for (var i = 0; i < PhotoCount; i++)
        {
            var name = $"foto{i}.png";
            File.WriteAllBytes(Path.Combine(_root, name), PngBytes);
            entries.Add(new ProtocolEntry
            {
                Code = "BAB",
                Beschreibung = $"Riss {i}",
                MeterStart = i + 1,
                FotoPaths = [name],
                Source = ProtocolEntrySource.Imported
            });
        }

        var document = new ProtocolDocument
        {
            HaltungId = "100-200",
            Current = new ProtocolRevision { Entries = entries }
        };
        var record = new HaltungRecord { Protocol = document };
        record.SetFieldValue("Haltungsname", "100-200", FieldSource.Manual, userEdited: true);
        record.SetFieldValue("Haltungslaenge_m", "10", FieldSource.Manual, userEdited: true);

        var options = new HaltungsprotokollPdfOptions
        {
            IncludePhotos = true,
            IncludeHaltungsgrafik = false,
            IncludeObservationTable = false
        };
        if (photosPerPage is int value)
            options = options with { PhotosPerPage = value };

        return new ProtocolPdfExporter().BuildHaltungsprotokollPdf(
            new Project { Name = "Fototest" },
            record,
            document,
            _root,
            options);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}
