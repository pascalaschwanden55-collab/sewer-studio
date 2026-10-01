using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Infrastructure.Import.Pdf;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

/// <summary>
/// Die Wahl des PDF-Textlesers darf nicht dem Zufall des jeweiligen Rechners folgen.
/// Ein ungeeignetes pdftotext liefert die Anschlusstabelle zeilenverschoben und damit
/// falsche Tiefen, ohne Fehlermeldung (gemessen an Xpdf 4.00, 19.09.2026).
/// Diese Tests verwenden ein vorgetaeuschtes pdftotext, das seine Version meldet und
/// jeden echten Extraktionsversuch in einer Markierungsdatei festhaelt.
/// </summary>
public sealed class PdfLeserWahlTests : IDisposable
{
    private readonly string _ordner = Path.Combine(
        Path.GetTempPath(), "pdfleser_" + Guid.NewGuid().ToString("N"));

    public PdfLeserWahlTests() => Directory.CreateDirectory(_ordner);

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_ordner))
                Directory.Delete(_ordner, recursive: true);
        }
        catch
        {
            // Ein gesperrter Temp-Ordner darf den Testlauf nicht faerben.
        }
    }

    [Fact]
    public void Ein_ungeeignetes_pdftotext_wird_nicht_einmal_aufgerufen()
    {

        var markierung = Path.Combine(_ordner, "wurde_aufgerufen.txt");
        var falsches = SchreibeVorgetaeuschtesPdfToText(
            "pdftotext version 4.00",
            "Copyright 1996-2017 Glyph ^& Cog, LLC",
            markierung);
        var pdf = SchreibePdf("Schacht 4711 Anschluesse");

        var ergebnis = new PdfTextExtractionService().ExtractPages(pdf, falsches);

        Assert.Equal(PdfLeserArt.Eingebaut, ergebnis.Leser);
        Assert.Contains("Xpdf", ergebnis.LeserHinweis, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(markierung), "Ein ungeeignetes pdftotext darf keine Seite lesen duerfen.");
        Assert.Contains("Schacht 4711", ergebnis.FullText, StringComparison.Ordinal);
    }

    [Fact]
    public void Ein_unbekanntes_Programm_wird_ebenfalls_abgelehnt()
    {

        var markierung = Path.Combine(_ordner, "unbekannt_aufgerufen.txt");
        var fremdes = SchreibeVorgetaeuschtesPdfToText("irgendein Werkzeug 9.9", "ohne Herstellerangabe", markierung);
        var pdf = SchreibePdf("Fremdes Werkzeug");

        var ergebnis = new PdfTextExtractionService().ExtractPages(pdf, fremdes);

        Assert.Equal(PdfLeserArt.Eingebaut, ergebnis.Leser);
        Assert.False(File.Exists(markierung));
    }

    [Fact]
    public void Ein_geeignetes_pdftotext_wird_verwendet()
    {

        // Meldet sich als geprueftes Poppler und liefert bei der Extraktion eine echte Textdatei.
        var markierung = Path.Combine(_ordner, "poppler_aufgerufen.txt");
        var echtes = SchreibeVorgetaeuschtesPdfToText(
            "pdftotext version 25.07.0",
            "Copyright 2005-2025 The Poppler Developers - http://poppler.freedesktop.org",
            markierung,
            ausgabetext: "KENNUNG AUS DEM VORGETAEUSCHTEN POPPLER");
        var pdf = SchreibePdf("Diesen Text darf der eingebaute Leser liefern");

        var ergebnis = new PdfTextExtractionService().ExtractPages(pdf, echtes);

        Assert.Equal(PdfLeserArt.PdfToText, ergebnis.Leser);
        Assert.True(File.Exists(markierung), "Ein geprueftes pdftotext muss weiterhin verwendet werden.");
        Assert.Contains("VORGETAEUSCHTEN POPPLER", ergebnis.FullText, StringComparison.Ordinal);
    }

    [Fact]
    public void Ein_geeignetes_aber_fehlerhaftes_pdftotext_faellt_auf_den_eingebauten_Leser_zurueck()
    {

        var markierung = Path.Combine(_ordner, "fehler_aufgerufen.txt");
        var kaputtes = SchreibeVorgetaeuschtesPdfToText(
            "pdftotext version 25.07.0",
            "Copyright 2005-2025 The Poppler Developers",
            markierung,
            ausgabetext: null,
            fehlercode: 3);
        var pdf = SchreibePdf("Rueckfall nach Prozessfehler");

        var ergebnis = new PdfTextExtractionService().ExtractPages(pdf, kaputtes);

        Assert.Equal(PdfLeserArt.Eingebaut, ergebnis.Leser);
        Assert.True(File.Exists(markierung));
        Assert.Contains("Rueckfall nach Prozessfehler", ergebnis.FullText, StringComparison.Ordinal);
    }

    /// <summary>
    /// Schreibt eine Batchdatei, die sich bei <c>-v</c> mit der angegebenen Version meldet
    /// und jeden anderen Aufruf in der Markierungsdatei festhaelt.
    /// </summary>
    private string SchreibeVorgetaeuschtesPdfToText(
        string versionszeile,
        string zweiteZeile,
        string markierung,
        string? ausgabetext = null,
        int fehlercode = 0)
    {
        // Der Dateiname enthaelt eine Guid, damit das Urteil je Pfad nicht aus einem
        // frueheren Test stammt: Der Dienst merkt sich die Beurteilung je Programmpfad.
        var pfad = Path.Combine(_ordner, $"pdftotext_{Guid.NewGuid():N}.cmd");
        var zeilen = new List<string>
        {
            "@echo off",
            "if \"%1\"==\"-v\" (",
            $"  echo {versionszeile} 1>&2",
            $"  echo {zweiteZeile} 1>&2",
            "  exit /b 99",
            ")",
            $"echo aufgerufen> \"{markierung}\""
        };

        if (ausgabetext is not null)
        {
            // Der letzte Parameter ist die Zieldatei: -enc UTF-8 -layout <pdf> <ziel>
            zeilen.Add($"echo {ausgabetext}> %5");
            zeilen.Add("exit /b 0");
        }
        else
        {
            zeilen.Add($"exit /b {fehlercode}");
        }

        File.WriteAllLines(pfad, zeilen);
        return pfad;
    }

    private string SchreibePdf(string text)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var bytes = Document.Create(dokument =>
        {
            dokument.Page(seite =>
            {
                seite.Size(PageSizes.A4);
                seite.Margin(20);
                seite.Content().Text(text);
            });
        }).GeneratePdf();

        var pfad = Path.Combine(_ordner, $"probe_{Guid.NewGuid():N}.pdf");
        File.WriteAllBytes(pfad, bytes);
        return pfad;
    }
}
