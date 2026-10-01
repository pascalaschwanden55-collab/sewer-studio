using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using AuswertungPro.Next.Infrastructure.Dossiers;

using UglyToad.PdfPig;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Dossiers;

/// <summary>
/// Seiten aus dem fertigen Gesamt-PDF weglassen.
///
/// Pascal will vor dem Erzeugen alle Blätter sehen und einzeln abwählen
/// können. Weggelassen wird nur in der AUSGABE — die Word-Datei und die
/// Original-Protokolle bleiben unverändert. Genau deshalb arbeitet diese
/// Regel auf dem bereits zusammengeführten PDF und nicht an den Quellen.
/// </summary>
public sealed class DossierPdfPageFilterTests
{
    /// <summary>Ein PDF mit erkennbaren Seiten: Seite n trägt den Text „Seite n".</summary>
    private static byte[] Pdf(int seiten)
    {
        using var speicher = new MemoryStream();
        using (var bauer = new PdfDocumentBuilder(speicher))
        {
            var schrift = bauer.AddStandard14Font(Standard14Font.Helvetica);

            for (var nummer = 1; nummer <= seiten; nummer++)
            {
                var seite = bauer.AddPage(595, 842);
                seite.AddText(
                    $"Seite {nummer}",
                    12,
                    new UglyToad.PdfPig.Core.PdfPoint(50, 700),
                    schrift);
            }
        }

        return speicher.ToArray();
    }

    private static IReadOnlyList<string> Texte(byte[] pdf)
    {
        using var dokument = PdfDocument.Open(pdf);
        return dokument.GetPages().Select(seite => seite.Text).ToList();
    }

    [Fact]
    public void Ohne_Ausschluss_bleibt_das_PDF_unveraendert()
    {
        var original = Pdf(3);

        var gefiltert = DossierPdfPageFilter.Ohne(original, new HashSet<int>());

        Assert.Same(original, gefiltert);
    }

    [Fact]
    public void Eine_abgewaehlte_Seite_fehlt_im_Ergebnis()
    {
        var gefiltert = DossierPdfPageFilter.Ohne(Pdf(3), new HashSet<int> { 2 });

        var texte = Texte(gefiltert);
        Assert.Equal(2, texte.Count);
        Assert.Contains("Seite 1", texte[0], StringComparison.Ordinal);
        Assert.Contains("Seite 3", texte[1], StringComparison.Ordinal);
    }

    [Fact]
    public void Die_Reihenfolge_der_uebrigen_Seiten_bleibt()
    {
        var gefiltert = DossierPdfPageFilter.Ohne(Pdf(5), new HashSet<int> { 1, 4 });

        var texte = Texte(gefiltert);
        Assert.Equal(3, texte.Count);
        Assert.Contains("Seite 2", texte[0], StringComparison.Ordinal);
        Assert.Contains("Seite 3", texte[1], StringComparison.Ordinal);
        Assert.Contains("Seite 5", texte[2], StringComparison.Ordinal);
    }

    [Fact]
    public void Eine_unbekannte_Seitennummer_stoert_nicht()
    {
        var gefiltert = DossierPdfPageFilter.Ohne(Pdf(2), new HashSet<int> { 0, 7, -1 });

        Assert.Equal(2, Texte(gefiltert).Count);
    }

    [Fact]
    public void Alle_Seiten_abzuwaehlen_ist_kein_gueltiges_PDF_und_wird_abgelehnt()
    {
        // Ein PDF ohne Seiten waere kaputt. Lieber ehrlich scheitern als eine
        // unbrauchbare Datei schreiben.
        Assert.Throws<InvalidOperationException>(
            () => DossierPdfPageFilter.Ohne(Pdf(2), new HashSet<int> { 1, 2 }));
    }
}
