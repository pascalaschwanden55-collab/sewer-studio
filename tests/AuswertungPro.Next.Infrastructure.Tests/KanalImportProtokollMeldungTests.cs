using System.IO;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Ein nicht verteiltes Haltungsprotokoll muss im Importbericht stehen. In Goeschenen
/// (2026-09-04) meldete der Lauf "0 Original-Protokolle, 0 Fehler", obwohl alle 239
/// Protokolle fehlten — der Verteiler uebersprang jedes Fehlerergebnis stillschweigend.
/// </summary>
public sealed class KanalImportProtokollMeldungTests
{
    [Fact]
    public void Nicht_verteilte_Protokolle_stehen_im_Bericht()
    {
        using var temp = new TempDirectory();
        var projekt = Path.Combine(temp.Path, "Projekt");
        var archiv = Path.Combine(projekt, "Importdateien", "PDF");
        var videos = Path.Combine(temp.Path, "Video");
        Directory.CreateDirectory(archiv);
        Directory.CreateDirectory(videos);

        SchreibeSammelprotokoll(Path.Combine(archiv, "gesamt.pdf"), "10052-9080", "9084-9085");

        // Der Zielordner einer Haltung ist durch eine gleichnamige Datei blockiert.
        var ziel = Path.Combine(projekt, ProjectStructure.HaltungenVerteilt);
        Directory.CreateDirectory(ziel);
        File.WriteAllText(Path.Combine(ziel, "9084-9085"), "blockiert");

        var ergebnis = new KanalImportDistributionService().Distribute(
            new Project(),
            projekt,
            archiv,
            videos,
            splitPdf: true);

        // Eine Haltung kommt durch, die andere nicht — und das steht sichtbar im Bericht.
        Assert.Equal(1, ergebnis.OriginalProtocolsDistributed);
        Assert.Contains(
            ergebnis.Messages,
            m => m.Contains("nicht verteilt", StringComparison.OrdinalIgnoreCase)
                 && m.Contains("9084-9085", StringComparison.Ordinal));
    }

    private static void SchreibeSammelprotokoll(string pfad, params string[] haltungen)
    {
        using var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);

        foreach (var haltung in haltungen)
        {
            var titel = builder.AddPage(PageSize.A4);
            titel.AddText($"Haltungsinspektion - 10.08.2026 - {haltung}", 12, new PdfPoint(40, 780), font);
            titel.AddText("Datum 10.08.2026", 10, new PdfPoint(40, 750), font);

            var folge = builder.AddPage(PageSize.A4);
            folge.AddText($"Haltungsinspektion - 10.08.2026 - {haltung}", 12, new PdfPoint(40, 780), font);
            folge.AddText("Befunde", 10, new PdfPoint(40, 750), font);
        }

        File.WriteAllBytes(pfad, builder.Build());
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "sewerstudio-kanalmeldung-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch
            {
                // Aufraeumen darf das Testergebnis nicht verdecken.
            }
        }
    }
}
