using System.IO;
using System.Text.RegularExpressions;
using AuswertungPro.Next.UI.Services;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Optikanalyse 28.09.2026, Aufgabe 6 («Hilfe-Menü, F1, Tastenkürzel, Handbuch»): Wächter, dass
/// JEDE Seite der Leiste (<c>ShellViewModel.NavItems</c>) einen Handbuchabschnitt hat.
/// Die 15 Seitenschlüssel werden nicht hier von Hand nachgebildet, sondern per Regex direkt aus
/// dem Quelltext von <c>ShellViewModel.cs</c> gelesen (dieselbe <c>new("\uXXXX", "Titel", ...)</c>-
/// Zeile, die auch die echte Leiste befüllt) - eine WPF-App/ServiceProvider braucht dieser Test
/// dafür nicht, weil die Titel reine String-Literale im Quelltext sind.
///
/// Hinweis: Task-6-Brief nennt «alle 16 aktuellen Seiten» und zählt 15 Namen auf; der Code
/// (<c>ShellViewModel.NavItems</c>) hat tatsächlich genau 15 Einträge (bewusst geprüft, kein
/// 16. Eintrag vergessen) - dieser Wächter folgt dem Code, nicht der Brief-Zahl.
/// </summary>
public sealed class HandbuchInhaltAbdeckungTests
{
    private static readonly Regex NavItemMuster = new(
        "new\\(\"\\\\u[0-9A-Fa-f]{4}\",\\s*\"([^\"]+)\"",
        RegexOptions.Compiled);

    [Fact]
    public void Jede_Seite_der_Leiste_hat_einen_Handbuchabschnitt()
    {
        var quelle = File.ReadAllText(RepoFile(
            "src", "AuswertungPro.Next.UI", "ViewModels", "ShellViewModel.cs"));

        var navBlockStart = quelle.IndexOf("NavItems = new List<NavItem>", System.StringComparison.Ordinal);
        Assert.True(navBlockStart >= 0, "NavItems-Liste wurde in ShellViewModel.cs nicht gefunden - Wächter kann nicht prüfen.");
        var navBlockEnde = quelle.IndexOf("\n        };", navBlockStart, System.StringComparison.Ordinal);
        Assert.True(navBlockEnde > navBlockStart, "Ende der NavItems-Liste wurde nicht gefunden.");
        var block = quelle[navBlockStart..navBlockEnde];

        var seitenSchluessel = NavItemMuster.Matches(block)
            .Select(m => m.Groups[1].Value)
            .ToList();

        Assert.True(seitenSchluessel.Count >= 10, "Es wurden zu wenige NavItem-Titel gefunden - Regex/Muster prüfen.");

        var fehlend = seitenSchluessel
            .Where(schluessel => HandbuchInhalt.Abschnitte.All(a => a.Schluessel != schluessel))
            .ToList();

        Assert.True(
            fehlend.Count == 0,
            "Seiten der Leiste ohne Handbuchabschnitt (HandbuchInhalt.Abschnitte ergänzen): "
            + string.Join(", ", fehlend));
    }

    [Fact]
    public void HandbuchInhalt_Abschnitte_haben_je_einen_nichtleeren_Titel_und_Text()
    {
        foreach (var abschnitt in HandbuchInhalt.Abschnitte)
        {
            Assert.False(string.IsNullOrWhiteSpace(abschnitt.Schluessel), "Ein Abschnitt hat keinen Schlüssel.");
            Assert.False(string.IsNullOrWhiteSpace(abschnitt.Titel), $"Abschnitt '{abschnitt.Schluessel}' hat keinen Titel.");
            Assert.False(string.IsNullOrWhiteSpace(abschnitt.Text), $"Abschnitt '{abschnitt.Schluessel}' hat keinen Text.");
        }
    }

    [Fact]
    public void Genau_ein_Abschnitt_ist_der_Fachleute_Abschnitt_und_steht_am_Ende()
    {
        var fachliche = HandbuchInhalt.Abschnitte.Where(a => a.IstFachlich).ToList();
        Assert.Single(fachliche);
        Assert.Equal(HandbuchInhalt.FachleuteSchluessel, fachliche[0].Schluessel);
        Assert.Same(HandbuchInhalt.Abschnitte[^1], fachliche[0]);
    }

    [Fact]
    public void Finde_faellt_bei_unbekanntem_Schluessel_auf_den_ersten_Abschnitt_zurueck()
    {
        var ergebnis = HandbuchInhalt.Finde("Gibt es nicht");
        Assert.Same(HandbuchInhalt.Abschnitte[0], ergebnis);

        Assert.Same(HandbuchInhalt.Abschnitte[0], HandbuchInhalt.Finde(null));
    }
}
