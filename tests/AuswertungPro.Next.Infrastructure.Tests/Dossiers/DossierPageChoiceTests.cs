using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.Application.Reports;
using AuswertungPro.Next.Infrastructure.Dossiers;

using UglyToad.PdfPig;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Dossiers;

/// <summary>
/// Die Blattauswahl vor dem Gesamt-PDF.
///
/// Pascal will alle Blätter auf einmal sehen und einzeln abwählen können,
/// bevor die Datei entsteht. Der Zusammenbau fragt deshalb zwischen
/// „zusammengeführt" und „geschrieben" nach — wer nichts fragt, bekommt wie
/// bisher alles.
///
/// Die Auswahl darf auch abbrechen. Dann wird nichts geschrieben, und eine
/// vorhandene Datei bleibt unverändert stehen.
/// </summary>
public sealed class DossierPageChoiceTests : IDisposable
{
    private readonly string _ordner = Path.Combine(
        Path.GetTempPath(), "dossier_wahl_" + Guid.NewGuid().ToString("N"));

    public DossierPageChoiceTests()
    {
        Directory.CreateDirectory(_ordner);
        File.WriteAllText(Path.Combine(_ordner, "Dossier.docx"), "Platzhalter");
    }

    public void Dispose()
    {
        try { Directory.Delete(_ordner, recursive: true); } catch { }
    }

    private static byte[] Pdf(int seiten)
    {
        using var speicher = new MemoryStream();
        using (var bauer = new PdfDocumentBuilder(speicher))
        {
            var schrift = bauer.AddStandard14Font(Standard14Font.Helvetica);

            for (var nummer = 1; nummer <= seiten; nummer++)
            {
                bauer.AddPage(595, 842).AddText(
                    $"Seite {nummer}",
                    12,
                    new UglyToad.PdfPig.Core.PdfPoint(50, 700),
                    schrift);
            }
        }

        return speicher.ToArray();
    }

    /// <summary>Ein Wandler, der statt Word einfach ein PDF mit N Seiten hinlegt.</summary>
    private Func<string, string?, bool> Wandler(int seiten)
        => (_, ziel) =>
        {
            File.WriteAllBytes(ziel!, Pdf(seiten));
            return true;
        };

    private DossierPdfAssemblyService Dienst(int seiten)
        => new(new DurchreichendeZusammenfuehrung(), Wandler(seiten));

    private string Ergebnisdatei
        => Path.Combine(_ordner, DossierFolderPlanner.CombinedPdfFileName);

    private int SeitenImErgebnis()
    {
        using var dokument = PdfDocument.Open(File.ReadAllBytes(Ergebnisdatei));
        return dokument.NumberOfPages;
    }

    [Fact]
    public async Task Ohne_Rueckfrage_bleibt_alles_wie_bisher()
    {
        var ergebnis = await Dienst(4).AssembleAsync(_ordner);

        Assert.True(ergebnis.Success, ergebnis.Message);
        Assert.Equal(4, SeitenImErgebnis());
    }

    [Fact]
    public async Task Die_Rueckfrage_bekommt_das_fertig_zusammengefuehrte_PDF()
    {
        var gesehen = 0;

        await Dienst(3).AssembleAsync(
            _ordner,
            (pdf, _) =>
            {
                using var dokument = PdfDocument.Open(pdf);
                gesehen = dokument.NumberOfPages;
                return Task.FromResult<IReadOnlySet<int>?>(new HashSet<int>());
            });

        Assert.Equal(3, gesehen);
    }

    [Fact]
    public async Task Ein_abgewaehltes_Blatt_fehlt_in_der_Datei()
    {
        var ergebnis = await Dienst(4).AssembleAsync(
            _ordner,
            (_, _) => Task.FromResult<IReadOnlySet<int>?>(new HashSet<int> { 2, 3 }));

        Assert.True(ergebnis.Success, ergebnis.Message);
        Assert.Equal(2, SeitenImErgebnis());
    }

    [Fact]
    public async Task Ein_Abbruch_schreibt_keine_Datei()
    {
        var ergebnis = await Dienst(3).AssembleAsync(
            _ordner,
            (_, _) => Task.FromResult<IReadOnlySet<int>?>(null));

        Assert.False(ergebnis.Success);
        Assert.False(File.Exists(Ergebnisdatei), "Trotz Abbruch wurde geschrieben.");
    }

    [Fact]
    public async Task Ein_Abbruch_laesst_eine_vorhandene_Datei_unveraendert()
    {
        await Dienst(3).AssembleAsync(_ordner);
        var vorher = File.ReadAllBytes(Ergebnisdatei);

        await Dienst(3).AssembleAsync(
            _ordner,
            (_, _) => Task.FromResult<IReadOnlySet<int>?>(null));

        Assert.Equal(vorher, File.ReadAllBytes(Ergebnisdatei));
    }

    [Fact]
    public async Task Alle_Blaetter_abzuwaehlen_wird_ehrlich_gemeldet()
    {
        var ergebnis = await Dienst(2).AssembleAsync(
            _ordner,
            (_, _) => Task.FromResult<IReadOnlySet<int>?>(new HashSet<int> { 1, 2 }));

        Assert.False(ergebnis.Success);
        Assert.Contains("Blatt", ergebnis.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(Ergebnisdatei));
    }

    /// <summary>Ohne Beilagen ist die Zusammenfuehrung eine Durchreiche.</summary>
    private sealed class DurchreichendeZusammenfuehrung : IPdfMergeService
    {
        public byte[] MergeWithOriginals(byte[] generatedPdf, IReadOnlyList<string> originalPdfPaths)
            => generatedPdf;

        public byte[] MergeOriginals(IReadOnlyList<string> originalPdfPaths)
            => Array.Empty<byte>();
    }
}
