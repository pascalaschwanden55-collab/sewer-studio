using System.Xml.Linq;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Wartbarkeitsaudit 30.09.2026, Z10: Jede globale Taste aus <c>MainWindow.xaml</c> steht auch in der
/// Liste des Tastenkürzel-Fensters. Ohne diesen Abgleich fällt ein neues <c>KeyBinding</c> dort
/// nicht auf, und die Übersicht veraltet unbemerkt.
/// </summary>
public sealed class MainWindowTastenkuerzelTests
{
    [Fact]
    public void Jede_Taste_aus_MainWindow_steht_im_Tastenkuerzel_Fenster()
    {
        var angezeigt = TastenkuerzelWindow.BauGruppen()
            .SelectMany(g => g.Kuerzel)
            .Select(k => k.Taste)
            .ToHashSet(StringComparer.Ordinal);

        var fehlend = LeseKeyBindings()
            .Where(taste => !angezeigt.Contains(taste))
            .ToList();

        Assert.True(
            fehlend.Count == 0,
            "Diese Tasten aus MainWindow.xaml fehlen in TastenkuerzelWindow.BauGruppen(): " +
            string.Join(", ", fehlend));
    }

    [Fact]
    public void MainWindow_hat_KeyBindings_und_die_Abbildung_liefert_bekannte_Schreibweisen()
    {
        var tasten = LeseKeyBindings();

        Assert.NotEmpty(tasten);
        Assert.Contains("Strg+S", tasten);
        Assert.Contains("Strg+Umschalt+Z", tasten);
    }

    // Die einzige Abbildung Key/Modifiers -> Anzeigetext (Control -> Strg, Shift -> Umschalt).
    private static List<string> LeseKeyBindings()
    {
        var pfad = TestRepoPaths.RepoFile("src", "AuswertungPro.Next.UI", "MainWindow.xaml");
        return XDocument.Load(pfad)
            .Descendants()
            .Where(e => e.Name.LocalName == "KeyBinding")
            .Select(e => AnzeigeText(
                (string?)e.Attribute("Key") ?? string.Empty,
                (string?)e.Attribute("Modifiers") ?? string.Empty))
            .ToList();
    }

    private static string AnzeigeText(string key, string modifiers)
    {
        var teile = modifiers.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(m => m switch
            {
                "Control" or "Ctrl" => "Strg",
                "Shift" => "Umschalt",
                "Alt" => "Alt",
                "Windows" => "Win",
                _ => m,
            })
            .OrderBy(m => Array.IndexOf(new[] { "Strg", "Alt", "Umschalt", "Win" }, m))
            .ToList();
        teile.Add(key);
        return string.Join("+", teile);
    }
}
