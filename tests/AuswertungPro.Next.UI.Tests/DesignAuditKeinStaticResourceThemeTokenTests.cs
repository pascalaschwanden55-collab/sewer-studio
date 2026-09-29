using System.IO;
using System.Text.RegularExpressions;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Waechter zur Optikanalyse 28.09.2026, Schlusswelle (Item 1): ausserhalb von
/// <c>src/AuswertungPro.Next.UI/Theme/</c> darf kein XAML-Attribut ein Theme-Token (Brush oder
/// Color) mehr als <c>{StaticResource ...}</c> ansprechen - nur <c>{DynamicResource ...}</c>
/// zieht einen Themewechsel (Hell/Dunkel, Hochkontrast) nach. Anlass war
/// <c>MeasureTemplateEditorWindow.xaml</c>: Das Leerzustands-Wasserzeichen der Suchbox blieb bei
/// einem Theme-Wechsel auf der urspruenglichen Farbe stehen.
///
/// Geprueft wird NUR der direkte, alleinige Attributwert
/// (<c>Foreground="{StaticResource MutedBrush}"</c>), NICHT ein <c>StaticResource</c>, das als
/// <c>Converter=</c> innerhalb einer Bindung steckt (z. B.
/// <c>Background="{Binding X, Converter={StaticResource ZkBrushConv}}"</c>) - ein Konverter ist
/// kein Theme-Token und muss nicht dynamisch sein.
///
/// Ausnahmen sind namentlich und mit Grund zu belegen (regelgranular wie bei
/// <see cref="DesignAuditKnopfleistenTests"/>): bisher KEINE, weil das einzige real gefundene
/// Beispiel (VisualBrush.Visual-Wasserzeichen) durch <see cref="MeasureTemplateEditorWindowWasserzeichenIsolatedSmokeTests"/>
/// nachweislich mit DynamicResource funktioniert.
/// </summary>
public sealed class DesignAuditKeinStaticResourceThemeTokenTests
{
    private static readonly string UiRoot = RepoFile("src", "AuswertungPro.Next.UI");
    private static readonly string ThemeOrdner = Path.Combine(UiRoot, "Theme") + Path.DirectorySeparatorChar;

    /// <summary>
    /// Direkter Attributwert <c>="{StaticResource XyzBrush}"</c> / <c>="{StaticResource ColorXyz}"</c> -
    /// NICHT verschachtelt in <c>Converter=</c> oder einer <c>Binding</c>, weil dort kein
    /// theme-abhaengiges Token, sondern ein Konverter-/Wert-Objekt gemeint ist.
    /// </summary>
    private static readonly Regex ThemeTokenMuster =
        new(@"=""\{StaticResource ([A-Za-z0-9_]*Brush|Color[A-Za-z0-9_]*)\}""", RegexOptions.Compiled);

    /// <summary>
    /// Namentliche, regelgranulare Ausnahmen. Aktuell leer: Der einzige real gefundene Fall
    /// (MeasureTemplateEditorWindow.xaml Wasserzeichen) ist auf DynamicResource umgestellt und
    /// nachweislich funktionsfaehig (siehe Klassenkommentar).
    /// </summary>
    private static readonly (string Datei, string Grund)[] Ausnahmen = [];

    private static IEnumerable<string> AlleGepruefteXamlDateien()
    {
        foreach (var pfad in Directory.GetFiles(UiRoot, "*.xaml", SearchOption.AllDirectories)
                     .OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            if (pfad.StartsWith(ThemeOrdner, StringComparison.OrdinalIgnoreCase))
                continue;
            if (Ausnahmen.Any(a => string.Equals(a.Datei, Path.GetFileName(pfad), StringComparison.OrdinalIgnoreCase)))
                continue;
            yield return pfad;
        }
    }

    [Fact]
    public void Ausnahmeliste_verweist_nur_auf_tatsaechlich_vorhandene_Dateien()
    {
        var alle = Directory.GetFiles(UiRoot, "*.xaml", SearchOption.AllDirectories)
            .Select(Path.GetFileName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var fehlend = Ausnahmen.Select(a => a.Datei).Where(d => !alle.Contains(d)).ToList();
        Assert.True(fehlend.Count == 0, "Ausnahmeliste nennt nicht (mehr) vorhandene Dateien: " + string.Join(", ", fehlend));
    }

    [Fact]
    public void Mindestens_hundert_XAML_Dateien_werden_tatsaechlich_geprueft()
    {
        // Schuetzt davor, dass ein Pfadfehler den Waechter leerlaufen laesst.
        var anzahl = AlleGepruefteXamlDateien().Count();
        Assert.True(anzahl >= 100, $"Nur {anzahl} XAML-Dateien im Pruefumfang - Pfad pruefen.");
    }

    [Fact]
    public void Kein_direktes_StaticResource_Theme_Token_ausserhalb_von_Theme()
    {
        var verstoesse = new List<string>();
        foreach (var pfad in AlleGepruefteXamlDateien())
        {
            var xaml = File.ReadAllText(pfad);
            foreach (Match match in ThemeTokenMuster.Matches(xaml))
            {
                verstoesse.Add($"{Path.GetFileName(pfad)}: {match.Value}");
            }
        }

        Assert.True(verstoesse.Count == 0,
            "StaticResource-Theme-Token ausserhalb von Theme/ gefunden (sollten DynamicResource sein):\n"
            + string.Join("\n", verstoesse));
    }

    [Fact]
    public void MeasureTemplateEditorWindow_Wasserzeichen_verwendet_DynamicResource()
    {
        var xaml = File.ReadAllText(Path.Combine(UiRoot, "Views", "Windows", "MeasureTemplateEditorWindow.xaml"));
        Assert.Contains("Foreground=\"{DynamicResource MutedBrush}\"", xaml);
        Assert.DoesNotContain("Foreground=\"{StaticResource MutedBrush}\"", xaml);
    }
}
