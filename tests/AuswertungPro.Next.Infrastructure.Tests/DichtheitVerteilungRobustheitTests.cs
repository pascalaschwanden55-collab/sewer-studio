using AuswertungPro.Next.Infrastructure;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Anlass 18.09.2026 (acht KIT-PDFs, null Erfolge): Ein Fehler auf einer Seite beendete
/// die ganze Datei, weil der try/catch um das gesamte PDF lag. Und Behaelterpruefungen
/// (Regenbecken) wurden als "Haltung nicht erkannt" abgewiesen statt eigenstaendig abgelegt.
/// </summary>
public sealed class DichtheitVerteilungRobustheitTests
{
    [Fact]
    public void DistributeDichtheit_SeiteScheitert_VerteiltDieUebrigenSeitenTrotzdem()
    {
        using var temp = new TempDirectory();
        var ziel = Path.Combine(temp.Path, "Verteilt");
        Directory.CreateDirectory(ziel);

        var pdf = Path.Combine(temp.Path, "zwei_haltungen.pdf");
        WritePdf(pdf,
            ["Datum 2026/07/12", "Prufgegenstand / Haltung    1000-1001"],
            ["Datum 2026/07/12", "Prufgegenstand / Haltung    2000-2001"]);

        // Eine DATEI mit dem Namen des benoetigten Ordners blockiert Seite 1.
        File.WriteAllText(Path.Combine(ziel, "1000-1001"), "blockiert");

        var ergebnisse = HoldingFolderDistributor.DistributeDichtheitFiles([pdf], ziel);

        var erfolg = Assert.Single(ergebnisse, e => e.Success);
        Assert.Contains("2000-2001", erfolg.Message);
        Assert.True(File.Exists(Path.Combine(ziel, "2000-2001", "20260712_2000-2001_DP.pdf")));

        var fehler = Assert.Single(ergebnisse, e => !e.Success);
        Assert.Contains("Seite 1", fehler.Message);
    }

    [Fact]
    public void DistributeDichtheit_Behaelterpruefung_LegtBauwerksordnerAn()
    {
        using var temp = new TempDirectory();
        var ziel = Path.Combine(temp.Path, "Verteilt");
        Directory.CreateDirectory(ziel);

        var pdf = Path.Combine(temp.Path, "beckenmessung.pdf");
        WritePdf(pdf,
            [
                "Pegel-Dichtheitsprufung nach SIA190:2017/VSARLDicht:2023 (Wasser)",
                "Bauvorhaben:   RB 2 Ellbogenkapelle",
                "Prufobjekt:    6473 Silenen",
                "               RB2",
                "Priifdurchfuhrung:  Erstprufung (Grundwasserschutzzone) Behalter",
                "Beginn SSttigung:   16.09.202612:03:18"
            ],
            [
                "Pegel-Dichtheitsprufung nach SIA190:2017 /VSA RL Dicht:2023 (Wasser)",
                "Hohe oberer Schachtring [m] 0.000",
                "Hohe unterer Schachtring [m]  4.000"
            ]);

        var ergebnis = Assert.Single(HoldingFolderDistributor.DistributeDichtheitFiles([pdf], ziel));

        Assert.True(ergebnis.Success, ergebnis.Message);
        Assert.Equal(Path.Combine(ziel, "RB2"), ergebnis.HoldingFolder);
        Assert.True(File.Exists(Path.Combine(ziel, "RB2", "20260916_RB2_BP.pdf")));
        // Die Masszeilen der Anlage duerfen keine Haltung erfinden.
        Assert.False(Directory.Exists(Path.Combine(ziel, "000-4000")));
        Assert.False(Directory.Exists(Path.Combine(ziel, "keine_Zuordnung")));
    }

    [Fact]
    public void DistributeDichtheit_BehaelterOhneObjektname_LegtNichtsAbUndSagtWarum()
    {
        using var temp = new TempDirectory();
        var ziel = Path.Combine(temp.Path, "Verteilt");
        Directory.CreateDirectory(ziel);

        var pdf = Path.Combine(temp.Path, "unbenannt.pdf");
        WritePdf(pdf,
            [
                "Pegel-Dichtheitsprufung nach SIA190:2017/VSARLDicht:2023 (Wasser)",
                "Prufobjekt:    Klaerbecken der Gemeinde",
                "Priifdurchfuhrung:  Erstprufung (Grundwasserschutzzone) Behalter"
            ]);

        var ergebnis = Assert.Single(HoldingFolderDistributor.DistributeDichtheitFiles([pdf], ziel));

        Assert.False(ergebnis.Success);
        Assert.Contains("Behälterprüfung", ergebnis.Message);
        Assert.Empty(Directory.GetDirectories(ziel));
    }

    private static void WritePdf(string path, params string[][] pages)
    {
        using var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        foreach (var zeilen in pages)
        {
            var page = builder.AddPage(PageSize.A4);
            var y = 780;
            foreach (var zeile in zeilen)
            {
                page.AddText(zeile, 10, new PdfPoint(40, y), font);
                y -= 16;
            }
        }
        File.WriteAllBytes(path, builder.Build());
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "sewerstudio-dichtheit-robust-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            try { Directory.Delete(Path, recursive: true); } catch { }
        }
    }
}
