using System;
using System.Reflection;
using AuswertungPro.Next.Infrastructure;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Tests fuer die robuste Haltungspaar-Erkennung in Dichtheitspruefungs-PDFs.
/// Die Zeilen stammen 1:1 aus echten KIT-Bauinspekt-PDFs (Abwasser Uri), inkl. der
/// OCR-kaputten Trennzeichen ("-^", "-+", "->"). Schachtnummern sind 4- bis 6-stellig.
/// Sichert ab, dass diese Faelle nicht wieder durchs Raster fallen (frueher: nur 5-stellig).
/// </summary>
public sealed class DichtheitPairLineParsingTests
{
    private static (string A, string B)? Match(string line)
    {
        var m = typeof(HoldingFolderDistributor).GetMethod(
            "TryMatchDichtheitPairLine",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        var result = m.Invoke(null, new object?[] { line });
        if (result is null) return null;
        var t = result.GetType();
        return ((string)t.GetField("Item1")!.GetValue(result)!,
                (string)t.GetField("Item2")!.GetValue(result)!);
    }

    [Theory]
    // Saubere Trenner, verschiedene Stelligkeiten
    [InlineData("6927 -+ 6926", "6927", "6926")]              // 4-stellig, OCR "-+"
    [InlineData("6928 -> 6927", "6928", "6927")]              // 4-stellig, "->"
    [InlineData("993170-^614445", "993170", "614445")]       // 6-stellig, "-^", kein Leerraum
    [InlineData("865 -> 864", "865", "864")]                  // 3-stellig zu kurz -> KEIN Treffer erwartet? s.u.
    [InlineData("Prufgegenstand / Haltung 6928 -> 6927", "6928", "6927")]
    [InlineData("07.993164 -> 993162", "07.993164", "993162")] // gepunkteter Praefix
    public void Match_RealOcrLines_ExtractsBothShafts(string line, string expA, string expB)
    {
        // 865/864 sind 3-stellig und liegen unter der 4-stellig-Schwelle -> separat behandelt.
        if (expA.Length < 4)
        {
            Assert.Null(Match(line));
            return;
        }

        var r = Match(line);
        Assert.NotNull(r);
        Assert.Equal(expA, r!.Value.A);
        Assert.Equal(expB, r.Value.B);
    }

    [Theory]
    [InlineData("70.51 m")]                       // Laenge, kein Paar
    [InlineData("155.00 m")]                      // Laenge
    [InlineData("Prufdruck: 1000.0 mbar")]        // Messwert
    [InlineData("26/05/13 07:36:15 46.62 N")]     // Datum/GPS
    [InlineData("gepruft bei 6926, 9490")]        // nur eine Nummer
    [InlineData("6926 6926")]                      // gleiche Nummer (kein Trenner) -> kein Paar
    [InlineData("Software: 2.16.2/2.16.2/P0-0")]  // Versions-Nummern
    public void Match_NonPairLines_ReturnsNull(string line)
    {
        Assert.Null(Match(line));
    }

    [Fact]
    public void Match_OcrDestroyedFirstShaft_ReturnsNull()
    {
        // "993^-<j-^.993160": erste Nummer von OCR zerstoert -> ehrlich nicht rettbar aus Text.
        Assert.Null(Match("993^-<j-^.993160"));
    }
}
