using System.Collections.Generic;
using AuswertungPro.Next.Infrastructure.Import.Common;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

/// <summary>
/// Charakterisierungstests fuer <see cref="PdfFileIndexHelper.ResolvePdfMatches"/>.
/// Stellt sicher, dass der extrahierte Helfer dieselbe Semantik wie die
/// urspruenglichen dreifach-duplizierten LINQ-Bloecke in WinCan/IBAK hat.
/// </summary>
public class PdfFileIndexHelperTests
{
    private static Dictionary<string, List<string>> BuildIndex(params (string Name, string[] Paths)[] entries)
    {
        var dict = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, paths) in entries)
            dict[name] = new List<string>(paths);
        return dict;
    }

    [Fact]
    public void KeineEintraege_GibtLeereListeZurueck()
    {
        var index = BuildIndex();
        var result = PdfFileIndexHelper.ResolvePdfMatches(index, "100-200");
        Assert.Empty(result);
    }

    [Fact]
    public void PdfMitPassendemKey_WirdAufgeloest()
    {
        var index = BuildIndex(("100-200_Inspektion.pdf", new[] { @"C:\Export\100-200_Inspektion.pdf" }));
        var result = PdfFileIndexHelper.ResolvePdfMatches(index, "100-200");
        Assert.Single(result);
        Assert.Equal(@"C:\Export\100-200_Inspektion.pdf", result[0]);
    }

    [Fact]
    public void MehrerePdfsPassend_AlleWerdenZurueckgegeben()
    {
        var index = BuildIndex(
            ("100-200_A.pdf", new[] { @"C:\A.pdf" }),
            ("100-200_B.pdf", new[] { @"C:\B.pdf" }));
        var result = PdfFileIndexHelper.ResolvePdfMatches(index, "100-200");
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void PdfOhnePassendenKey_WirdNichtZurueckgegeben()
    {
        var index = BuildIndex(("300-400_Bericht.pdf", new[] { @"C:\300-400.pdf" }));
        var result = PdfFileIndexHelper.ResolvePdfMatches(index, "100-200");
        Assert.Empty(result);
    }

    [Fact]
    public void KuerzererKey_TrifftNichtAufPdfDerLaengerenHaltung()
    {
        var index = BuildIndex((
            "100-2000_Inspektion.pdf",
            new[] { @"C:\Export\100-2000_Inspektion.pdf" }));

        var result = PdfFileIndexHelper.ResolvePdfMatches(index, "100-200");

        Assert.Empty(result);
    }

    [Fact]
    public void NichtPdfDatei_WirdIgnoriert()
    {
        var index = BuildIndex(
            ("100-200.mp4", new[] { @"C:\video.mp4" }),
            ("100-200_report.pdf", new[] { @"C:\report.pdf" }));
        var result = PdfFileIndexHelper.ResolvePdfMatches(index, "100-200");
        Assert.Single(result);
    }

    [Fact]
    public void MehrdeutigerPfad_WirdNichtAufgeloest()
    {
        // Dateiname ist zweimal im Dateisystem vorhanden (Mehrdeutigkeit) -> null -> wird gefiltert
        var index = BuildIndex(("100-200.pdf", new[] { @"C:\A\100-200.pdf", @"C:\B\100-200.pdf" }));
        var result = PdfFileIndexHelper.ResolvePdfMatches(index, "100-200");
        Assert.Empty(result);
    }

    [Fact]
    public void KeyVergleichCaseInsensitive()
    {
        var index = BuildIndex(("100-200_Bericht.PDF", new[] { @"C:\Bericht.PDF" }));
        var result = PdfFileIndexHelper.ResolvePdfMatches(index, "100-200");
        Assert.Single(result);
    }
}

/// <summary>
/// Doppelablage im Kundenexport: WinCan legt dieselben Section-PDFs zweimal ab
/// (DISK1\Section_PDF und Projects\...\Misc\Docu\Section_PDF). Beide Kopien sind
/// byte-identisch. Frueher galt das als "mehrdeutig" und ALLE Haltungsprotokolle
/// gingen verloren (real gemessen: 0 von 38).
/// </summary>
public class PdfFileIndexHelperDoppelablageTests : IDisposable
{
    private readonly string _wurzel = Path.Combine(
        Path.GetTempPath(), $"pdfindex-doppel-{Guid.NewGuid():N}");

    private string SchreibeDatei(string unterordner, string name, string inhalt)
    {
        var ordner = Path.Combine(_wurzel, unterordner);
        Directory.CreateDirectory(ordner);
        var pfad = Path.Combine(ordner, name);
        File.WriteAllText(pfad, inhalt);
        return pfad;
    }

    [Fact]
    public void ZweiKopienMitGleichemInhalt_WerdenAlsEineDateiAufgeloest()
    {
        var a = SchreibeDatei("A", "Section_8_892037-74091.pdf", "identischer inhalt");
        var b = SchreibeDatei("B", "Section_8_892037-74091.pdf", "identischer inhalt");

        var index = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Section_8_892037-74091.pdf"] = new List<string> { a, b }
        };

        var treffer = PdfFileIndexHelper.ResolvePdfMatches(index, "892037-74091");

        Assert.Single(treffer);
        Assert.Equal(a, treffer[0]);
    }

    [Fact]
    public void ZweiKopienMitVerschiedenemInhalt_BleibenMehrdeutig()
    {
        var a = SchreibeDatei("A", "892037-74091.pdf", "stand eins");
        var b = SchreibeDatei("B", "892037-74091.pdf", "stand zwei - anderer inhalt");

        var index = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["892037-74091.pdf"] = new List<string> { a, b }
        };

        var treffer = PdfFileIndexHelper.ResolvePdfMatches(index, "892037-74091");

        Assert.Empty(treffer);
    }

    public void Dispose()
    {
        try { Directory.Delete(_wurzel, recursive: true); } catch { }
    }
}
