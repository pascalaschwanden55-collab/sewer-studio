using AuswertungPro.Next.Application.Reports;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using UglyToad.PdfPig;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Die Fotoseiten des Haltungsdossiers hatten eine eigene, fehlerhafte Anordnung:
/// Jedes Foto war ein normaler Listeneintrag ohne Umbruchschutz, deshalb wurde das
/// dritte Foto jeder Seite an der Blattkante zerschnitten. Der Rahmen nahm ausserdem
/// die volle Textbreite, das Bild aber nur einen Teil davon.
/// Das Dossier verwendet jetzt dieselbe Fotoseiten-Logik wie das Haltungsprotokoll
/// (<c>ProtocolPdfPhotoSection</c>): zwei Fotos je Seite, mittig, mit laufender Nummer.
/// </summary>
public sealed class HaltungsDossierPhotoLayoutTests : IDisposable
{
    // 1x1 PNG - reicht QuestPDF als gueltiges Bild.
    private static readonly byte[] PngBytes = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");

    private const string Holding = "45570-1099829";

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "dossier-photo-layout",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void Eine_Fotoseite_traegt_hoechstens_zwei_Fotos()
    {
        // Regression: vorher standen drei Fotos je Seite, das dritte war halbiert.
        var bytes = BuildDossier([1, 1, 1, 1, 1]);

        using var pdf = PdfDocument.Open(bytes);
        var perPage = pdf.GetPages().Select(p => p.GetImages().Count()).ToList();

        Assert.All(perPage, count => Assert.InRange(count, 0, 2));
        Assert.Equal(5, perPage.Sum());
        // Genau 3 Seiten (2+2+1) - keine leere Seite vor der ersten Fotogruppe.
        Assert.Equal(3, perPage.Count);
    }

    [Fact]
    public void Alle_Fotos_werden_gleich_gross_gezeichnet()
    {
        // Ein am Seitenende beschnittenes Foto wird niedriger gezeichnet als die
        // uebrigen. Gleiche Quellbilder muessen deshalb gleich hoch herauskommen.
        var bytes = BuildDossier([1, 1, 1, 1, 1]);

        using var pdf = PdfDocument.Open(bytes);
        var heights = pdf.GetPages()
            .SelectMany(p => p.GetImages())
            .Select(i => Math.Round(i.Bounds.Height, 1))
            .ToList();

        Assert.Equal(5, heights.Count);
        Assert.True(
            heights.Max() - heights.Min() <= 0.5,
            $"Fotohoehen weichen ab: {string.Join(", ", heights)}");
    }

    [Fact]
    public void Kein_Foto_ragt_ueber_die_Blattkante_hinaus()
    {
        var bytes = BuildDossier([1, 1, 1, 1, 1]);

        using var pdf = PdfDocument.Open(bytes);
        foreach (var page in pdf.GetPages())
        {
            foreach (var image in page.GetImages())
            {
                var b = image.Bounds;
                Assert.True(b.Bottom >= -0.5, $"Seite {page.Number}: Foto ragt unten hinaus (y={b.Bottom:F1}).");
                Assert.True(b.Top <= page.Height + 0.5, $"Seite {page.Number}: Foto ragt oben hinaus (y={b.Top:F1}).");
                Assert.True(b.Left >= -0.5, $"Seite {page.Number}: Foto ragt links hinaus (x={b.Left:F1}).");
                Assert.True(b.Right <= page.Width + 0.5, $"Seite {page.Number}: Foto ragt rechts hinaus (x={b.Right:F1}).");
            }
        }
    }

    [Fact]
    public void Jedes_Foto_traegt_seine_laufende_Nummer_und_die_Codebeschriftung()
    {
        var bytes = BuildDossier([1, 1, 1]);

        using var pdf = PdfDocument.Open(bytes);
        var text = Text(pdf);

        // Genau das Format des Haltungsprotokolls: "<Nr>. <Meter> m" und darunter Code + Klartext.
        Assert.Contains("1. 1.00 m", text);
        Assert.Contains("2. 2.00 m", text);
        Assert.Contains("3. 3.00 m", text);
        Assert.Contains("BABBB Riss radial", text);
    }

    [Fact]
    public void Die_Beobachtungstabelle_nennt_dieselben_Fotonummern()
    {
        // Ohne die Foto-Spalte zeigen die Nummern unter den Bildern auf nichts.
        // Erster Befund traegt zwei Fotos -> Zelle "1,2", zweiter Befund -> "3".
        var bytes = BuildDossier([2, 1], includeProtokoll: true);

        using var pdf = PdfDocument.Open(bytes);
        var protocolPage = pdf.GetPage(1).Text;

        Assert.Contains("Foto", protocolPage);
        Assert.Contains("1,2", protocolPage);
    }

    [Fact]
    public void Ohne_Fotos_entsteht_keine_leere_Fotoseite()
    {
        var bytes = BuildDossier([], includeProtokoll: true);

        using var pdf = PdfDocument.Open(bytes);

        Assert.Equal(1, pdf.NumberOfPages);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(6)]
    public void Die_eingestellte_Anzahl_Fotos_je_Seite_gilt_auch_im_Dossier(int perPage)
    {
        // Dossier und Haltungsprotokoll zeichnen die Fotoseiten mit derselben Klasse -
        // die Einstellung muss deshalb auch hier ankommen.
        var bytes = BuildDossier([1, 1, 1, 1, 1, 1, 1], photosPerPage: perPage);

        using var pdf = PdfDocument.Open(bytes);
        var jeSeite = pdf.GetPages().Select(p => p.GetImages().Count()).ToList();

        Assert.All(jeSeite, count => Assert.InRange(count, 0, perPage));
        Assert.Equal(7, jeSeite.Sum());
        Assert.Equal((int)Math.Ceiling(7 / (double)perPage), jeSeite.Count(c => c > 0));
    }

    private static string Text(PdfDocument pdf)
        => string.Join("\n", pdf.GetPages().Select(p => p.Text));

    /// <param name="photosPerEntry">Je Eintrag die Anzahl Fotos.</param>
    private byte[] BuildDossier(int[] photosPerEntry, bool includeProtokoll = false, int? photosPerPage = null)
    {
        var photoDir = Path.Combine(_root, "Fotos", "Haltungen", Holding);
        Directory.CreateDirectory(photoDir);

        var record = new HaltungRecord();
        record.SetFieldValue("Haltungsname", Holding, FieldSource.Xtf, userEdited: false);
        record.SetFieldValue("Haltungslaenge_m", "22.47", FieldSource.Xtf, userEdited: false);

        var doc = new ProtocolDocument { HaltungId = Holding };
        var fileIndex = 0;
        for (var entryIndex = 0; entryIndex < photosPerEntry.Length; entryIndex++)
        {
            var entry = new ProtocolEntry
            {
                EntryId = Guid.NewGuid(),
                Code = "BABBB",
                Beschreibung = "Riss radial",
                MeterStart = entryIndex + 1.0,
            };

            for (var i = 0; i < photosPerEntry[entryIndex]; i++)
            {
                var name = $"foto_{fileIndex++}.png";
                File.WriteAllBytes(Path.Combine(photoDir, name), PngBytes);
                entry.FotoPaths.Add(Path.Combine("Fotos", "Haltungen", Holding, name));
            }

            doc.Current.Entries.Add(entry);
        }

        record.Protocol = doc;

        var options = new DossierPrintOptions
        {
            IncludeDeckblatt = false,
            IncludeHaltungsprotokoll = includeProtokoll,
            IncludeFotos = true,
            IncludeSchachtVon = false,
            IncludeSchachtBis = false,
            IncludeHydraulik = false,
            IncludeKostenschaetzung = false,
            IncludeOriginalProtokolle = false,
            PhotosPerPage = photosPerPage,
        };

        return HaltungsDossierPdfBuilder.Build(new Project(), record, null, null, null, _root, options);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { }
    }
}
