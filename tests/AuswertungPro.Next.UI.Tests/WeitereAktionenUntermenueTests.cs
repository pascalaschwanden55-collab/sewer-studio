using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Optikanalyse 28.09.2026, Aufgabe 7 («Weitere Aktionen» gliedern, Doppelungen und Namen
/// bereinigen), Fix-Runde 1 (Controller-Entscheid «Auffindbarkeit schlägt Entdoppelung»):
/// Haltungen (<c>DataPage.xaml</c>) und Schächte (<c>SchaechtePage.xaml</c>) gliedern «Weitere
/// Aktionen» in EXAKT denselben fünf Untermenüs, in derselben Reihenfolge:
/// «Daten abgleichen», «Bearbeiten», «Reihenfolge», «Ansicht», «Ausgabe». Ein Eintrag darf dabei
/// sowohl im Zeilenmenü als auch unter «Weitere Aktionen» stehen (zwei Wege zum selben Handler
/// sind hier bewusst KEINE zu entfernende Doppelung, sondern zusätzliche Auffindbarkeit) —
/// siehe <see cref="DesignAuditNovaSchaechteTests"/> für die entsprechende Prüfung der
/// Zeilenmenü-Gegenstücke.
/// </summary>
public sealed class WeitereAktionenUntermenueTests
{
    private static readonly string[] ErwarteteTitelReihenfolge =
    {
        "Daten abgleichen", "Bearbeiten", "Reihenfolge", "Ansicht", "Ausgabe"
    };

    [Theory]
    [InlineData("DataPage.xaml")]
    [InlineData("SchaechtePage.xaml")]
    public void Weitere_Aktionen_zeigt_exakt_die_fuenf_Untermenues_in_derselben_Reihenfolge(string dateiname)
    {
        var titel = LiesUntermenueTitel(dateiname);
        Assert.Equal(ErwarteteTitelReihenfolge, titel);
    }

    /// <summary>
    /// «Beide Seiten gleiche Namen (Nach oben/Nach unten)»: das Reihenfolge-Untermenü nennt auf
    /// beiden Seiten dieselben beiden Verschiebe-Aktionen, nie die alten Kurzformen «Hoch»/«Runter».
    /// </summary>
    [Theory]
    [InlineData("DataPage.xaml")]
    [InlineData("SchaechtePage.xaml")]
    public void Reihenfolge_Untermenue_nennt_Nach_oben_und_Nach_unten(string dateiname)
    {
        var reihenfolge = LiesUntermenueInhalt(dateiname, "Reihenfolge");
        Assert.Contains("Header=\"Nach oben\"", reihenfolge);
        Assert.Contains("Header=\"Nach unten\"", reihenfolge);
        Assert.DoesNotContain("Header=\"Hoch\"", reihenfolge);
        Assert.DoesNotContain("Header=\"Runter\"", reihenfolge);
    }

    /// <summary>Beide «Bearbeiten»-Untermenüs enthalten mindestens einen echten Eintrag.</summary>
    [Theory]
    [InlineData("DataPage.xaml")]
    [InlineData("SchaechtePage.xaml")]
    public void Bearbeiten_Untermenue_ist_nicht_leer(string dateiname)
        => Assert.NotEmpty(LiesUntermenueKindElemente(dateiname, "Bearbeiten"));

    /// <summary>Beide «Ausgabe»-Untermenüs enthalten mindestens einen echten Eintrag.</summary>
    [Theory]
    [InlineData("DataPage.xaml")]
    [InlineData("SchaechtePage.xaml")]
    public void Ausgabe_Untermenue_ist_nicht_leer(string dateiname)
        => Assert.NotEmpty(LiesUntermenueKindElemente(dateiname, "Ausgabe"));

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
        => LiesUntermenueGruppe(dateiname, titel).ToString();

    /// <summary>Echte Menüpunkte (MenuItem, keine Separator) direkt unter dem benannten Untermenü.</summary>
    private static IReadOnlyList<XElement> LiesUntermenueKindElemente(string dateiname, string titel)
    {
        var gruppe = LiesUntermenueGruppe(dateiname, titel);
        return gruppe.Elements(gruppe.Name.Namespace + "MenuItem").ToList();
    }

    private static XElement LiesUntermenueGruppe(string dateiname, string titel)
    {
        var contextMenu = WeitereAktionenContextMenu(dateiname);
        var ns = contextMenu.Name.Namespace;
        var gruppe = contextMenu.Elements(ns + "MenuItem")
            .FirstOrDefault(mi => (string?)mi.Attribute("Header") == titel);
        Assert.True(gruppe is not null, $"{dateiname}: Untermenü \"{titel}\" nicht gefunden.");
        return gruppe!;
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
