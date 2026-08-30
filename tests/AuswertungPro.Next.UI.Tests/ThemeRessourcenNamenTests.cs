using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Ein Verweis auf eine nicht vorhandene Theme-Ressource faellt beim Bauen
/// nicht auf: WPF loest ihn zur Laufzeit einfach nicht auf, die Eigenschaft
/// bleibt ungesetzt. Das Ergebnis ist ein Fenster mit falschen Farben —
/// im schlimmsten Fall dunkler Text auf dunklem Grund.
///
/// Genau das ist am 2026-08-30 passiert: "SurfaceBrush" und "SurfaceAltBrush"
/// gibt es nicht, das Vorschlagsfenster war unlesbar. Der Name war von
/// bestehenden Fenstern abgeschaut, die denselben Fehler tragen.
/// </summary>
public sealed class ThemeRessourcenNamenTests
{
    /// <summary>
    /// Bestand am 2026-08-30. Diese Liste darf schrumpfen, niemals wachsen.
    /// Jeder Eintrag ist ein Fenster mit mindestens einer Farbe, die es nicht
    /// gibt — dort stimmt die Darstellung nicht.
    /// </summary>
    private static readonly HashSet<string> BekannterBestand = new(StringComparer.OrdinalIgnoreCase)
    {
        // "SurfaceBrush" gibt es nicht — diese Flaechen bleiben ungesetzt:
        "DossierPageSelectionWindow.xaml",
        "DossierParcelLookupWindow.xaml",
        "DossierPlanWindow.xaml",
        "DossierPreviewWindow.xaml",
        "ExportPage.xaml",

        // "TextPrimaryBrush" gibt es nicht — betrifft nur die Schriftfarbe:
        "BendSuggestionPreviewWindow.xaml",
        "TrainingStudioWindow.xaml",

        // Styles aus anderen Woerterbuechern, keine Farben:
        "Controls.xaml",
        "PlayerCodingSidePanel.xaml",
    };

    [Fact]
    public void Kein_neues_Fenster_verweist_auf_eine_erfundene_Theme_Ressource()
    {
        var definiert = LiesDefinierteRessourcen();
        Assert.True(definiert.Count > 100, $"Nur {definiert.Count} Ressourcen gefunden — Theme nicht gelesen?");

        var treffer = new List<string>();

        foreach (var datei in AlleXamlDateien())
        {
            var name = Path.GetFileName(datei);
            if (BekannterBestand.Contains(name))
                continue;

            var text = File.ReadAllText(datei);
            var lokal = SchluesselIn(text);

            foreach (var verweis in VerweiseIn(text))
            {
                if (!definiert.Contains(verweis) && !lokal.Contains(verweis))
                    treffer.Add($"{name}: {verweis}");
            }
        }

        Assert.True(
            treffer.Count == 0,
            "Diese Verweise zeigen ins Leere — WPF laesst die Eigenschaft dann ungesetzt:"
            + Environment.NewLine + string.Join(Environment.NewLine, treffer.Distinct()));
    }

    private static HashSet<string> LiesDefinierteRessourcen()
    {
        var ergebnis = new HashSet<string>(StringComparer.Ordinal);

        foreach (var datei in AlleXamlDateien())
        {
            // Ressourcen-Woerterbuecher gelten programmweit; einzelne Fenster
            // bringen ihre eigenen mit, die nur dort zaehlen.
            if (!datei.Contains($"{Path.DirectorySeparatorChar}Theme{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                && !datei.EndsWith("App.xaml", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var schluessel in SchluesselIn(File.ReadAllText(datei)))
                ergebnis.Add(schluessel);
        }

        return ergebnis;
    }

    private static IEnumerable<string> AlleXamlDateien()
    {
        var wurzel = RepoFile("src", "AuswertungPro.Next.UI");

        return Directory.EnumerateFiles(wurzel, "*.xaml", SearchOption.AllDirectories)
            .Where(d => !d.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                        && !d.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase));
    }

    private static HashSet<string> SchluesselIn(string xaml)
        => Regex.Matches(xaml, @"x:Key=""([A-Za-z0-9_]+)""")
            .Select(m => m.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

    private static IEnumerable<string> VerweiseIn(string xaml)
        => Regex.Matches(xaml, @"DynamicResource ([A-Za-z0-9_]+)\s*\}")
            .Select(m => m.Groups[1].Value)
            .Distinct(StringComparer.Ordinal);
}
