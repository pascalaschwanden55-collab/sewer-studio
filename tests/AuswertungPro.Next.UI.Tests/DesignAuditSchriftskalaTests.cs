using System.IO;
using System.Text.RegularExpressions;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Schriftskala aus dem Design-Audit 2026-09-03 (M1): Eine Skala mit sieben Stufen, kleinste
/// Stufe 11 px (Entscheid Pascal 2026-09-03). Sichtbare Oberflaechen lesen ihre Groesse nur
/// noch ueber die Tokens; die Theme-Dateien selbst duerfen Zahlen tragen, aber keine unter 11.
/// </summary>
public sealed class DesignAuditSchriftskalaTests
{
    private static readonly string UiRoot = RepoFile("src", "AuswertungPro.Next.UI");

    // Stufe -> Pixel. TextXS ist die kleinste erlaubte Groesse im ganzen Programm.
    private static readonly (string Key, string Wert)[] Skala =
    [
        ("TextXS", "11"), ("TextS", "12"), ("TextM", "13"), ("TextL", "15"),
        ("TextXL", "18"), ("TextTitle", "22"), ("TextDisplay", "28")
    ];

    [Fact]
    public void Die_Schriftskala_liegt_als_Tokens_in_Controls_xaml()
    {
        var controls = File.ReadAllText(Path.Combine(UiRoot, "Theme", "Controls.xaml"));
        foreach (var (key, wert) in Skala)
            Assert.Contains($"<sys:Double x:Key=\"{key}\">{wert}</sys:Double>", controls);
    }

    [Fact]
    public void Keine_Schrift_unter_11_Pixel_in_XAML()
    {
        var zuKlein = new Regex("FontSize=\"(?:[0-9]|10)(?:\\.[0-9]+)?\"", RegexOptions.Compiled);
        var treffer = SucheInXaml(zuKlein, _ => true);
        Assert.True(treffer.Count == 0, "Schrift unter 11 px (Entscheid 2026-09-03: TextXS = 11 ist die Untergrenze):\n" + string.Join("\n", treffer));
    }

    [Fact]
    public void Oberflaechen_lesen_Schriftgroessen_nur_ueber_die_Skala()
    {
        // Theme-Dateien definieren die Stile (Zahlen erlaubt). Der Startbildschirm hat eine eigene
        // Choreografie mit 76-px-Wortmarke und bleibt wie bei Farben und Eintritt aussen vor.
        var literal = new Regex("FontSize=\"[0-9]", RegexOptions.Compiled);
        var treffer = SucheInXaml(literal, datei =>
            !datei.Contains($"{Path.DirectorySeparatorChar}Theme{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            && Path.GetFileName(datei) != "StartupSplashWindow.xaml");
        Assert.True(
            treffer.Count == 0,
            "Feste Schriftgroessen ausserhalb des Themes — bitte {DynamicResource TextXS|TextS|TextM|TextL|TextXL|TextTitle|TextDisplay}:\n"
            + string.Join("\n", treffer));
    }

    [Fact]
    public void Im_Code_erzeugte_Oberflaechentexte_sind_nicht_kleiner_als_11_Pixel()
    {
        // Gezeichnete Beschriftungen AUF Video, Grafik und PDF-Nachbildung duerfen kleiner sein:
        // Sie skalieren mit dem Bild, nicht mit der Oberflaeche.
        string[] grafik =
        [
            "SamMaskRenderer.cs", "PipeGraphTimeline.xaml.cs", "CodingRulerOverlayRenderer.cs",
            "DamageMarkerController.cs", "RohrquerschnittControl.cs", "PipelinePipeRadarRenderer.cs",
            "DossierPreviewPageRenderer.cs", "DossierExactPreviewPageRenderer.cs"
        ];
        var zuKlein = new Regex("FontSize = (?:[0-9]|10)(?:d|\\.0)?[,;) ]", RegexOptions.Compiled);
        var treffer = new List<string>();

        foreach (var datei in Directory.EnumerateFiles(UiRoot, "*.cs", SearchOption.AllDirectories))
        {
            if (IstBuildAusgabe(datei) || grafik.Contains(Path.GetFileName(datei)))
                continue;

            var zeilen = File.ReadAllLines(datei);
            for (var i = 0; i < zeilen.Length; i++)
            {
                if (zuKlein.IsMatch(zeilen[i]))
                    treffer.Add($"{Path.GetRelativePath(UiRoot, datei)}:{i + 1}: {zeilen[i].Trim()}");
            }
        }

        Assert.True(treffer.Count == 0, "Oberflaechentext unter 11 px im Code:\n" + string.Join("\n", treffer));
    }

    private static List<string> SucheInXaml(Regex muster, Func<string, bool> dateiFilter)
    {
        var treffer = new List<string>();
        foreach (var datei in Directory.EnumerateFiles(UiRoot, "*.xaml", SearchOption.AllDirectories))
        {
            if (IstBuildAusgabe(datei) || !dateiFilter(datei))
                continue;

            var zeilen = File.ReadAllLines(datei);
            for (var i = 0; i < zeilen.Length; i++)
            {
                foreach (Match m in muster.Matches(zeilen[i]))
                    treffer.Add($"{Path.GetRelativePath(UiRoot, datei)}:{i + 1}: {m.Value}");
            }
        }

        return treffer;
    }

    private static bool IstBuildAusgabe(string pfad)
        => pfad.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
        || pfad.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);
}
