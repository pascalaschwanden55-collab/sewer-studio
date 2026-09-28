using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Optikanalyse 28.09.2026, Aufgabe 7 («Weitere Aktionen» gliedern, Doppelungen und Namen
/// bereinigen): Haltungen (<c>DataPage.xaml</c>) und Schächte (<c>SchaechtePage.xaml</c>)
/// gliedern «Weitere Aktionen» in Untermenüs mit derselben Bezeichnung aus einer festen Liste
/// («Daten abgleichen», «Bearbeiten», «Reihenfolge», «Ansicht», «Ausgabe»). Nicht jede Seite
/// braucht jede Gruppe — die Schachtseite hat keine eigenständige «Bearbeiten»- oder
/// «Ausgabe»-Aktion, die nicht schon im Zeilenmenü steht (<see cref="DesignAuditNovaSchaechteTests"/>) —
/// aber wo ein Titel vorkommt, muss er wortgleich aus dieser Liste stammen, nie eine
/// abweichende Bezeichnung wie «Sortierung» statt «Reihenfolge».
/// </summary>
public sealed class WeitereAktionenUntermenueTests
{
    private static readonly string[] BekannteTitel =
    {
        "Daten abgleichen", "Bearbeiten", "Reihenfolge", "Ansicht", "Ausgabe"
    };

    // Diese drei Gruppen bieten auf BEIDEN Seiten echte Aktionen (Datenabgleich, Zeile
    // verschieben, Ansicht wechseln/anpassen) und müssen deshalb auf beiden Seiten vorkommen.
    private static readonly string[] GemeinsamePflichtTitel =
    {
        "Daten abgleichen", "Reihenfolge", "Ansicht"
    };

    [Fact]
    public void Jeder_verwendete_Untermenue_Titel_stammt_aus_der_gemeinsamen_Liste()
    {
        var haltungenTitel = LiesUntermenueTitel("DataPage.xaml");
        var schaechteTitel = LiesUntermenueTitel("SchaechtePage.xaml");

        foreach (var titel in haltungenTitel.Concat(schaechteTitel))
            Assert.Contains(titel, BekannteTitel);
    }

    [Fact]
    public void Beide_Seiten_bieten_dieselben_gemeinsamen_Untermenues_an()
    {
        var haltungenTitel = LiesUntermenueTitel("DataPage.xaml");
        var schaechteTitel = LiesUntermenueTitel("SchaechtePage.xaml");

        foreach (var titel in GemeinsamePflichtTitel)
        {
            Assert.Contains(titel, haltungenTitel);
            Assert.Contains(titel, schaechteTitel);
        }
    }

    /// <summary>Haltungen haben zusätzlich fachliche Bearbeiten- und Ausgabe-Aktionen.</summary>
    [Fact]
    public void Haltungen_haben_zusaetzlich_Bearbeiten_und_Ausgabe()
    {
        var haltungenTitel = LiesUntermenueTitel("DataPage.xaml");
        Assert.Contains("Bearbeiten", haltungenTitel);
        Assert.Contains("Ausgabe", haltungenTitel);
    }

    /// <summary>
    /// «Beide Seiten gleiche Namen (Nach oben/Nach unten)»: das Reihenfolge-Untermenü nennt auf
    /// beiden Seiten dieselben beiden Verschiebe-Aktionen, nie die alten Kurzformen «Hoch»/«Runter».
    /// </summary>
    [Fact]
    public void Reihenfolge_Untermenue_nennt_auf_beiden_Seiten_Nach_oben_und_Nach_unten()
    {
        foreach (var datei in new[] { "DataPage.xaml", "SchaechtePage.xaml" })
        {
            var reihenfolge = LiesUntermenueInhalt(datei, "Reihenfolge");
            Assert.Contains("Header=\"Nach oben\"", reihenfolge);
            Assert.Contains("Header=\"Nach unten\"", reihenfolge);
            Assert.DoesNotContain("Header=\"Hoch\"", reihenfolge);
            Assert.DoesNotContain("Header=\"Runter\"", reihenfolge);
        }
    }

    private static IReadOnlyList<string> LiesUntermenueTitel(string dateiname)
    {
        var contextMenu = WeitereAktionenContextMenu(dateiname);
        return contextMenu.Elements(contextMenu.Name.Namespace + "MenuItem")
            .Select(mi => (string?)mi.Attribute("Header"))
            .Where(header => !string.IsNullOrEmpty(header))
            .Cast<string>()
            .ToList();
    }

    private static string LiesUntermenueInhalt(string dateiname, string titel)
    {
        var contextMenu = WeitereAktionenContextMenu(dateiname);
        var ns = contextMenu.Name.Namespace;
        var gruppe = contextMenu.Elements(ns + "MenuItem")
            .FirstOrDefault(mi => (string?)mi.Attribute("Header") == titel);
        Assert.True(gruppe is not null, $"{dateiname}: Untermenü \"{titel}\" nicht gefunden.");
        return gruppe!.ToString();
    }

    /// <summary>Das <c>ContextMenu</c> des Knopfs <c>WeitereAktionenDropdown</c>, direkt aus der XAML gelesen.</summary>
    private static XElement WeitereAktionenContextMenu(string dateiname)
    {
        var pfad = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Pages", dateiname);
        var doc = XDocument.Parse(File.ReadAllText(pfad));
        var ns = doc.Root!.Name.Namespace;
        var xNs = (XNamespace)"http://schemas.microsoft.com/winfx/2006/xaml";

        var dropdown = doc.Descendants(ns + "Button")
            .FirstOrDefault(button => (string?)button.Attribute(xNs + "Name") == "WeitereAktionenDropdown");
        Assert.True(dropdown is not null, $"{dateiname}: Button \"WeitereAktionenDropdown\" nicht gefunden.");

        var contextMenuProperty = dropdown!.Element(ns + "Button.ContextMenu");
        Assert.True(contextMenuProperty is not null, $"{dateiname}: Button.ContextMenu nicht gefunden.");

        var contextMenu = contextMenuProperty!.Element(ns + "ContextMenu");
        Assert.True(contextMenu is not null, $"{dateiname}: ContextMenu nicht gefunden.");
        return contextMenu!;
    }
}
