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
