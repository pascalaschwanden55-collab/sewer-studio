using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Die EINE Auflistung aller XAML-Dateien des UI-Projekts fuer Quelltext-Waechter.
/// Build-Ausgaben (bin/obj) zaehlen nie mit; alle Pfade sind absolut und stammen von
/// <see cref="UiRoot"/>, damit Vergleiche mit <c>RepoFile("src", "AuswertungPro.Next.UI", ...)</c> stimmen.
/// </summary>
internal static class TestXaml
{
    /// <summary>Wurzel des UI-Projekts (<c>src/AuswertungPro.Next.UI</c>).</summary>
    public static string UiRoot => TestRepoPaths.RepoFile("src", "AuswertungPro.Next.UI");

    /// <summary>Alle XAML-Dateien, Reihenfolge des Dateisystems (rekursiv, ohne bin/obj).</summary>
    public static IEnumerable<string> Alle()
        => Directory.EnumerateFiles(UiRoot, "*.xaml", SearchOption.AllDirectories)
            .Where(pfad => !IstBuildAusgabe(pfad));

    /// <summary>
    /// Wie <see cref="Alle()"/>, aber ohne die genannten Relativpfade (relativ zu <see cref="UiRoot"/>,
    /// '/' oder '\' beliebig). Ein Eintrag mit abschliessendem Trennzeichen (<c>"Theme/"</c>) nimmt den
    /// ganzen Ordner aus, jeder andere genau diese Datei.
    /// </summary>
    public static IEnumerable<string> Alle(params string[] ausgenommeneRelativpfade)
    {
        var ausgenommen = ausgenommeneRelativpfade
            .Select(p => p.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar))
            .ToArray();

        return Alle().Where(pfad =>
        {
            var relativ = Relativ(pfad);
            return !ausgenommen.Any(a => a.EndsWith(Path.DirectorySeparatorChar)
                ? relativ.StartsWith(a, StringComparison.OrdinalIgnoreCase)
                : string.Equals(relativ, a, StringComparison.OrdinalIgnoreCase));
        });
    }

    /// <summary>Alle XAML-Dateien, fuer die <paramref name="behalte"/> (Absolutpfad) wahr ist.</summary>
    public static IEnumerable<string> Alle(Func<string, bool> behalte)
        => Alle().Where(behalte);

    /// <summary>
    /// Sucht <paramref name="muster"/> zeilenweise in allen XAML-Dateien, fuer die <paramref name="dateiFilter"/>
    /// (Absolutpfad) wahr ist. Treffer: <c>Relativpfad:Zeile: Treffertext</c>.
    /// </summary>
    public static List<string> SucheZeilenweise(Regex muster, Func<string, bool> dateiFilter)
    {
        var treffer = new List<string>();
        foreach (var datei in Alle(dateiFilter))
        {
            var zeilen = File.ReadAllLines(datei);
            for (var i = 0; i < zeilen.Length; i++)
            {
                foreach (Match m in muster.Matches(zeilen[i]))
                    treffer.Add($"{Relativ(datei)}:{i + 1}: {m.Value}");
            }
        }

        return treffer;
    }

    /// <summary>Pfad relativ zu <see cref="UiRoot"/>.</summary>
    public static string Relativ(string pfad) => Path.GetRelativePath(UiRoot, pfad);

    /// <summary>Liegt die Datei in einem bin- oder obj-Ordner (Kopien alter Staende)?</summary>
    public static bool IstBuildAusgabe(string pfad)
        => pfad.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
        || pfad.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);
}
