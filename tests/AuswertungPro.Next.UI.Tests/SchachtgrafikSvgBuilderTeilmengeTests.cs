using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using AuswertungPro.Next.Application.Reports;
using AuswertungPro.Next.UI.Controls;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Der Vertrag zwischen Schachtgrafik-Bauer und Zeichner — analog
/// <see cref="HaltungsgrafikSvgBuilderTeilmengeTests"/>: Alles, was
/// <see cref="SchachtgrafikSvgBuilder"/> schreibt, muss <see cref="SvgTeilmengeZeichner"/> auch
/// zeichnen koennen, und jede feste Farbe braucht eine Theme-Zuordnung.
/// </summary>
public sealed class SchachtgrafikSvgBuilderTeilmengeTests
{
    private static readonly string[] Quelldateien = ["SchachtgrafikSvgBuilder.cs"];

    [Fact]
    public void Ein_vollstaendiges_Beispiel_liegt_ganz_in_der_Teilmenge()
    {
        SvgTeilmengeZeichner.PruefeTeilmenge(BeispielSvg(mitTiefe: true));
        SvgTeilmengeZeichner.PruefeTeilmenge(BeispielSvg(mitTiefe: false));
    }

    /// <summary>Jedes Element und jedes Attribut des Beispiels steht in der Teilmenge.</summary>
    [Fact]
    public void Beispiel_verwendet_nur_bekannte_Elemente_und_Attribute()
    {
        var wurzel = XDocument.Parse(BeispielSvg(mitTiefe: true)).Root!;
        foreach (var element in wurzel.DescendantsAndSelf())
        {
            Assert.Contains(element.Name.LocalName, SvgTeilmengeZeichner.UnterstuetzteElemente);
            var erlaubt = SvgTeilmengeZeichner.UnterstuetzteAttribute[element.Name.LocalName];
            foreach (var attribut in element.Attributes().Where(a => !a.IsNamespaceDeclaration))
                Assert.Contains(attribut.Name.LocalName, erlaubt);
        }
    }

    /// <summary>Quelltext-Gegenprobe: Auch ein Zweig ohne Beispieldaten darf kein fremdes Element schreiben.</summary>
    [Fact]
    public void Der_Quelltext_schreibt_nur_Elemente_der_Teilmenge()
    {
        var muster = new Regex(@"(?<![A-Za-z0-9_])<\/?([a-zA-Z][A-Za-z0-9]*)[\s/>]", RegexOptions.Compiled);
        var unbekannt = new List<string>();
        var gefunden = new HashSet<string>(StringComparer.Ordinal);

        foreach (var datei in Quelldateien)
        {
            foreach (var zeile in Codezeilen(datei))
            {
                foreach (Match treffer in muster.Matches(zeile))
                {
                    var name = treffer.Groups[1].Value;
                    gefunden.Add(name);
                    if (!SvgTeilmengeZeichner.UnterstuetzteElemente.Contains(name))
                        unbekannt.Add($"{datei}: <{name}>");
                }
            }
        }

        Assert.True(
            unbekannt.Count == 0,
            "Der Schachtgrafik-Bauer schreibt SVG-Elemente, die der Zeichner nicht kennt:\n"
            + string.Join("\n", unbekannt.Distinct()));

        foreach (var pflicht in new[] { "svg", "defs", "rect", "line", "circle", "text", "path", "polygon", "ellipse" })
            Assert.Contains(pflicht, gefunden);
    }

    /// <summary>Quelltext-Gegenprobe fuer Attribute — analog zur Element-Gegenprobe oben.</summary>
    [Fact]
    public void Der_Quelltext_schreibt_nur_Attribute_der_Teilmenge()
    {
        var muster = new Regex(@"(?<![A-Za-z0-9_-])([a-zA-Z][a-zA-Z0-9-]*)='", RegexOptions.Compiled);
        var bekannt = new HashSet<string>(
            SvgTeilmengeZeichner.UnterstuetzteAttribute.Values.SelectMany(a => a),
            StringComparer.Ordinal);
        bekannt.Add("xmlns");

        var unbekannt = new List<string>();
        var gefunden = new HashSet<string>(StringComparer.Ordinal);

        foreach (var datei in Quelldateien)
        {
            foreach (var zeile in Codezeilen(datei))
            {
                foreach (Match treffer in muster.Matches(zeile))
                {
                    var name = treffer.Groups[1].Value;
                    gefunden.Add(name);
                    if (!bekannt.Contains(name))
                        unbekannt.Add($"{datei}: {name}='");
                }
            }
        }

        Assert.True(
            unbekannt.Count == 0,
            "Der Schachtgrafik-Bauer schreibt SVG-Attribute, die der Zeichner nicht kennt:\n"
            + string.Join("\n", unbekannt.Distinct()));

        foreach (var pflicht in new[] { "x", "y", "width", "height", "fill", "stroke", "d", "points" })
            Assert.Contains(pflicht, gefunden);
    }

    /// <summary>Jede feste Farbe des Bauers hat ein Theme-Token — dieselbe Palette wie die Haltungsgrafik.</summary>
    [Fact]
    public void Jede_feste_Farbe_des_Bauers_hat_eine_Zuordnung()
    {
        var muster = new Regex("#[0-9A-Fa-f]{6}(?![0-9A-Fa-f])", RegexOptions.Compiled);
        var ohneZuordnung = new List<string>();

        foreach (var datei in Quelldateien)
        {
            foreach (var zeile in Codezeilen(datei))
            {
                foreach (Match treffer in muster.Matches(zeile))
                {
                    if (!SvgFarbZuordnung.IstBekannt(treffer.Value))
                        ohneZuordnung.Add($"{datei}: {treffer.Value}");
                }
            }
        }

        Assert.True(
            ohneZuordnung.Count == 0,
            "Farben ohne Theme-Zuordnung (SvgFarbZuordnung ergaenzen):\n"
            + string.Join("\n", ohneZuordnung.Distinct()));
    }

    private static IEnumerable<string> Codezeilen(string datei)
    {
        var pfad = RepoFile("src", "AuswertungPro.Next.Application", "Reports", datei);
        foreach (var zeile in File.ReadAllLines(pfad))
        {
            var text = zeile.TrimStart();
            if (text.StartsWith("//", StringComparison.Ordinal))
                continue;
            yield return zeile;
        }
    }

    /// <summary>
    /// Ein Beispiel mit allen Symbolkategorien, Zu- und Ablaeufen (inklusive Ueberlauf "+n")
    /// sowie Tiefe/ohne Tiefe.
    /// </summary>
    private static string BeispielSvg(bool mitTiefe)
    {
        var schaeden = new List<SchachtgrafikSchadenEintrag>
        {
            new(SchachtZone.Konus, "crack", "#D64541", "K1"),
            new(SchachtZone.Schachtwand, "roots", "#27AE60", "W1"),
            new(SchachtZone.Anschluss, "obstacle", "#6B7280", "A1"),
            new(SchachtZone.Sohle, "deposit", "#8B6914", "S1"),
        };

        var zulaeufe = Enumerable.Range(1, 5)
            .Select(i => new SchachtgrafikStummel($"H{i}", "DN200"))
            .ToList();
        var ablaeufe = new List<SchachtgrafikStummel> { new("H9", "DN300") };

        var (svg, _) = SchachtgrafikSvgBuilder.Baue(
            "12345",
            mitTiefe ? 2.4 : null,
            "1100",
            "900",
            zulaeufe,
            ablaeufe,
            schaeden);
        return svg;
    }
}
