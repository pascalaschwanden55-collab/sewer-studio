using System.IO;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure;
using AuswertungPro.Next.Infrastructure.Import.Pdf;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Ein WinCan-GEP liefert alle Haltungsprotokolle in EINER Sammeldatei; der Verteiler teilt
/// sie in die Haltungsordner auf. Goeschenen (2026-09-04) verlor dabei alle 239 Protokolle,
/// weil das Sammel-PDF 1003 Seiten hat und die Seitengrenze bei 1000 lag — und weil ein
/// einziger Fehlschlag still die ganze Datei abbrach.
/// </summary>
public sealed class SammelprotokollVerteilungTests
{
    [Fact]
    public void Ein_echtes_GEP_Sammelprotokoll_passt_in_das_Seitenbudget()
    {
        // Goeschenen: 239 aufgenommene Haltungen ergaben 1003 Seiten (rund 4 Seiten je Haltung).
        // Das Budget muss ein ganzes Gemeinde-GEP tragen, nicht nur ein Quartier.
        Assert.True(PdfImportSafetyPolicy.CheckPageBudget(1003).Allowed);
        Assert.True(PdfImportSafetyPolicy.CheckPageBudget(4200).Allowed);

        // Der Schutz gegen wirklich pathologische Dateien bleibt bestehen.
        Assert.False(PdfImportSafetyPolicy.CheckPageBudget(50_000).Allowed);
    }

    [Fact]
    public void Eine_blockierte_Haltung_reisst_die_uebrigen_nicht_mit()
    {
        using var temp = new TempDirectory();
        var pdf = Path.Combine(temp.Path, "gesamtprotokoll.pdf");
        SchreibeSammelprotokoll(pdf, "10052-9080", "9084-9085", "9077-9078");

        var ziel = Path.Combine(temp.Path, "Verteilt");
        Directory.CreateDirectory(ziel);

        // Hindernis: Der Zielordner der mittleren Haltung existiert bereits als DATEI.
        // Frueher brach das die ganze Sammeldatei ab — alle drei Haltungen gingen verloren.
        File.WriteAllText(Path.Combine(ziel, "9084-9085"), "blockiert");

        var ergebnisse = HoldingFolderDistributor.DistributeFiles(
            pdfFiles: [pdf],
            videoSourceFolder: temp.Path,
            destGemeindeFolder: ziel,
            project: new Project());

        var erfolge = ergebnisse.Where(r => r.Success).ToList();
        Assert.Equal(2, erfolge.Count);
        Assert.Contains(erfolge, r => r.HoldingFolder!.EndsWith("10052-9080", StringComparison.Ordinal));
        Assert.Contains(erfolge, r => r.HoldingFolder!.EndsWith("9077-9078", StringComparison.Ordinal));

        // Der Fehlschlag verschwindet nicht: Er steht als eigenes Ergebnis mit Begruendung da.
        var fehlschlag = Assert.Single(ergebnisse.Where(r => !r.Success));
        Assert.False(string.IsNullOrWhiteSpace(fehlschlag.Message));

        Assert.True(File.Exists(Path.Combine(ziel, "10052-9080", "20260810_10052-9080.pdf")));
        Assert.True(File.Exists(Path.Combine(ziel, "9077-9078", "20260810_9077-9078.pdf")));
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "sewerstudio-sammelprotokoll-" + Guid.NewGuid().ToString("N"));
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

    /// <summary>Baut ein Sammelprotokoll im WinCan-Stil: je Haltung eine Titelseite und eine Folgeseite.</summary>
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
}
