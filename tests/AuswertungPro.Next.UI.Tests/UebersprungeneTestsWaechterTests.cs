using System.IO;
using System.Text.RegularExpressions;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Waechter ueber alle bewusst uebersprungenen Tests (Gesamtaudit 2026-08-14, Prio 2).
///
/// Ein uebersprungener Test sieht im Ergebnis fast wie ein bestandener aus. Damit nicht
/// unbemerkt weitere dazukommen, sind die zulaessigen Gruende hier namentlich
/// aufgefuehrt. Ein neuer Skip macht diesen Test rot — dann ist bewusst zu entscheiden,
/// ob er wirklich sein muss.
/// </summary>
public sealed class UebersprungeneTestsWaechterTests
{
    /// <summary>
    /// Bekannte, bewusst uebersprungene Faelle: Datei -> Grund.
    /// Alle sechs haengen an einer Umgebung, die es in der CI nicht gibt
    /// (Entwicklermodus, Kundenbestand, ffmpeg, GPU, WPF-Kindprozess).
    /// </summary>
    private static readonly (string Datei, string Grundfragment)[] Bekannt =
    {
        (Path.Combine("AuswertungPro.Next.Infrastructure.Tests", "Backup", "JunctionFactAttribute.cs"),
            "JunctionTestSupport.UnavailableReason"),
        (Path.Combine("AuswertungPro.Next.Infrastructure.Tests", "Import", "XtfVideoCounterLiveAcceptanceTests.cs"),
            "Kundenbestand nicht vorhanden"),
        (Path.Combine("AuswertungPro.Next.Infrastructure.Tests", "VsaKekCatalogBuilderTests.cs"),
            "VSA-KEK-Export-Fixture"),
        (Path.Combine("AuswertungPro.Next.Pipeline.Tests", "FfmpegFactAttribute.cs"),
            "ffmpeg nicht auffindbar"),
        (Path.Combine("AuswertungPro.Next.Pipeline.Tests", "SidecarE2eSmokeContractTests.cs"),
            "Maschinengebundener GPU-Test"),
        (Path.Combine("AuswertungPro.Next.UI.Tests", "IsolatedWpfFactAttribute.cs"),
            "isolierten WPF-Kindprozess")
    };

    [Fact]
    public void Es_gibt_keine_unbekannten_uebersprungenen_Tests()
    {
        var gefunden = FindeAlleSkips();

        var unbekannt = gefunden
            .Where(fund => !Bekannt.Any(b =>
                fund.Datei.EndsWith(b.Datei, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        Assert.True(
            unbekannt.Count == 0,
            "Neu uebersprungene Tests gefunden. Bewusst entscheiden und in "
            + $"{nameof(UebersprungeneTestsWaechterTests)} aufnehmen:\n"
            + string.Join("\n", unbekannt.Select(f => $"  {f.Datei}: {f.Zeile}")));
    }

    [Fact]
    public void Jeder_bekannte_Grund_ist_noch_vorhanden_und_unveraendert()
    {
        // Umgekehrte Richtung: Wird ein Skip entfernt oder umformuliert, soll die Liste
        // nicht stillschweigend veralten.
        var gefunden = FindeAlleSkips();

        foreach (var (datei, grundfragment) in Bekannt)
        {
            var passend = gefunden
                .Where(f => f.Datei.EndsWith(datei, StringComparison.OrdinalIgnoreCase))
                .ToList();

            Assert.True(passend.Count > 0, $"Kein Skip mehr in {datei} - Liste bereinigen.");
            Assert.Contains(passend, f => f.Zeile.Contains(grundfragment, StringComparison.Ordinal));
        }
    }

    [Fact]
    public void Die_Zahl_der_uebersprungenen_Stellen_bleibt_bei_sechs()
    {
        // Harte Zahl statt Gefuehl: 4 im .NET-Lauf sichtbare Skips plus zwei
        // Attribut-Faelle, die je nach Umgebung greifen.
        Assert.Equal(Bekannt.Length, FindeAlleSkips().Count);
    }

    private static List<(string Datei, string Zeile)> FindeAlleSkips()
    {
        var wurzel = RepoFile("tests");
        var treffer = new List<(string, string)>();

        foreach (var datei in Directory.EnumerateFiles(wurzel, "*.cs", SearchOption.AllDirectories))
        {
            // Ausgaben und Zwischenstaende nicht mitzaehlen
            if (datei.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || datei.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                continue;
            }

            // Der Waechter selbst zaehlt nicht mit.
            if (Path.GetFileName(datei).Equals(
                    nameof(UebersprungeneTestsWaechterTests) + ".cs",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var zeile in File.ReadLines(datei))
            {
                if (Regex.IsMatch(zeile, @"\bSkip\s*="))
                    treffer.Add((datei, zeile.Trim()));
            }
        }

        return treffer;
    }
}
