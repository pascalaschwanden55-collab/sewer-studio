using System.IO;
using System.Text.RegularExpressions;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Harmonisierung der Fenster (Design-Audit 2026-09-03, Paket nach M1): gleiche Rundungen,
/// gleiche Fenstertitel, gleiche Mindestgroessen. Die Theme-Dateien definieren die Stile und
/// duerfen Zahlen tragen; der Startbildschirm hat seine eigene Choreografie.
/// </summary>
public sealed class DesignAuditFensterUndRundungenTests
{
    private static readonly string UiRoot = RepoFile("src", "AuswertungPro.Next.UI");

    [Fact]
    public void Rundungen_kommen_aus_den_Radius_Tokens()
    {
        var controls = File.ReadAllText(Path.Combine(UiRoot, "Theme", "Controls.xaml"));
        foreach (var (key, wert) in new[] { ("RadiusS", "4"), ("RadiusM", "6"), ("RadiusL", "8"), ("RadiusXL", "10"), ("RadiusXXL", "14"), ("RadiusPill", "999") })
            Assert.Contains($"<CornerRadius x:Key=\"{key}\">{wert}</CornerRadius>", controls);

        // Eine einzelne Zahl ist eine Stufe der Skala; vierteilige Werte (z. B. 8,8,0,0) sind bewusst
        // halbe Rundungen an zusammengesetzten Kanten und bleiben erlaubt. "0" ist keine Rundung.
        var festeRundung = new Regex("CornerRadius=\"(?!0\")[0-9.]+\"", RegexOptions.Compiled);
        var treffer = SucheInXaml(festeRundung, datei => !IstTheme(datei) && Path.GetFileName(datei) != "StartupSplashWindow.xaml");
        Assert.True(treffer.Count == 0, "Feste Rundungen ausserhalb des Themes — bitte {DynamicResource RadiusS|M|L|XL|XXL|Pill}:\n" + string.Join("\n", treffer));
    }

    [Fact]
    public void Jedes_Fenster_heisst_SewerStudio_Gedankenstrich_Aufgabe()
    {
        // Gebundene Titel (z. B. {Binding WindowTitle}) setzt das ViewModel; das Hauptfenster bindet
        // den Projektnamen, der Splash zeigt nur die Wortmarke.
        var treffer = new List<string>();
        foreach (var (datei, wurzel) in AlleFensterWurzeln())
        {
            var name = Path.GetFileName(datei);
            if (name is "MainWindow.xaml" or "StartupSplashWindow.xaml")
                continue;

            var title = Regex.Match(wurzel, "Title=\"([^\"]*)\"");
            if (!title.Success)
            {
                treffer.Add($"{Relativ(datei)}: kein Title");
                continue;
            }

            var wert = title.Groups[1].Value;
            if (wert.StartsWith('{'))
                continue;
            if (!wert.StartsWith("SewerStudio — ", StringComparison.Ordinal) || wert.Length <= "SewerStudio — ".Length)
                treffer.Add($"{Relativ(datei)}: Title=\"{wert}\"");
        }

        Assert.True(treffer.Count == 0, "Fenstertitel ohne das gemeinsame Muster \"SewerStudio — <Aufgabe>\":\n" + string.Join("\n", treffer));
    }

    [Fact]
    public void Jedes_veraenderbare_Fenster_hat_eine_Mindestgroesse()
    {
        // Fenster, die sich an ihren Inhalt anpassen oder nicht veraendert werden koennen, brauchen keine.
        var treffer = new List<string>();
        foreach (var (datei, wurzel) in AlleFensterWurzeln())
        {
            if (Path.GetFileName(datei) == "StartupSplashWindow.xaml")
                continue;
            if (wurzel.Contains("SizeToContent=", StringComparison.Ordinal) || wurzel.Contains("ResizeMode=\"NoResize\"", StringComparison.Ordinal))
                continue;

            var hatBreite = Regex.IsMatch(wurzel, "MinWidth=\"[0-9]+\"");
            var hatHoehe = Regex.IsMatch(wurzel, "MinHeight=\"[0-9]+\"");
            if (!hatBreite || !hatHoehe)
                treffer.Add(Relativ(datei));
        }

        Assert.True(treffer.Count == 0, "Veraenderbare Fenster ohne MinWidth/MinHeight (sonst lassen sie sich bis zur Unlesbarkeit zusammenschieben):\n" + string.Join("\n", treffer));
    }

    private static IEnumerable<(string Datei, string Wurzel)> AlleFensterWurzeln()
    {
        foreach (var datei in Directory.EnumerateFiles(UiRoot, "*.xaml", SearchOption.AllDirectories))
        {
            if (IstBuildAusgabe(datei))
                continue;

            var xaml = File.ReadAllText(datei);
            var m = Regex.Match(xaml, "<Window\\b(.*?)>", RegexOptions.Singleline);
            if (m.Success)
                yield return (datei, m.Groups[1].Value);
        }
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
                    treffer.Add($"{Relativ(datei)}:{i + 1}: {m.Value}");
            }
        }

        return treffer;
    }

    private static bool IstTheme(string datei)
        => datei.Contains($"{Path.DirectorySeparatorChar}Theme{Path.DirectorySeparatorChar}", StringComparison.Ordinal);

    private static bool IstBuildAusgabe(string pfad)
        => pfad.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
        || pfad.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);

    private static string Relativ(string pfad) => Path.GetRelativePath(UiRoot, pfad);
}
