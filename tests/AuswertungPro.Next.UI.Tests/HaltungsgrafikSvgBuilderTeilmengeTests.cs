using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using AuswertungPro.Next.Application.Reports;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Controls;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Der Vertrag zwischen Grafikbauer und Zeichner: Alles, was
/// <see cref="HaltungsgrafikSvgBuilder"/> schreibt, muss
/// <see cref="SvgTeilmengeZeichner"/> auch zeichnen koennen. Wer im Bauer ein neues SVG-Element,
/// Attribut oder eine neue Farbe einfuehrt, muss den Zeichner erweitern — sonst faellt dieser
/// Waechter um und nicht erst der Benutzer.
///
/// Geprueft wird doppelt: an einem erzeugten SVG mit allen Symbolarten UND am Quelltext, denn
/// ein Zweig, den die Beispieldaten nicht treffen, wuerde sonst durchrutschen.
/// </summary>
public sealed class HaltungsgrafikSvgBuilderTeilmengeTests
{
    private static readonly string[] Quelldateien =
    [
        "HaltungsgrafikSvgBuilder.cs",
        "DamageSymbolRenderer.cs"
    ];

    [Fact]
    public void Ein_vollstaendiges_Beispiel_liegt_ganz_in_der_Teilmenge()
    {
        SvgTeilmengeZeichner.PruefeTeilmenge(BeispielSvg(flowDown: true));
        SvgTeilmengeZeichner.PruefeTeilmenge(BeispielSvg(flowDown: false));
        SvgTeilmengeZeichner.PruefeTeilmenge(BeispielSvg(flowDown: null));
    }

    /// <summary>Jedes Element und jedes Attribut des Beispiels steht in der Teilmenge.</summary>
    [Fact]
    public void Beispiel_verwendet_nur_bekannte_Elemente_und_Attribute()
    {
        var wurzel = XDocument.Parse(BeispielSvg(flowDown: true)).Root!;
        foreach (var element in wurzel.DescendantsAndSelf())
        {
            Assert.Contains(element.Name.LocalName, SvgTeilmengeZeichner.UnterstuetzteElemente);
            var erlaubt = SvgTeilmengeZeichner.UnterstuetzteAttribute[element.Name.LocalName];
            foreach (var attribut in element.Attributes().Where(a => !a.IsNamespaceDeclaration))
                Assert.Contains(attribut.Name.LocalName, erlaubt);
        }
    }

    /// <summary>
    /// Quelltext-Gegenprobe: Auch ein Zweig ohne Beispieldaten darf kein fremdes Element
    /// schreiben.
    /// </summary>
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
            "Der Grafikbauer schreibt SVG-Elemente, die der Zeichner nicht kennt:\n"
            + string.Join("\n", unbekannt.Distinct()));

        // Gegenprobe, damit der Waechter nicht still leer laeuft, falls das Muster einmal
        // nicht mehr greift.
        foreach (var pflicht in new[] { "svg", "defs", "rect", "line", "circle", "text", "path", "polygon", "ellipse" })
            Assert.Contains(pflicht, gefunden);
    }

    /// <summary>
    /// Jede feste Farbe des Bauers hat ein Theme-Token. Ohne Zuordnung stuende die Grafik im
    /// dunklen Design mit hellen Druckfarben da.
    /// </summary>
    [Fact]
    public void Jede_feste_Farbe_des_Bauers_hat_eine_Zuordnung()
    {
        // Achtstellige Werte tragen ihre Deckung selbst und kommen nur in Filtern vor
        // (Schattenfarbe), nicht als Oberflaechenfarbe.
        var muster = new Regex("#[0-9A-Fa-f]{6}(?![0-9A-Fa-f])", RegexOptions.Compiled);
        var ohneZuordnung = new List<string>();

        var dateien = Quelldateien.Concat(["DamageSymbolClassifier.cs", "HaltungsgrafikLabelLayout.cs"]);
        foreach (var datei in dateien)
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

    /// <summary>Ein fremdes Element bleibt ein Fehler mit Namen, kein stilles Weglassen.</summary>
    [Fact]
    public void Ein_fremdes_Element_wird_namentlich_abgewiesen()
    {
        var fehler = Assert.Throws<NotSupportedException>(() => SvgTeilmengeZeichner.PruefeTeilmenge(
            "<svg xmlns='http://www.w3.org/2000/svg' width='10' height='10'><image x='1'/></svg>"));
        Assert.Contains("image", fehler.Message, StringComparison.Ordinal);
    }

    /// <summary>Auch ein fremdes Attribut faellt auf, damit eine neue Angabe nicht still verschwindet.</summary>
    [Fact]
    public void Ein_fremdes_Attribut_wird_namentlich_abgewiesen()
    {
        var fehler = Assert.Throws<NotSupportedException>(() => SvgTeilmengeZeichner.PruefeTeilmenge(
            "<svg xmlns='http://www.w3.org/2000/svg' width='10' height='10'><rect mask='url(#m)'/></svg>"));
        Assert.Contains("mask", fehler.Message, StringComparison.Ordinal);
    }

    /// <summary>Ein unbekannter Pfadbefehl (etwa ein Bogen) muss bewusst geprueft werden.</summary>
    [Fact]
    public void Ein_fremder_Pfadbefehl_wird_abgewiesen()
    {
        var fehler = Assert.Throws<NotSupportedException>(() => SvgTeilmengeZeichner.PruefeTeilmenge(
            "<svg xmlns='http://www.w3.org/2000/svg' width='10' height='10'>"
            + "<path d='M 0,0 A 5 5 0 0 1 10,10'/></svg>"));
        Assert.Contains("A", fehler.Message, StringComparison.Ordinal);
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
    /// Ein SVG mit allen Symbolarten des Klassifizierers, Strecken- und Punktschaden,
    /// Seitenanschluss mit und ohne Uhrlage, Abbruch und einem nicht inspizierten Bereich.
    /// </summary>
    private static string BeispielSvg(bool? flowDown)
    {
        var eintraege = new List<ProtocolEntry>();
        var meter = 1.0;
        foreach (var code in new[]
                 {
                     "BAA", "BAB", "BAC", "BAD", "BAE", "BAF", "BAI", "BAJ",
                     "BAK", "BAL", "BBA", "BBB", "BBC", "XYZ"
                 })
        {
            eintraege.Add(Eintrag(code, meter));
            meter += 2.0;
        }

        eintraege.Add(new ProtocolEntry
        {
            Code = "BAF",
            Beschreibung = "Oberflaechenschaden",
            IsStreckenschaden = true,
            MeterStart = 32.0,
            MeterEnd = 38.0,
            CodeMeta = new ProtocolEntryCodeMeta { Code = "BAF" }
        });
        eintraege.Add(MitUhrlage("BCA", 40.0, "3"));
        eintraege.Add(MitUhrlage("BCA", 42.0, "12"));
        eintraege.Add(MitUhrlage("BCA", 44.0, "9"));
        eintraege.Add(Eintrag("BCA", 46.0));
        eintraege.Add(Eintrag("BDC", 48.0));

        return HaltungsgrafikSvgBuilder.BuildHaltungsgrafikSvg(
            50.0,
            eintraege,
            photoNumbers: null,
            "10001",
            "10002",
            flowDown,
            HaltungsgrafikAnsichtBuilder.Markenfarbe,
            overrideHeight: 700,
            unknownGaps: [new InspectionGap(20.0, 24.0)],
            catalog: null);
    }

    private static ProtocolEntry Eintrag(string code, double meter)
        => new()
        {
            Code = code,
            Beschreibung = code,
            MeterStart = meter,
            CodeMeta = new ProtocolEntryCodeMeta { Code = code, Severity = "3" }
        };

    private static ProtocolEntry MitUhrlage(string code, double meter, string uhr)
    {
        var eintrag = Eintrag(code, meter);
        eintrag.CodeMeta!.Parameters["Uhr_von"] = uhr;
        return eintrag;
    }
}
