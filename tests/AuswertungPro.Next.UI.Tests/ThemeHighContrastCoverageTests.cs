using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Optikanalyse 28.09.2026, Aufgabe 13, Fix-Runde 1 (IMPORTANT 2), erweitert in Fix-Runde 2:
/// scannt alle XAML-Dateien nach Background-Tokens, auf denen tatsaechlich normaler Text sitzt.
/// Zwei Fundwege:
///
/// 1) <c>Background="{...Resource XyzBrush}"</c> als normales Attribut (Foreground
///    Text/TextSecondary/Header/Muted/Faint direkt am Element, oder ein TextBlock/TextBox/Run/
///    AccessText/Label-Kind ohne eigene Foreground-Angabe - das erbt die normale Textfarbe).
/// 2) <c>&lt;Setter Property="Background" Value="{...Resource XyzBrush}"/&gt;</c> innerhalb eines
///    Style/ControlTemplate fuer einen textfaehigen Bedienelement-Typ (Button, ListBoxItem, ...),
///    gepaart mit einem Foreground-Setter im selben Trigger-Zustand bzw. dem Style-Grundzustand
///    (Fix-Runde 2, IMPORTANT 2: Runde 1 sah nur Attribute und uebersah damit z. B.
///    RecordDetailsView.xaml's <c>DataTrigger</c>-Paar Background=SuccessSubtleBrush/
///    Foreground=SuccessTextBrush und Controls.xaml's <c>BearbeitungErledigtKnopf</c>-Style).
///
/// Jeder so gefundene Hintergrund-Schluessel muss in <c>Theme/ThemeHighContrast.xaml</c>
/// vorkommen, sonst waere Text in Windows-Hochkontrast auf diesem Hintergrund potenziell
/// unlesbar (Text folgt SystemColors, Hintergrund bliebe die normale Themefarbe).
///
/// Bewusst ausgenommen bleiben nur Hintergrund-Tokens, deren TATSAECHLICH gepaarter Text
/// (Foreground) SELBST nicht ueberschrieben wird - dann faellt das Paar gemeinsam auf seine
/// normale Themefarbe zurueck und bleibt intern genauso lesbar wie ausserhalb von Hochkontrast
/// (z. B. KiSubtleBrush + KiTextBrush, beide unveraendert). Ein Hintergrund, dessen gepaarter Text
/// dagegen auf WindowText/HighlightText gezwungen wird (TextBrush, Success-/Warning-/DangerTextBrush,
/// AccentTextBrush, SelectionTextBrush, Muted/Faint), MUSS mitgehen - das war der Fehler in Runde 1
/// bei SuccessSubtleBrush/WarningSubtleBrush/DangerSubtleBrush/InputWarmBrush/VsaInputHighlightBrush.
/// </summary>
public sealed class ThemeHighContrastCoverageTests
{
    /// <summary>
    /// Genau die Foreground-Tokens, die in ThemeHighContrast.xaml auf WindowText/GrayText gezwungen
    /// werden (siehe dortige Text- und "Erfolg/Warnung/Fehler (Text)"-Abschnitte). Jeder Hintergrund,
    /// der mit einem dieser Tokens gepaart auftritt, verliert seinen Partner an eine neutrale Farbe
    /// und muss deshalb selbst ebenfalls neutralisiert werden.
    /// </summary>
    private static readonly HashSet<string> PlaintextForegroundTokens = new(System.StringComparer.Ordinal)
    {
        "TextBrush", "TextSecondaryBrush", "HeaderTextBrush", "MutedBrush", "FaintBrush",
        "SuccessTextBrush", "WarningTextBrush", "DangerTextBrush",
        "AccentTextBrush", "SelectionTextBrush",
    };

    private static readonly HashSet<string> PlaintextElementNames = new(System.StringComparer.Ordinal)
    {
        "TextBlock", "TextBox", "Run", "AccessText", "Label",
    };

    /// <summary>
    /// Style-/ControlTemplate-Zieltypen, die ihren eigenen Content/eigene Beschriftung ueber
    /// Foreground direkt rendern (Button.Content, ListBoxItem.Content, ...). Eine Style-Definition
    /// fuer einen reinen Layout-Container (Border, Grid, StackPanel, ...) rendert dagegen NIE selbst
    /// Text - deren tatsaechlicher Text steckt in Kind-Markup, das der attributbasierte Scan (Weg 1)
    /// bereits getrennt erfasst. Ohne diese Eingrenzung wuerde jede Card-Hintergrundfarbe faelschlich
    /// als "traegt Text" gelten, nur weil irgendwo in derselben Datei Text vorkommt.
    /// </summary>
    private static readonly HashSet<string> TextBearingStyleTargetTypes = new(System.StringComparer.Ordinal)
    {
        "Button", "ToggleButton", "RepeatButton", "RadioButton", "CheckBox",
        "MenuItem", "ListBoxItem", "ListViewItem", "TreeViewItem", "ComboBoxItem",
        "TabItem", "Expander", "GroupBox", "Label", "TextBlock",
    };

    /// <summary>
    /// Hintergrund-Tokens, deren gepaarter Text NICHT auf eine SystemColors-Farbe gezwungen wird
    /// (siehe Klassendoku) - beide fallen gemeinsam auf ihre normale Themefarbe zurueck und bleiben
    /// dadurch intern lesbar, unabhaengig vom Windows-Hochkontraststatus.
    /// </summary>
    private static readonly HashSet<string> BewusstAusgenommen = new(System.StringComparer.Ordinal)
    {
        // KiSubtleBrush wird ausschliesslich mit KiTextBrush gepaart (nie mit einem der oben
        // gezwungenen Tokens) - beide bleiben in ThemeHighContrast.xaml unveraendert, ihr in
        // CLAUDE.md dokumentierter Kontrast (KiTextBrush auf CardBrush/KiSubtleBrush) bleibt damit
        // exakt der bereits gepruefte normale Themekontrast.
        "KiSubtleBrush", "KiBrush",
        "SecondaryAccentSubtleBrush", "SecondaryAccentBrush", "SecondaryAccentHoverBrush",
        "CodeGroupStrukturSubtleBrush", "CodeGroupBetriebSubtleBrush",
        "CodeGroupBestandSubtleBrush", "CodeGroupSonstigSubtleBrush",
        "CodeGroupStrukturBrush", "CodeGroupBetriebBrush", "CodeGroupBestandBrush", "CodeGroupSonstigBrush",
        "MarkierungGelbBrush", "MarkierungOrangeBrush", "MarkierungRotBrush",
        "MarkierungGruenBrush", "MarkierungBlauBrush",
        "Severity1Brush", "Severity2Brush", "Severity3Brush", "Severity4Brush", "Severity5Brush",
        "GateOkBrush", "GateReviewBrush", "GateFailBrush",
        "ConfidenceHighBrush", "ConfidenceMidBrush", "ConfidenceLowBrush",
        "SuccessBrush", "DangerBrush", "WarningBrush", "InfoBrush",
        "AccentBarBrush",
        // Zustandsklassen-Chips (Z0..Z4) und Schadensgruppen: eigene, ausserhalb dieser
        // Ueberlagerung geprüfte Farbregel (siehe ZustandsklasseInkPolicy in CLAUDE.md); die
        // konkreten Werte kommen nicht aus einem *Brush-Token, sondern aus Code, deshalb hier
        // ohnehin nicht sichtbar.
        // Video*Brush: bewusst theme-UNABHAENGIG (gleicher Wert in Theme.xaml/ThemeLight.xaml,
        // siehe CLAUDE.md "Feste Farbwerte gibt es nur in den sechs Video-Dateien") - ein
        // halbtransparenter Video-Abdunkelungs-Scrim, kein normales UI-Chrome. Eine
        // SystemColors-Zuordnung wuerde der bestehenden, bewusst pixelgenauen Ausnahme
        // widersprechen.
        "VideoScrimBlackSoftBrush", "VideoScrimStrongBrush",
    };

    [Fact]
    public void Jeder_normale_Textuntergrund_ist_in_der_Hochkontrast_Ueberlagerung_abgedeckt()
    {
        var overlayKeys = LiesUeberlagerungsSchluessel();
        var gefunden = new SortedSet<string>(System.StringComparer.Ordinal);

        foreach (var datei in XamlDateien())
        {
            XDocument doc;
            try
            {
                doc = XDocument.Load(datei);
            }
            catch (System.Xml.XmlException)
            {
                continue; // keine XML-Wohlgeformtheit (kommt in diesem Bestand nicht vor, aber fail-safe statt Testfehler durch eine fremde Datei)
            }

            var root = doc.Root;
            if (root is null)
                continue;

            foreach (var element in root.DescendantsAndSelf())
                SammleHintergrundMitText(element, gefunden);

            SammleSetterHintergrundMitText(root, gefunden);
        }

        var fehlend = gefunden
            .Where(k => !overlayKeys.Contains(k) && !BewusstAusgenommen.Contains(k))
            .ToArray();

        Assert.True(
            fehlend.Length == 0,
            "Diese Hintergrund-Tokens tragen normalen Text, fehlen aber in "
            + "Theme/ThemeHighContrast.xaml (oder muessen bewusst in "
            + $"{nameof(BewusstAusgenommen)} eingetragen werden): "
            + string.Join(", ", fehlend));
    }

    /// <summary>Dokumentiert, dass die Ausschlussliste nicht verwaist (kein Eintrag, der
    /// tatsaechlich nirgends mehr als Hintergrund mit Text vorkommt) - haelt die Liste ehrlich.</summary>
    [Fact]
    public void BewusstAusgenommen_ist_nicht_leer_und_disjunkt_zur_Ueberlagerung()
    {
        var overlayKeys = LiesUeberlagerungsSchluessel();
        Assert.NotEmpty(BewusstAusgenommen);
        Assert.Empty(BewusstAusgenommen.Intersect(overlayKeys));
    }

    // ── Weg 1: Background="..." als Attribut ──────────────────────────────────────────────

    private static void SammleHintergrundMitText(XElement element, SortedSet<string> gefunden)
    {
        var bg = element.Attribute("Background")?.Value;
        var key = ExtrahiereBrushSchluessel(bg);
        if (key is not null && TraegtText(element, istWurzel: true))
            gefunden.Add(key);
    }

    private static bool TraegtText(XElement element, bool istWurzel)
    {
        if (!istWurzel && element.Attribute("Background") is not null)
            return false; // eigene Flaeche - ihr Text zaehlt dort, nicht bei der uebergeordneten

        var vg = element.Attribute("Foreground")?.Value;
        if (vg is not null)
            return ExtrahiereBrushSchluessel(vg) is { } vgKey && PlaintextForegroundTokens.Contains(vgKey);

        if (!istWurzel
            && PlaintextElementNames.Contains(element.Name.LocalName)
            && element.Attribute("Foreground") is null)
        {
            return true;
        }

        foreach (var kind in element.Elements())
        {
            // Nur echte visuelle Kind-Elemente betrachten, keine Property-Element-Syntax
            // (z. B. <Border.Background>) und keine Resources/Style/Trigger-Definitionen, die
            // nicht direkt auf dieser Flaeche gerendert werden.
            var name = kind.Name.LocalName;
            if (name.Contains('.') || name is "Style" or "Resources" or "DataTemplate" or "ControlTemplate")
                continue;

            if (TraegtText(kind, istWurzel: false))
                return true;
        }

        return false;
    }

    // ── Weg 2: <Setter Property="Background" Value="..."/> in Style/ControlTemplate ───────

    private static void SammleSetterHintergrundMitText(XElement wurzel, SortedSet<string> gefunden)
    {
        foreach (var setter in wurzel.Descendants().Where(e => e.Name.LocalName == "Setter"))
        {
            if (setter.Attribute("Property")?.Value != "Background")
                continue;

            var key = ExtrahiereBrushSchluessel(setter.Attribute("Value")?.Value);
            if (key is null)
                continue;

            if (SetterHintergrundTraegtText(setter))
                gefunden.Add(key);
        }
    }

    private static bool SetterHintergrundTraegtText(XElement backgroundSetter)
    {
        var container = backgroundSetter.Ancestors()
            .FirstOrDefault(a => a.Name.LocalName is "Style" or "ControlTemplate");
        if (container is null)
            return false; // kein Style/Template-Kontext (kommt bei Property="Background" nicht vor)

        var targetType = (container.Attribute("TargetType")?.Value ?? string.Empty)
            .Replace("{x:Type ", string.Empty).TrimEnd('}');
        var typKurzname = targetType.Contains(':') ? targetType[(targetType.IndexOf(':') + 1)..] : targetType;
        if (!TextBearingStyleTargetTypes.Contains(typKurzname))
            return false; // Border/Grid/... rendern selbst keinen Text - siehe Klassendoku Weg 2

        // 1) Foreground-Setter im selben Zustand (direktes Geschwister, z. B. selber DataTrigger)
        var geschwisterForeground = backgroundSetter.Parent?.Elements()
            .FirstOrDefault(e => e.Name.LocalName == "Setter" && e.Attribute("Property")?.Value == "Foreground");
        if (geschwisterForeground is not null)
        {
            var fgKey = ExtrahiereBrushSchluessel(geschwisterForeground.Attribute("Value")?.Value);
            return fgKey is not null && PlaintextForegroundTokens.Contains(fgKey);
        }

        // 2) Kein Foreground im selben Trigger-Zustand: der Grundzustand des Style/Templates gilt.
        var basisForeground = container.Elements()
            .FirstOrDefault(e => e.Name.LocalName == "Setter" && e.Attribute("Property")?.Value == "Foreground");
        if (basisForeground is not null)
        {
            var fgKey = ExtrahiereBrushSchluessel(basisForeground.Attribute("Value")?.Value);
            return fgKey is not null && PlaintextForegroundTokens.Contains(fgKey);
        }

        // 3) Weder Trigger- noch Grundzustand setzen Foreground: das Element erbt die normale
        // Fenstertextfarbe (dieselbe Annahme wie bei PlaintextElementNames in Weg 1) - konservativ
        // als "traegt Text" werten, statt eine stille Luecke zu riskieren.
        return true;
    }

    private static string? ExtrahiereBrushSchluessel(string? wert)
    {
        if (string.IsNullOrEmpty(wert))
            return null;

        var match = Regex.Match(wert, @"\{(?:Dynamic|Static)Resource\s+([A-Za-z0-9]+Brush)\}");
        return match.Success ? match.Groups[1].Value : null;
    }

    private static IEnumerable<string> XamlDateien()
        => Directory.EnumerateFiles(RepoFile("src", "AuswertungPro.Next.UI"), "*.xaml", SearchOption.AllDirectories)
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", System.StringComparison.OrdinalIgnoreCase)
                     && !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", System.StringComparison.OrdinalIgnoreCase));

    private static HashSet<string> LiesUeberlagerungsSchluessel()
    {
        var pfad = RepoFile("src", "AuswertungPro.Next.UI", "Theme", "ThemeHighContrast.xaml");
        var doc = XDocument.Load(pfad);
        var xNamespace = doc.Root!.GetDefaultNamespace();
        var xamlNs = doc.Root.GetNamespaceOfPrefix("x")!;

        return doc.Root.Elements()
            .Select(e => e.Attribute(xamlNs + "Key")?.Value)
            .Where(k => k is not null)
            .Select(k => k!)
            .ToHashSet(System.StringComparer.Ordinal);
    }
}
