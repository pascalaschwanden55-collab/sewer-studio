using System.IO;
using System.Xml.Linq;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Wartbarkeitsaudit 30.09.2026, Q3 (Befund Z3): Ein benannter Stil oder eine benannte Vorlage, die
/// in Theme.xaml und ThemeLight.xaml zeichengleich (ohne Kommentare und Leerraum) stehen, gehoert
/// genau einmal nach Theme/Controls.xaml. Vorher waren 18 Stile doppelt gepflegt, und jede Aenderung
/// musste an beiden Stellen nachgezogen werden. Bleibt ein Stil je Theme, muss er sich also
/// tatsaechlich unterscheiden (feste Farben, andere Masse).
/// </summary>
public sealed class DesignAuditThemeStileEinmalTests
{
    private static readonly string[] Bausteine = { "Style", "ControlTemplate", "DataTemplate" };

    /// <summary>
    /// Zeichengleiche Stile, die bewusst je Theme stehen bleiben. Jede Ausnahme braucht einen Grund
    /// und wird geprueft: Verschwindet die Gleichheit (oder der Stil), macht die Liste den Test rot.
    /// </summary>
    private static readonly Dictionary<string, string> Ausnahmen = new(StringComparer.Ordinal)
    {
        // BasedOn="{StaticResource ToolbarButton}": ToolbarButton hat feste, je Theme verschiedene
        // Hover-/Druckfarben und bleibt deshalb je Theme. Ein Stil in Controls.xaml wuerde sein
        // BasedOn beim Laden auf das damalige Theme einfrieren und beim Designwechsel die alten
        // Hover-Farben behalten. Erst wenn ToolbarButton tokenisiert ist, kann er nachziehen.
        ["ToolbarButtonDanger"] = "abgeleitet von ToolbarButton, der je Theme verschieden ist",
    };

    [Fact]
    public void Kein_benannter_Stil_steht_zeichengleich_in_Theme_und_ThemeLight()
    {
        var dunkel = Benannte(Lade("Theme.xaml"));
        var hell = Benannte(Lade("ThemeLight.xaml"));

        var doppelt = dunkel
            .Where(k => hell.TryGetValue(k.Key, out var gegenstueck) && gegenstueck == k.Value)
            .Select(k => k.Key)
            .Where(k => !Ausnahmen.ContainsKey(k))
            .OrderBy(k => k, StringComparer.Ordinal)
            .ToList();

        Assert.True(doppelt.Count == 0,
            "Diese benannten Stile/Vorlagen stehen zeichengleich in Theme.xaml UND ThemeLight.xaml. "
            + "Genau einmal nach Theme/Controls.xaml verschieben (Farben ueber DynamicResource-Tokens): "
            + string.Join(", ", doppelt));
    }

    [Fact]
    public void Die_Ausnahmen_sind_nicht_veraltet()
    {
        var dunkel = Benannte(Lade("Theme.xaml"));
        var hell = Benannte(Lade("ThemeLight.xaml"));

        foreach (var (schluessel, grund) in Ausnahmen)
        {
            Assert.True(dunkel.TryGetValue(schluessel, out var d) && hell.TryGetValue(schluessel, out var h) && d == h,
                $"Ausnahme {schluessel} ({grund}) ist veraltet: Der Stil steht nicht mehr zeichengleich in beiden Themes - Eintrag entfernen.");
        }
    }

    [Fact]
    public void Der_Waechter_erkennt_zwei_gleiche_Stile_trotz_anderer_Kommentare_und_anderem_Leerraum()
    {
        // Selbsttest der Vergleichsregel: Kommentare und Leerraum zaehlen nicht, ein anderer Wert schon.
        var a = Benannte(XDocument.Parse(
            "<R xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\">"
            + "<Style x:Key=\"S\"><!-- eins --><Setter Property=\"A\" Value=\"1\"/></Style>"
            + "<Style x:Key=\"T\"><Setter Property=\"A\" Value=\"1\"/></Style></R>"));
        var b = Benannte(XDocument.Parse(
            "<R xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\">\n"
            + "  <Style x:Key=\"S\">\n    <!-- zwei, ganz anders -->\n    <Setter   Property=\"A\"   Value=\"1\"/>\n  </Style>\n"
            + "  <Style x:Key=\"T\"><Setter Property=\"A\" Value=\"2\"/></Style></R>"));

        Assert.Equal(a["S"], b["S"]);
        Assert.NotEqual(a["T"], b["T"]);
    }

    private static XDocument Lade(string datei)
        => XDocument.Load(RepoFile("src", "AuswertungPro.Next.UI", "Theme", datei));

    /// <summary>Alle benannten Stile/Vorlagen (auch verschachtelte) als normalisierter Text je Schluessel.</summary>
    private static Dictionary<string, string> Benannte(XDocument dokument)
    {
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var ergebnis = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var element in dokument.Descendants().Where(e => Bausteine.Contains(e.Name.LocalName)))
        {
            var schluessel = (string?)element.Attribute(x + "Key");
            if (schluessel is null)
                continue;

            var kopie = new XElement(element);
            kopie.DescendantNodes().OfType<XComment>().ToList().ForEach(c => c.Remove());
            kopie.DescendantNodes().OfType<XText>().Where(t => string.IsNullOrWhiteSpace(t.Value)).ToList()
                .ForEach(t => t.Remove());
            ergebnis[schluessel] = kopie.ToString(SaveOptions.DisableFormatting);
        }

        return ergebnis;
    }
}
