using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>Ein tatsaechlich gerendertes Farbpaar: Text/Symbol (<see cref="Vordergrund"/>) auf einer
/// Flaeche (<see cref="Hintergrund"/>) an einer Stelle in einem bestimmten Zustand. Werte sind die
/// rohen XAML-Angaben ("{DynamicResource AccentBrush}", "White", ...); "?" heisst unbekannt
/// (gebunden, Konverter).</summary>
internal sealed record Farbpaar(string Datei, string Ort, string Zustand, string Hintergrund, string Vordergrund)
{
    public override string ToString() => $"{Datei}: {Ort} [{Zustand}] {Vordergrund} auf {Hintergrund}";
}

/// <summary>
/// Optikanalyse 28.09.2026, Aufgabe 13, Fix-Runde 4 (Befund E): ein kleines, bewusst statisches
/// Zustandsmodell der XAML-Darstellung. Statt einzelne Attribut-/Setter-Paare zu vergleichen,
/// bestimmt es fuer JEDEN Zustand (Grundzustand und jede Trigger-Bedingung) das wirksame Paar aus
/// Flaeche und Schrift jedes Text-/Symbol-Elements - mit der echten WPF-Rangfolge:
///
/// - Eigenschaften des Bedienelements: lokaler Wert &gt; Style-Trigger &gt; Template-Trigger ohne
///   TargetName &gt; Style-Setter (BasedOn-Kette aufgeloest, Basis zuerst).
/// - Elemente in einer Vorlage: Template-/DataTemplate-Trigger mit TargetName &gt; Attribut der
///   Vorlage; <c>{TemplateBinding X}</c> liest die wirksame Eigenschaft X des Bedienelements.
/// - Vordergrund wird vererbt (Foreground, TextElement.Foreground, TextBlock.Foreground), die
///   Flaeche ist die naechste gemalte Background/Fill-Angabe darueber.
/// - Ein TextBlock ohne eigene Schrift bekommt die Schrift seines impliziten Stils (Theme:
///   TextBrush) - ausser ein naeher liegender Resources-Block ueberschreibt ihn (B7: die
///   Knopfvorlagen reichen dort die Knopfschrift durch).
/// - Inhalt eines Knopfs (eigene Kind-Elemente in der Ansicht) wird am ContentPresenter seiner
///   Vorlage weitergefuehrt: er sitzt auf der Flaeche und erbt die Schrift, die dort gilt.
/// - Ein in einem Zustand eingeklapptes Element (Visibility Collapsed/Hidden) zaehlt dort nicht.
/// - IsPressed=True schliesst IsMouseOver=True ein (beide Trigger greifen beim Druecken).
///
/// Grenzen: Kombinationen unabhaengiger Bedingungen (z. B. IsSelected UND IsMouseOver) werden nur
/// ueber die IsPressed-Regel zusammen betrachtet; Storyboards, EventTrigger und im Code gesetzte
/// Farben bleiben unsichtbar; gebundene Farben gelten als unbekannt ("?") und werden nicht bewertet.
/// </summary>
internal sealed class XamlFarbpaarModell
{
    public const string Unbekannt = "?";

    private static readonly XNamespace XamlNs = "http://schemas.microsoft.com/winfx/2006/xaml";

    private static readonly HashSet<string> FarbEigenschaften = new(StringComparer.Ordinal)
    {
        "Background", "Foreground", "Fill", "Stroke", "Visibility",
    };

    private static readonly HashSet<string> ItemContainerTypen = new(StringComparer.Ordinal)
    {
        "ListBoxItem", "ListViewItem", "TreeViewItem", "ComboBoxItem", "DataGridRow", "DataGridCell",
    };

    private static readonly HashSet<string> TextFaehigeTypenOhneVorlage = new(StringComparer.Ordinal)
    {
        "Button", "ToggleButton", "RepeatButton", "RadioButton", "CheckBox",
        "MenuItem", "ListBoxItem", "ListViewItem", "TreeViewItem", "ComboBoxItem",
        "TabItem", "Expander", "GroupBox", "Label", "TextBox", "ContentControl",
    };

    private readonly List<Dictionary<string, XElement>> _global;

    /// <param name="globaleWoerterbuecher">Die programmweit eingemischten Woerterbuecher in
    /// Suchreihenfolge (App-Ressourcen, Theme, Controls.xaml, ...).</param>
    public XamlFarbpaarModell(IEnumerable<XElement> globaleWoerterbuecher)
    {
        _global = globaleWoerterbuecher.Select(SammleWoerterbuch).ToList();
    }

    // ═══════════════════════════════════════════════════════════════════════════════════
    // Oeffentliche Einstiege
    // ═══════════════════════════════════════════════════════════════════════════════════

    /// <summary>Alle Farbpaare einer XAML-Datei: jeder Style fuer sich (ohne lokale Werte) und der
    /// ganze sichtbare Elementbaum (Ansichten, Fenster, DataTemplates).</summary>
    public List<Farbpaar> SammlePaare(XElement wurzel, string datei)
    {
        var paare = new List<Farbpaar>();
        var dateiScope = new List<Dictionary<string, XElement>>();

        if (wurzel.Name.LocalName == "ResourceDictionary")
        {
            dateiScope.Add(SammleWoerterbuch(wurzel));
        }

        // 1) Jeder Style fuer sich, als waere er ein Bedienelement ohne eigene Werte.
        foreach (var stil in wurzel.DescendantsAndSelf().Where(e => e.Name.LocalName == "Style"))
        {
            // Inline-Stile eines Elements (<Border.Style>) bewertet der Elementbaum mit dem Element.
            if (stil.Parent is { } p && p.Name.LocalName.EndsWith(".Style", StringComparison.Ordinal))
                continue;

            var scope = BaueScope(stil, dateiScope);
            var name = StilName(stil);
            var zielTyp = KurzTyp(stil.Attribute("TargetType")?.Value) ?? "Control";
            var info = BaueStil(stil, scope, 0);
            BewerteGestyltesElement(
                element: null, zielTyp, info, localWerte: new Dictionary<string, string>(),
                inhalt: Array.Empty<XElement>(),
                new Kontext(null, null, scope, null, Array.Empty<string>()), datei, $"Style {name}", "Grundzustand", paare, 0);
        }

        // 2) Der sichtbare Elementbaum (nicht fuer reine Ressourcen-Woerterbuecher).
        if (wurzel.Name.LocalName != "ResourceDictionary")
        {
            Durchlaufe(wurzel, new Kontext(null, null, dateiScope, null, Array.Empty<string>()), datei, "", "Grundzustand",
                ueberschreibung: null, vorlagenBindung: null, paare, 0);
        }

        // Keyed DataTemplates in Ressourcen haben keinen Elternbaum - einzeln ohne Kontext.
        foreach (var dt in wurzel.Descendants().Where(e => e.Name.LocalName.EndsWith("DataTemplate", StringComparison.Ordinal)
                                                       && e.Parent is { } p
                                                       && (p.Name.LocalName.EndsWith(".Resources", StringComparison.Ordinal)
                                                           || p.Name.LocalName is "ResourceDictionary" or "Setter.Value")))
        {
            var scope = BaueScope(dt, dateiScope);
            BewerteDataTemplate(dt, new Kontext(null, null, scope, null, Array.Empty<string>()), datei, "DataTemplate", "Grundzustand", paare, 0);
        }

        return paare;
    }

    /// <summary>
    /// Regeln (HC = Zuordnung Schluessel -&gt; SystemColors-Name aus ThemeHighContrast.xaml):
    /// R1 Flaeche faellt unter Hochkontrast auf Highlight -&gt; Schrift muss auf HighlightText fallen.
    ///    Flaeche faellt auf eine andere Systemfarbe (Window/Control/...) -&gt; Schrift darf weder
    ///    literal (z. B. "White") noch HighlightText sein.
    /// R2 Schrift OnAccentBrush (bzw. jede andere "Text auf Akzent"-Farbe) -&gt; Flaeche muss unter
    ///    Hochkontrast auf Highlight fallen (sie ist in den normalen Themes Weiss und gehoert nur auf
    ///    eine gefuellte Akzentflaeche).
    /// </summary>
    public static List<string> Pruefe(IEnumerable<Farbpaar> paare, IReadOnlyDictionary<string, string> hc,
        IReadOnlySet<string> textAufAkzentSchluessel)
    {
        var verstoesse = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var p in paare)
        {
            if (p.Hintergrund == Unbekannt || p.Vordergrund == Unbekannt)
                continue;

            var bgKey = Schluessel(p.Hintergrund);
            var fgKey = Schluessel(p.Vordergrund);
            string? bgHc = bgKey is not null && hc.TryGetValue(bgKey, out var b) ? b : null;
            string? fgHc = fgKey is not null && hc.TryGetValue(fgKey, out var f) ? f : null;

            if (bgHc == "Highlight")
            {
                if (fgHc != "HighlightText")
                    verstoesse.Add($"R1 {p} (Flaeche Highlight, Schrift {(fgHc ?? (fgKey is null ? "literal" : "nicht ueberlagert"))})");
            }
            else if (bgHc is not null)
            {
                if (fgKey is null)
                    verstoesse.Add($"R1 {p} (Flaeche {bgHc}, Schrift literal)");
                else if (fgHc == "HighlightText")
                    verstoesse.Add($"R1 {p} (Flaeche {bgHc}, Schrift HighlightText)");
            }

            if (fgKey is not null && textAufAkzentSchluessel.Contains(fgKey) && bgHc != "Highlight")
                verstoesse.Add($"R2 {p} (Text-auf-Akzent-Farbe ausserhalb einer Akzentflaeche)");
        }

        return verstoesse.ToList();
    }

    /// <summary>Schluesselname aus "{DynamicResource X}"/"{StaticResource X}", sonst null (literal).</summary>
    public static string? Schluessel(string? wert)
    {
        if (string.IsNullOrEmpty(wert))
            return null;
        var m = Regex.Match(wert, @"^\{(?:Dynamic|Static)Resource\s+([A-Za-z0-9_]+)\s*\}$");
        return m.Success ? m.Groups[1].Value : null;
    }

    // ═══════════════════════════════════════════════════════════════════════════════════
    // Elementbaum
    // ═══════════════════════════════════════════════════════════════════════════════════

    /// <param name="Bg">Flaeche, auf der das Element sitzt (null = unbekannt).</param>
    /// <param name="Fg">Vererbte Schrift (null = unbekannt).</param>
    /// <param name="Scope">Ressourcen-Woerterbuecher der Vorfahren, innerstes zuletzt.</param>
    /// <param name="Inhalt">Innerhalb einer Vorlage: der Inhalt des Bedienelements, den der
    /// ContentPresenter dieser Vorlage zeigt.</param>
    /// <param name="Vorfahr">Zustand (Bedingungen) des naechsten Bedienelements mit Vorlage darueber -
    /// ein DataTrigger mit Bindung an eine Eigenschaft des Vorfahren (RelativeSource AncestorType/
    /// TemplatedParent, im Modell "^IsChecked=True") greift genau dann, wenn der Vorfahr in diesem
    /// Zustand ist.</param>
    private sealed record Kontext(string? Bg, string? Fg, List<Dictionary<string, XElement>> Scope, InhaltsWeiterleitung? Inhalt,
        IReadOnlyCollection<string> Vorfahr);

    private delegate string? Ueberschreibung(string? elementName, string eigenschaft);

    private void Durchlaufe(
        XElement e, Kontext k, string datei, string pfad, string zustand,
        Ueberschreibung? ueberschreibung, Func<string, string?>? vorlagenBindung,
        List<Farbpaar> paare, int tiefe)
    {
        if (tiefe > 80)
            return;

        var typ = e.Name.LocalName;
        if (typ is "Style" or "ControlTemplate" or "DataTemplate" or "HierarchicalDataTemplate" or "ResourceDictionary" or "Storyboard"
            or "VisualStateManager.VisualStateGroups")
            return;

        var scope = k.Scope;
        var ressourcen = e.Elements().FirstOrDefault(x => x.Name.LocalName == $"{typ}.Resources");
        if (ressourcen is not null)
            scope = scope.Append(SammleWoerterbuch(ressourcen)).ToList();

        var name = ElementName(e);
        var ort = string.IsNullOrEmpty(pfad) ? Beschreibe(e) : $"{pfad} > {Beschreibe(e)}";

        // Stil des Elements: explizit, inline oder implizit nach Typ.
        var stil = FindeElementStil(e, scope);
        var lokal = LiesLokaleWerte(e, ueberschreibung, vorlagenBindung);

        var eigeneVorlage = e.Elements().FirstOrDefault(x => x.Name.LocalName == $"{typ}.Template")
            ?.Elements().FirstOrDefault(x => x.Name.LocalName == "ControlTemplate");
        if (eigeneVorlage is null && e.Attribute("Template")?.Value is { } tAttr && ReferenzSchluessel(tAttr) is { } tKey)
            eigeneVorlage = Finde(tKey, scope) is { Name.LocalName: "ControlTemplate" } ct ? ct : null;
        if (eigeneVorlage is not null)
        {
            stil = stil is null ? new StilInfo("lokal") : stil.Kopie();
            stil.Vorlage = eigeneVorlage;
        }

        var inhalt = InhaltsElemente(e).ToList();

        if (stil?.Vorlage is not null)
        {
            BewerteGestyltesElement(e, typ, stil, lokal, inhalt, k with { Scope = scope }, datei, ort, zustand, paare, tiefe,
                ueberschreibung, vorlagenBindung);
            DurchlaufeEigenschaftsVorlagen(e, k with { Scope = scope }, datei, ort, zustand, paare, tiefe);
            return;
        }

        // Kein eigenes Template: das Element malt selbst (Background/Fill) und reicht Schrift weiter.
        foreach (var (stilZustand, roh) in ZustaendeOhneVorlage(stil, lokal, k.Vorfahr))
        {
            var wirksam = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var (p, v) in roh)
            {
                if (LoeseBindungAuf(v, p, vorlagenBindung) is { } r)
                    wirksam[p] = r;
            }

            var z = Verbinde(zustand, stilZustand);

            // Eingeklappt zaehlt nur innerhalb einer Vorlage (dort schaltet ein Trigger es ein). In
            // einer Ansicht kann Code-behind ein eingeklapptes Element jederzeit zeigen (BtnUndo).
            if (ueberschreibung is not null && IstVerborgen(wirksam))
                continue;

            var bg = Malt(wirksam.GetValueOrDefault("Background")) ? wirksam["Background"] : k.Bg;
            var fg = wirksam.GetValueOrDefault("Foreground");
            var fgGeerbt = fg ?? k.Fg;

            var kindKontext = k with { Bg = bg, Fg = fgGeerbt, Scope = stil is null ? scope : scope.Concat(stil.Ressourcen).ToList() };

            if (typ == "ContentPresenter")
            {
                // Ein ContentPresenter macht aus Text-Inhalt einen TextBlock, und der bekommt den
                // impliziten TextBlock-Stil, der AM ContentPresenter gilt (zur Laufzeit belegt: im
                // Theme mit B7-Durchreich-Stil die Knopfschrift, ohne ihn TextBrush).
                var weiter = k.Inhalt;
                if (weiter is null || weiter.TextInhaltMoeglich)
                {
                    var textFg = fg ?? ImpliziteTextBlockSchrift(scope, fgGeerbt);
                    if (bg is not null && textFg is not null)
                        paare.Add(new Farbpaar(datei, ort, z, bg, textFg));
                }

                // Eigene Kind-Elemente des Bedienelements sitzen auf dieser Flaeche, loesen ihre
                // Stile aber im LOGISCHEN Baum auf (Ansicht, nicht Vorlage) - ein eigener TextBlock
                // ohne Schrift bekommt deshalb TextBrush, auch in einer Vorlage mit B7 (zur Laufzeit
                // belegt).
                if (weiter is not null && (e.Attribute("ContentSource")?.Value is null or "Content"))
                {
                    foreach (var kind in weiter.Elemente)
                    {
                        Durchlaufe(kind, kindKontext with { Inhalt = null, Scope = weiter.AnsichtsScope }, datei,
                            $"{ort} > Inhalt", z, weiter.AeussereUeberschreibung, weiter.AeussereBindung, paare, tiefe + 1);
                    }
                }
            }
            else
            {
                ErfasseTinte(e, typ, wirksam, bg, fgGeerbt, fg is not null, stil, k, datei, ort, z, paare);
            }

            foreach (var kind in inhalt)
                Durchlaufe(kind, kindKontext, datei, ort, z, ueberschreibung, vorlagenBindung, paare, tiefe + 1);
            DurchlaufeEigenschaftsVorlagen(e, kindKontext, datei, ort, z, paare, tiefe);
        }
    }

    /// <summary>Text-/Symbol-Elemente erfassen (ohne eigene Vorlage).</summary>
    private void ErfasseTinte(
        XElement e, string typ, Dictionary<string, string> wirksam, string? bg, string? fg, bool fgEigen,
        StilInfo? stil, Kontext k, string datei, string ort, string zustand, List<Farbpaar> paare)
    {
        switch (typ)
        {
            case "TextBlock" or "AccessText" or "Run" or "FluentIcon":
                if (bg is not null && fg is not null)
                    paare.Add(new Farbpaar(datei, ort, zustand, bg, fg));
                break;

            case "Path" or "Ellipse" or "Rectangle" or "Polygon" or "Line" or "Polyline":
            {
                var fill = wirksam.GetValueOrDefault("Fill");
                var stroke = wirksam.GetValueOrDefault("Stroke");
                if (Malt(fill) && stroke is not null && !IstTransparent(stroke))
                    paare.Add(new Farbpaar(datei, ort, zustand, fill!, stroke));
                else if (typ == "Path" && stroke is not null && !IstTransparent(stroke) && !Malt(fill) && bg is not null)
                    paare.Add(new Farbpaar(datei, ort, zustand, bg, stroke));
                else if (typ == "Path" && Malt(fill) && stroke is null && bg is not null)
                    paare.Add(new Farbpaar(datei, ort, zustand, bg, fill!));
                break;
            }

            default:
                // Bedienelement ohne bekannte Vorlage (WPF-Standardvorlage): zaehlt als Text, wenn es
                // Text als Attribut traegt.
                if (TextFaehigeTypenOhneVorlage.Contains(typ)
                    && (e.Attribute("Content") is not null || e.Attribute("Text") is not null || e.Attribute("Header") is not null)
                    && bg is not null && fg is not null)
                {
                    paare.Add(new Farbpaar(datei, ort, zustand, bg, fg));
                }
                break;
        }
    }

    /// <summary>
    /// Ein Bedienelement mit Vorlage (bzw. ein Style fuer sich, dann ist <paramref name="element"/>
    /// null): je Zustand die wirksamen Eigenschaften bestimmen, die Vorlage durchlaufen und den
    /// Inhalt am ContentPresenter weiterfuehren.
    /// </summary>
    private void BewerteGestyltesElement(
        XElement? element, string zielTyp, StilInfo stil, Dictionary<string, string> localWerte,
        IReadOnlyList<XElement> inhalt, Kontext k, string datei, string ort, string zustand,
        List<Farbpaar> paare, int tiefe,
        Ueberschreibung? aeussereUeberschreibung = null, Func<string, string?>? aeussereBindung = null)
    {
        var vorlage = stil.Vorlage;
        var vorlagenTrigger = vorlage is null ? new List<Ausloeser>() : LiesTrigger(vorlage, $"{vorlage.Name.LocalName}.Triggers", k.Scope);

        foreach (var z in Zustaende(stil.Trigger.Concat(vorlagenTrigger)))
        {
            var aktiveStil = stil.Trigger.Where(t => t.GiltIn(z, k.Vorfahr)).ToList();
            var aktiveVorlage = vorlagenTrigger.Where(t => t.GiltIn(z, k.Vorfahr)).ToList();

            string? Steuer(string eigenschaft)
            {
                if (localWerte.TryGetValue(eigenschaft, out var l))
                    return l;
                var st = aktiveStil.SelectMany(t => t.Setter).LastOrDefault(s => s.Ziel is null && s.Eigenschaft == eigenschaft);
                var roh = st?.Wert
                          ?? aktiveVorlage.SelectMany(t => t.Setter).LastOrDefault(s => s.Ziel is null && s.Eigenschaft == eigenschaft)?.Wert
                          ?? stil.Setter.GetValueOrDefault(eigenschaft);
                return roh is null ? null : LoeseBindungAuf(roh, eigenschaft, aeussereBindung);
            }

            string? Ziel(string? elementName, string eigenschaft)
            {
                if (elementName is null)
                    return null;
                return aktiveVorlage.SelectMany(t => t.Setter)
                    .LastOrDefault(s => s.Ziel == elementName && s.Eigenschaft == eigenschaft)?.Wert;
            }

            var zText = Verbinde(zustand, Beschreibe(z));
            // Siehe Durchlaufe: ein lokales "Collapsed" zaehlt nur innerhalb einer Vorlage.
            var sichtbarkeit = aeussereUeberschreibung is null && localWerte.ContainsKey("Visibility")
                ? null
                : Steuer("Visibility");
            if (IstVerborgenWert(sichtbarkeit))
                continue;

            var fg = Steuer("Foreground") ?? k.Fg;

            if (vorlage is null)
            {
                // Style ohne Vorlage (WPF-Standardvorlage): die Flaeche liegt hinter dem Inhalt.
                var bg = Steuer("Background");
                if (TextFaehigeTypenOhneVorlage.Contains(zielTyp) || zielTyp == "TextBlock")
                {
                    if (Malt(bg) && fg is not null)
                        paare.Add(new Farbpaar(datei, ort, zText, bg!, fg));
                }
                continue;
            }

            var ansichtsScope = k.Scope.Concat(stil.Ressourcen).ToList();
            var vorlagenScope = ansichtsScope;
            var vRes = vorlage.Elements().FirstOrDefault(x => x.Name.LocalName == "ControlTemplate.Resources");
            if (vRes is not null)
                vorlagenScope = vorlagenScope.Append(SammleWoerterbuch(vRes)).ToList();

            // Ein Item-Container (ListBoxItem ...) zeigt als Style fuer sich praktisch nie Text-Inhalt:
            // der kommt aus dem ItemTemplate, das dieses Modell nicht mit dem Container verbindet
            // (dokumentierte Grenze, siehe Klassendoku).
            var textInhalt = inhalt.Count == 0 && !(element is null && ItemContainerTypen.Contains(zielTyp));
            var vorlagenKontext = new Kontext(k.Bg, fg, vorlagenScope,
                new InhaltsWeiterleitung(inhalt, textInhalt, ansichtsScope, aeussereUeberschreibung, aeussereBindung),
                z);
            foreach (var wurzel in InhaltsElemente(vorlage))
            {
                Durchlaufe(wurzel, vorlagenKontext, datei, $"{ort} (Vorlage)", zText,
                    (n, p) => Ziel(n, p), Steuer, paare, tiefe + 1);
            }
        }
    }

    /// <summary>Traegt den Inhalt eines Knopfs bis zum ContentPresenter seiner Vorlage - samt den
    /// Ueberschreibungen/Bindungen der Vorlage, in der der Knopf selbst steht.</summary>
    private sealed record InhaltsWeiterleitung(
        IReadOnlyList<XElement> Elemente, bool TextInhaltMoeglich, List<Dictionary<string, XElement>> AnsichtsScope,
        Ueberschreibung? AeussereUeberschreibung, Func<string, string?>? AeussereBindung);

    /// <summary>Schrift eines impliziten TextBlock-Stils im Scope; eine Bindung an die Schrift des
    /// Vorfahren (B7) oder kein Stil heisst: die vererbte Schrift.</summary>
    private string? ImpliziteTextBlockSchrift(List<Dictionary<string, XElement>> scope, string? geerbt)
    {
        if (Finde("type:TextBlock", scope) is not { Name.LocalName: "Style" } stil)
            return geerbt;
        var info = BaueStil(stil, scope, 0);
        return info.Setter.TryGetValue("Foreground", out var v)
            ? LoeseBindungAuf(v, "Foreground", null) ?? geerbt
            : geerbt;
    }

    private void DurchlaufeEigenschaftsVorlagen(XElement e, Kontext k, string datei, string ort, string zustand, List<Farbpaar> paare, int tiefe)
    {
        foreach (var pe in e.Elements().Where(x => x.Name.LocalName.Contains('.')
                                                 && !x.Name.LocalName.EndsWith(".Resources", StringComparison.Ordinal)
                                                 && !x.Name.LocalName.EndsWith(".Style", StringComparison.Ordinal)
                                                 && !x.Name.LocalName.EndsWith(".Template", StringComparison.Ordinal)))
        {
            foreach (var dt in pe.Elements().Where(x => x.Name.LocalName.EndsWith("DataTemplate", StringComparison.Ordinal)))
                BewerteDataTemplate(dt, k with { Inhalt = null }, datei, ort, zustand, paare, tiefe + 1);
        }
    }

    private void BewerteDataTemplate(XElement dt, Kontext k, string datei, string ort, string zustand, List<Farbpaar> paare, int tiefe)
    {
        if (tiefe > 80)
            return;

        var scope = k.Scope;
        var res = dt.Elements().FirstOrDefault(x => x.Name.LocalName == "DataTemplate.Resources");
        if (res is not null)
            scope = scope.Append(SammleWoerterbuch(res)).ToList();

        var trigger = LiesTrigger(dt, "DataTemplate.Triggers", scope);
        foreach (var z in Zustaende(trigger))
        {
            var aktiv = trigger.Where(t => t.GiltIn(z, k.Vorfahr)).ToList();
            string? Ziel(string? elementName, string eigenschaft)
                => elementName is null
                    ? null
                    : aktiv.SelectMany(t => t.Setter).LastOrDefault(s => s.Ziel == elementName && s.Eigenschaft == eigenschaft)?.Wert;

            foreach (var wurzel in InhaltsElemente(dt))
            {
                Durchlaufe(wurzel, k with { Scope = scope, Inhalt = null }, datei, $"{ort} > DataTemplate",
                    Verbinde(zustand, Beschreibe(z)), (n, p) => Ziel(n, p), null, paare, tiefe + 1);
            }
        }
    }

    /// <summary>Wirksame Werte je Zustand fuer ein Element OHNE eigene Vorlage (inline-/impliziter
    /// Stil mit Triggern, z. B. eine Border mit DataTrigger).</summary>
    private static IEnumerable<(string Zustand, Dictionary<string, string> Werte)> ZustaendeOhneVorlage(
        StilInfo? stil, Dictionary<string, string> lokal, IReadOnlyCollection<string> vorfahr)
    {
        var trigger = stil?.Trigger ?? new List<Ausloeser>();
        foreach (var z in Zustaende(trigger))
        {
            var werte = new Dictionary<string, string>(StringComparer.Ordinal);
            if (stil is not null)
            {
                foreach (var (p, v) in stil.Setter)
                    werte[p] = v;
                foreach (var s in trigger.Where(t => t.GiltIn(z, vorfahr)).SelectMany(t => t.Setter).Where(s => s.Ziel is null))
                    werte[s.Eigenschaft] = s.Wert;
            }

            foreach (var (p, v) in lokal)
                werte[p] = v;

            yield return (Beschreibe(z), werte);
        }
    }

    /// <summary>Lokale Werte eines Elements: Attribut bzw. Eigenschaftselement, aufgeloeste
    /// TemplateBindings; ein Trigger-Setter mit TargetName (aus der umschliessenden Vorlage)
    /// schlaegt den in der Vorlage gesetzten Wert.</summary>
    private Dictionary<string, string> LiesLokaleWerte(XElement e, Ueberschreibung? ueberschreibung, Func<string, string?>? vorlagenBindung)
    {
        var werte = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var attr in e.Attributes())
        {
            var p = Normalisiere(attr.Name.LocalName);
            if (p is null || !FarbEigenschaften.Contains(p))
                continue;
            // Foreground hat Vorrang vor TextElement./TextBlock.Foreground am selben Element nicht -
            // WPF kennt nur eine Eigenschaft; der zuletzt genannte Wert zaehlt.
            werte[p] = attr.Value;
        }

        foreach (var pe in e.Elements().Where(x => x.Name.LocalName.Contains('.')))
        {
            var p = Normalisiere(pe.Name.LocalName[(pe.Name.LocalName.IndexOf('.') + 1)..]);
            if (p is null || !FarbEigenschaften.Contains(p) || p == "Visibility")
                continue;
            werte[p] = WertAusEigenschaftsElement(pe);
        }

        var aufgeloest = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (p, v) in werte)
        {
            var r = LoeseBindungAuf(v, p, vorlagenBindung);
            if (r is not null)
                aufgeloest[p] = r;
        }

        if (ueberschreibung is not null)
        {
            var name = ElementName(e);
            foreach (var p in FarbEigenschaften)
            {
                if (ueberschreibung(name, p) is { } ue && LoeseBindungAuf(ue, p, vorlagenBindung) is { } uer)
                    aufgeloest[p] = uer;
            }
        }

        return aufgeloest;
    }

    /// <summary>TemplateBinding/RelativeSource-TemplatedParent -&gt; Wert des Bedienelements;
    /// Bindung an die Schrift eines Vorfahren -&gt; null (= vererbt); jede andere Bindung -&gt; "?".</summary>
    private static string? LoeseBindungAuf(string wert, string eigenschaft, Func<string, string?>? vorlagenBindung)
    {
        var tb = Regex.Match(wert, @"^\{TemplateBinding\s+(?:Property=)?([A-Za-z.]+)\s*\}$");
        if (tb.Success)
            return vorlagenBindung?.Invoke(Normalisiere(tb.Groups[1].Value) ?? tb.Groups[1].Value) ?? null;

        if (wert.StartsWith("{Binding", StringComparison.Ordinal))
        {
            var pfad = Regex.Match(wert, @"^\{Binding\s+(?:Path=)?\(?([A-Za-z.]+)\)?");
            var ziel = pfad.Success ? Normalisiere(pfad.Groups[1].Value) : null;
            if (wert.Contains("TemplatedParent", StringComparison.Ordinal) && ziel is not null)
                return vorlagenBindung?.Invoke(ziel);
            if (wert.Contains("AncestorType", StringComparison.Ordinal) && ziel == eigenschaft && eigenschaft == "Foreground")
                return null; // Schrift des Vorfahren = vererbt
            return Unbekannt;
        }

        if (wert.StartsWith("{x:Static", StringComparison.Ordinal))
            return Unbekannt;

        return wert;
    }

    // ═══════════════════════════════════════════════════════════════════════════════════
    // Styles und Trigger
    // ═══════════════════════════════════════════════════════════════════════════════════

    private sealed class StilInfo
    {
        public StilInfo(string name) => Name = name;
        public string Name { get; }
        public Dictionary<string, string> Setter { get; } = new(StringComparer.Ordinal);
        public List<Ausloeser> Trigger { get; } = new();
        public XElement? Vorlage { get; set; }

        /// <summary>Style.Resources der BasedOn-Kette (Basis zuerst). Sie gelten fuer das gestylte
        /// Element und seinen Inhalt (zur Laufzeit belegt: ein impliziter TextBlock-Stil dort erreicht
        /// eigene TextBlocks im Knopfinhalt, auch ueber BasedOn).</summary>
        public List<Dictionary<string, XElement>> Ressourcen { get; } = new();

        public StilInfo Kopie()
        {
            var k = new StilInfo(Name) { Vorlage = Vorlage };
            foreach (var (p, v) in Setter)
                k.Setter[p] = v;
            k.Trigger.AddRange(Trigger);
            k.Ressourcen.AddRange(Ressourcen);
            return k;
        }
    }

    private sealed record SetterInfo(string? Ziel, string Eigenschaft, string Wert);

    private sealed record Ausloeser(IReadOnlyCollection<string> Bedingungen, IReadOnlyList<SetterInfo> Setter)
    {
        /// <summary>Eigene Bedingungen gegen den eigenen Zustand, "^"-Bedingungen (Bindung an den
        /// Vorfahren) gegen dessen Zustand.</summary>
        public bool GiltIn(IReadOnlyCollection<string> zustand, IReadOnlyCollection<string> vorfahr)
            => Bedingungen.All(b => b.StartsWith('^') ? vorfahr.Contains(b[1..]) : zustand.Contains(b));
    }

    private StilInfo BaueStil(XElement stil, List<Dictionary<string, XElement>> scope, int tiefe)
    {
        var info = new StilInfo(StilName(stil));
        if (tiefe > 20)
            return info;

        if (stil.Attribute("BasedOn")?.Value is { } basedOn && ReferenzSchluessel(basedOn) is { } baseKey
            && Finde(baseKey, scope, ausser: stil) is { } baseEl && baseEl.Name.LocalName == "Style")
        {
            var basis = BaueStil(baseEl, scope, tiefe + 1);
            foreach (var (p, v) in basis.Setter)
                info.Setter[p] = v;
            info.Trigger.AddRange(basis.Trigger);
            info.Vorlage = basis.Vorlage;
            info.Ressourcen.AddRange(basis.Ressourcen);
        }

        var eigeneRessourcen = stil.Elements().FirstOrDefault(x => x.Name.LocalName == "Style.Resources");
        if (eigeneRessourcen is not null)
            info.Ressourcen.Add(SammleWoerterbuch(eigeneRessourcen));

        var setterQuelle = stil.Elements().Where(x => x.Name.LocalName == "Setter")
            .Concat(stil.Elements().Where(x => x.Name.LocalName == "Style.Setters").SelectMany(x => x.Elements()));
        foreach (var setter in setterQuelle)
        {
            var roh = setter.Attribute("Property")?.Value;
            if (roh == "Template")
            {
                info.Vorlage = LiesVorlage(setter, scope) ?? info.Vorlage;
                continue;
            }

            var p = Normalisiere(roh);
            if (p is null || !FarbEigenschaften.Contains(p))
                continue;
            info.Setter[p] = SetterWert(setter);
        }

        info.Trigger.AddRange(LiesTrigger(stil, "Style.Triggers", scope));
        return info;
    }

    private XElement? LiesVorlage(XElement setter, List<Dictionary<string, XElement>> scope)
    {
        var inline = setter.Elements().FirstOrDefault(x => x.Name.LocalName == "Setter.Value")
            ?.Elements().FirstOrDefault(x => x.Name.LocalName == "ControlTemplate");
        if (inline is not null)
            return inline;

        return setter.Attribute("Value")?.Value is { } v && ReferenzSchluessel(v) is { } key
               && Finde(key, scope) is { Name.LocalName: "ControlTemplate" } ct
            ? ct
            : null;
    }

    private static List<Ausloeser> LiesTrigger(XElement besitzer, string triggerElementName, List<Dictionary<string, XElement>> scope)
    {
        var liste = new List<Ausloeser>();
        var block = besitzer.Elements().FirstOrDefault(x => x.Name.LocalName == triggerElementName);
        if (block is null)
            return liste;

        foreach (var t in block.Elements())
        {
            var bedingungen = new List<string>();
            switch (t.Name.LocalName)
            {
                case "Trigger":
                    bedingungen.Add($"{t.Attribute("Property")?.Value}={t.Attribute("Value")?.Value}");
                    break;
                case "DataTrigger":
                    bedingungen.Add(Bedingung(t.Attribute("Binding")?.Value, t.Attribute("Value")?.Value));
                    break;
                case "MultiTrigger" or "MultiDataTrigger":
                    foreach (var c in t.Descendants().Where(x => x.Name.LocalName == "Condition"))
                    {
                        bedingungen.Add(c.Attribute("Property") is { } cp
                            ? $"{cp.Value}={c.Attribute("Value")?.Value}"
                            : Bedingung(c.Attribute("Binding")?.Value, c.Attribute("Value")?.Value));
                    }
                    break;
                default:
                    continue; // EventTrigger u. Ae.: nur Animationen
            }

            var setter = new List<SetterInfo>();
            foreach (var s in t.Elements().Where(x => x.Name.LocalName == "Setter")
                         .Concat(t.Elements().Where(x => x.Name.LocalName.EndsWith(".Setters", StringComparison.Ordinal)).SelectMany(x => x.Elements())))
            {
                var p = Normalisiere(s.Attribute("Property")?.Value);
                if (p is null || !FarbEigenschaften.Contains(p))
                    continue;
                setter.Add(new SetterInfo(s.Attribute("TargetName")?.Value, p, SetterWert(s)));
            }

            if (setter.Count > 0)
                liste.Add(new Ausloeser(bedingungen, setter));
        }

        return liste;
    }

    /// <summary>Eine DataTrigger-Bedingung; eine Bindung an eine Eigenschaft des Vorfahren
    /// (RelativeSource AncestorType/TemplatedParent) wird zu "^Eigenschaft=Wert".</summary>
    private static string Bedingung(string? bindung, string? wert)
    {
        if (bindung is not null
            && (bindung.Contains("AncestorType", StringComparison.Ordinal) || bindung.Contains("TemplatedParent", StringComparison.Ordinal))
            && Regex.Match(bindung, @"^\{Binding\s+(?:Path=)?\(?([A-Za-z.]+)\)?\s*,") is { Success: true } m)
        {
            var eigenschaft = m.Groups[1].Value;
            eigenschaft = eigenschaft.Contains('.') ? eigenschaft[(eigenschaft.LastIndexOf('.') + 1)..] : eigenschaft;
            return $"^{eigenschaft}={wert}";
        }

        return $"Binding {bindung}={wert}";
    }

    /// <summary>Grundzustand plus je Trigger-Bedingungssatz ein Zustand; IsPressed=True schliesst
    /// IsMouseOver=True ein.</summary>
    private static IEnumerable<IReadOnlyCollection<string>> Zustaende(IEnumerable<Ausloeser> trigger)
    {
        var gesehen = new HashSet<string>(StringComparer.Ordinal);
        var basis = new HashSet<string>(StringComparer.Ordinal);
        gesehen.Add(string.Empty);
        yield return basis;

        foreach (var t in trigger)
        {
            var z = new HashSet<string>(t.Bedingungen.Where(b => !b.StartsWith('^')), StringComparer.Ordinal);
            if (z.Count == 0)
                continue; // nur vom Vorfahren abhaengig - kein eigener Zustand
            if (z.Contains("IsPressed=True"))
                z.Add("IsMouseOver=True");
            var schluessel = string.Join("|", z.OrderBy(x => x, StringComparer.Ordinal));
            if (gesehen.Add(schluessel))
                yield return z;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════════════
    // Ressourcen
    // ═══════════════════════════════════════════════════════════════════════════════════

    private StilInfo? FindeElementStil(XElement e, List<Dictionary<string, XElement>> scope)
    {
        var inline = e.Elements().FirstOrDefault(x => x.Name.LocalName == $"{e.Name.LocalName}.Style")
            ?.Elements().FirstOrDefault(x => x.Name.LocalName == "Style");
        if (inline is not null)
            return BaueStil(inline, scope, 0);

        if (e.Attribute("Style")?.Value is { } sAttr)
        {
            return ReferenzSchluessel(sAttr) is { } key && Finde(key, scope) is { Name.LocalName: "Style" } s
                ? BaueStil(s, scope, 0)
                : null; // gebundener/unbekannter Stil: kein impliziter Stil (explizit gesetzt)
        }

        return Finde("type:" + e.Name.LocalName, scope) is { Name.LocalName: "Style" } implizit
            ? BaueStil(implizit, scope, 0)
            : null;
    }

    /// <param name="ausser">Ein impliziter Stil mit BasedOn auf seinen eigenen Typ (B7-Muster) meint
    /// den naechst AEUSSEREN Stil - WPF loest BasedOn beim Laden auf, bevor der eigene Eintrag existiert.</param>
    private XElement? Finde(string key, List<Dictionary<string, XElement>> scope, XElement? ausser = null)
    {
        for (var i = scope.Count - 1; i >= 0; i--)
        {
            if (scope[i].TryGetValue(key, out var el) && !ReferenceEquals(el, ausser))
                return el;
        }

        foreach (var g in _global)
        {
            if (g.TryGetValue(key, out var el) && !ReferenceEquals(el, ausser))
                return el;
        }

        return null;
    }

    private List<Dictionary<string, XElement>> BaueScope(XElement element, List<Dictionary<string, XElement>> dateiScope)
    {
        var scope = new List<Dictionary<string, XElement>>(dateiScope);
        foreach (var vorfahr in element.Ancestors().Reverse())
        {
            if (vorfahr.Name.LocalName.EndsWith(".Resources", StringComparison.Ordinal)
                && vorfahr.Parent is not null && vorfahr.Parent.Name.LocalName != "ResourceDictionary")
            {
                scope.Add(SammleWoerterbuch(vorfahr));
            }
        }

        return scope;
    }

    private static Dictionary<string, XElement> SammleWoerterbuch(XElement container)
    {
        var d = new Dictionary<string, XElement>(StringComparer.Ordinal);
        IEnumerable<XElement> kinder = container.Elements();
        // <X.Resources><ResourceDictionary> ... </ResourceDictionary></X.Resources>
        kinder = kinder.SelectMany(k => k.Name.LocalName == "ResourceDictionary" && !ReferenceEquals(k, container) ? k.Elements() : new[] { k });

        foreach (var k in kinder)
        {
            var key = k.Attribute(XamlNs + "Key")?.Value;
            if (key is not null)
            {
                d[NormalisiereSchluessel(key)] = k;
                continue;
            }

            if (k.Name.LocalName == "Style" && KurzTyp(k.Attribute("TargetType")?.Value) is { } t)
                d["type:" + t] = k;
        }

        return d;
    }

    // ═══════════════════════════════════════════════════════════════════════════════════
    // Kleinkram
    // ═══════════════════════════════════════════════════════════════════════════════════

    private static IEnumerable<XElement> InhaltsElemente(XElement e)
        => e.Elements().Where(x => !x.Name.LocalName.Contains('.')
                                   && x.Name.LocalName is not ("Style" or "ControlTemplate" or "DataTemplate" or "ResourceDictionary"))
            .Concat(e.Elements().Where(x => x.Name.LocalName == $"{e.Name.LocalName}.Content").SelectMany(x => x.Elements()));

    private static string SetterWert(XElement setter)
    {
        if (setter.Attribute("Value")?.Value is { } v)
            return v;
        var pe = setter.Elements().FirstOrDefault(x => x.Name.LocalName == "Setter.Value");
        return pe is null ? Unbekannt : WertAusEigenschaftsElement(pe);
    }

    private static string WertAusEigenschaftsElement(XElement pe)
    {
        var kind = pe.Elements().FirstOrDefault();
        if (kind is null)
            return pe.Value.Trim();
        return kind.Name.LocalName switch
        {
            // Ein SolidColorBrush mit Color="{StaticResource ColorX}" ist ein Schnappschuss eines
            // rohen Farbschluessels - wird wie dieser Schluessel behandelt (nicht ueberlagerbar).
            "SolidColorBrush" => kind.Attribute("Color")?.Value ?? Unbekannt,
            "LinearGradientBrush" or "RadialGradientBrush" => "Verlauf",
            _ => Unbekannt,
        };
    }

    private static string? Normalisiere(string? eigenschaft)
    {
        if (string.IsNullOrEmpty(eigenschaft))
            return null;
        var p = eigenschaft.Trim('(', ')');
        var letzter = p.Contains('.') ? p[(p.LastIndexOf('.') + 1)..] : p;
        return letzter;
    }

    private static string? ReferenzSchluessel(string wert)
    {
        var m = Regex.Match(wert, @"^\{(?:Dynamic|Static)Resource\s+(?:ResourceKey=)?(.+?)\s*\}$");
        return m.Success ? NormalisiereSchluessel(m.Groups[1].Value.Trim()) : null;
    }

    private static string NormalisiereSchluessel(string key)
    {
        var t = Regex.Match(key, @"^\{x:Type\s+(?:\w+:)?(\w+)\s*\}$");
        return t.Success ? "type:" + t.Groups[1].Value : key;
    }

    private static string? KurzTyp(string? targetType)
    {
        if (string.IsNullOrEmpty(targetType))
            return null;
        var t = targetType.Replace("{x:Type", string.Empty).Trim().TrimEnd('}').Trim();
        return t.Contains(':') ? t[(t.IndexOf(':') + 1)..] : t;
    }

    private static string StilName(XElement stil)
        => stil.Attribute(XamlNs + "Key")?.Value
           ?? $"(implizit {KurzTyp(stil.Attribute("TargetType")?.Value) ?? "?"})";

    private static string? ElementName(XElement e)
        => e.Attribute(XamlNs + "Name")?.Value ?? e.Attribute("Name")?.Value;

    private static string Beschreibe(XElement e)
        => ElementName(e) is { } n ? $"{e.Name.LocalName}#{n}" : e.Name.LocalName;

    private static string Beschreibe(IReadOnlyCollection<string> zustand)
        => zustand.Count == 0 ? "Grundzustand" : string.Join(" + ", zustand.OrderBy(x => x, StringComparer.Ordinal));

    private static string Verbinde(string aussen, string innen)
        => aussen == "Grundzustand" ? innen : innen == "Grundzustand" ? aussen : $"{aussen} / {innen}";

    private static bool IstTransparent(string? wert)
        => wert is null || wert == "Transparent" || wert == "{x:Null}" || wert == "#00000000" || wert == "#00FFFFFF";

    private static bool Malt(string? wert) => wert is not null && !IstTransparent(wert);

    private static bool IstVerborgen(Dictionary<string, string> werte)
        => IstVerborgenWert(werte.GetValueOrDefault("Visibility"));

    private static bool IstVerborgenWert(string? wert) => wert is "Collapsed" or "Hidden";

}
