using AuswertungPro.Next.Application.Dossiers.Preview;
using AuswertungPro.Next.Infrastructure.Dossiers;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace AuswertungPro.Next.Infrastructure.Tests.Dossiers;

/// <summary>
/// Jede Zelle einer Wiederholtabelle bekommt beim Erzeugen eine unsichtbare
/// Textmarke. Sie wird beim Umwandeln zum benannten Ziel der PDF und macht die
/// Zuordnung exakt - unabhaengig davon, ob die Zelle gefuellt, leer oder eine von
/// dreizehn Zellen mit dem Text „unbekannt" ist.
/// </summary>
public sealed class DocxFieldMarkerWriterTests : IDisposable
{
    private readonly string _ordner = Path.Combine(
        Path.GetTempPath(), "feldmarken", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Jede_Zelle_jeder_Zeile_bekommt_ihre_Marke()
    {
        using var doc = ThemenDokument();

        DocxPlaceholderFiller.FillRepeatingRows(
            doc,
            "Themen",
            [Zeile("Ausführungstermin", "unbekannt"), Zeile("Unternehmer", "unbekannt")],
            "keine");

        var namen = Marken(doc);

        foreach (var (zeile, spalte) in new[] { (0, "Thema"), (0, "Text"), (1, "Thema"), (1, "Text") })
        {
            var erwartet = DossierPdfFieldMarker.Name(
                DossierPreviewTarget.RowCell("Themen", zeile, spalte));
            Assert.Contains(erwartet, namen);
        }
    }

    [Fact]
    public void Eine_leere_Zelle_bekommt_ebenfalls_ihre_Marke()
    {
        // Genau der Fall, den die Zuordnung ueber den Text nie loesen konnte.
        using var doc = ThemenDokument();

        DocxPlaceholderFiller.FillRepeatingRows(
            doc, "Themen", [Zeile("Beilagen", "")], "keine");

        Assert.Contains(
            DossierPdfFieldMarker.Name(DossierPreviewTarget.RowCell("Themen", 0, "Text")),
            Marken(doc));
    }

    [Fact]
    public void Die_Marken_stoeren_den_sichtbaren_Text_nicht()
    {
        using var doc = ThemenDokument();

        DocxPlaceholderFiller.FillRepeatingRows(
            doc, "Themen", [Zeile("Unternehmer", "Muster AG")], "keine");

        var text = doc.MainDocumentPart!.Document.Body!.InnerText;

        Assert.Contains("Unternehmer", text, StringComparison.Ordinal);
        Assert.Contains("Muster AG", text, StringComparison.Ordinal);
        Assert.DoesNotContain("SSFELD", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Jede_Marke_hat_eine_eigene_Nummer()
    {
        // Doppelte w:id machen die Datei fuer Word ungueltig.
        using var doc = ThemenDokument();

        DocxPlaceholderFiller.FillRepeatingRows(
            doc,
            "Themen",
            [Zeile("A", "x"), Zeile("B", "y"), Zeile("C", "z")],
            "keine");

        var ids = doc.MainDocumentPart!.Document.Body!
            .Descendants<BookmarkStart>()
            .Select(marke => marke.Id?.Value)
            .ToList();

        Assert.NotEmpty(ids);
        Assert.All(ids, id => Assert.False(string.IsNullOrEmpty(id)));
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public void Jede_Marke_wird_wieder_geschlossen()
    {
        using var doc = ThemenDokument();

        DocxPlaceholderFiller.FillRepeatingRows(
            doc, "Themen", [Zeile("A", "x"), Zeile("B", "y")], "keine");

        var body = doc.MainDocumentPart!.Document.Body!;
        var starts = body.Descendants<BookmarkStart>()
            .Where(marke => DossierPdfFieldMarker.IsMarker(marke.Name?.Value))
            .Select(marke => marke.Id!.Value!)
            .ToHashSet(StringComparer.Ordinal);
        var enden = body.Descendants<BookmarkEnd>()
            .Select(marke => marke.Id!.Value!)
            .ToHashSet(StringComparer.Ordinal);

        Assert.NotEmpty(starts);
        Assert.True(starts.IsSubsetOf(enden), "Es gibt Marken ohne Abschluss.");
    }

    [Fact]
    public void Eine_vorhandene_Marke_der_Vorlage_behaelt_ihre_Nummer()
    {
        // Die Vorlage traegt Words eigene Verzeichnismarken. Sie duerfen weder
        // ueberschrieben noch dupliziert werden.
        using var doc = ThemenDokument(mitVorlagenmarke: true);

        DocxPlaceholderFiller.FillRepeatingRows(
            doc, "Themen", [Zeile("A", "x")], "keine");

        var vorlage = doc.MainDocumentPart!.Document.Body!
            .Descendants<BookmarkStart>()
            .Single(marke => marke.Name?.Value == "_Toc4711");

        Assert.Equal("7", vorlage.Id?.Value);
    }

    private static IReadOnlyDictionary<string, string> Zeile(string thema, string text)
        => new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Thema"] = thema,
            ["Text"] = text
        };

    private static IReadOnlyList<string> Marken(WordprocessingDocument doc)
        => doc.MainDocumentPart!.Document.Body!
            .Descendants<BookmarkStart>()
            .Select(marke => marke.Name?.Value ?? "")
            .ToList();

    private WordprocessingDocument ThemenDokument(bool mitVorlagenmarke = false)
    {
        Directory.CreateDirectory(_ordner);
        var pfad = Path.Combine(_ordner, $"{Guid.NewGuid():N}.docx");

        var doc = WordprocessingDocument.Create(pfad, WordprocessingDocumentType.Document);
        var main = doc.AddMainDocumentPart();
        var body = new Body();

        if (mitVorlagenmarke)
        {
            body.Append(new Paragraph(
                new BookmarkStart { Id = "7", Name = "_Toc4711" },
                new Run(new Text("Informationen Sanierung")),
                new BookmarkEnd { Id = "7" }));
        }

        var kopf = new TableRow(Zelle("Thema"), Zelle("Bemerkungen"));
        var vorlagenzeile = new TableRow(
            Zelle("{{#Themen}}{{Thema}}"),
            Zelle("{{Text}}"));

        body.Append(new Table(kopf, vorlagenzeile));
        main.Document = new Document(body);
        main.Document.Save();

        return doc;
    }

    private static TableCell Zelle(string text)
        => new(new Paragraph(new Run(new Text(text) { Space = SpaceProcessingModeValues.Preserve })));

    public void Dispose()
    {
        try { Directory.Delete(_ordner, recursive: true); } catch { }
    }
}
