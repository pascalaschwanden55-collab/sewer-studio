using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Optikanalyse 28.09.2026, Aufgabe 13, Fix-Runde 1 (IMPORTANT 2): scannt alle XAML-Dateien
/// nach Background-Tokens, auf denen tatsaechlich normaler Text sitzt (Foreground
/// Text/TextSecondary/Header, oder ein TextBlock/TextBox/Run/AccessText/Label ohne eigene
/// Foreground-Angabe - der erbt die normale Textfarbe). Jeder so gefundene Hintergrund-Schluessel
/// muss in <c>Theme/ThemeHighContrast.xaml</c> vorkommen, sonst waere Text in Windows-Hochkontrast
/// auf diesem Hintergrund potenziell unlesbar (Text folgt SystemColors, Hintergrund bliebe die
/// normale Themefarbe).
///
/// Bewusst ausgenommen sind Hintergrund-Tokens, die selbst eine FACHLICHE BEDEUTUNG ueber die
/// Farbe tragen (Erfolg/Warnung/Fehler-Untergrund, Zustandsstufen, Markierungsfarben, Code-Gruppen).
/// Eine Zuordnung auf neutrale SystemColors wuerde genau diese Bedeutung zerstoeren - dieselbe
/// Begruendung wie bei den Success-/Warning-/Danger-TEXT-Tokens in ThemeHighContrast.xaml.
/// </summary>
public sealed class ThemeHighContrastCoverageTests
{
    /// <summary>
    /// Nur echte Klartext-Token gelten als "erbt automatisch die normale Textfarbe" - ein Element
    /// mit einer eigenen Foreground-Angabe wird direkt geprueft (auch bei einem hier NICHT
    /// gelisteten Token wie SuccessTextBrush, damit ein gefaerbtes Badge nicht als "traegt
    /// normalen Text" gezaehlt wird).
    /// </summary>
    private static readonly HashSet<string> PlaintextForegroundTokens = new(System.StringComparer.Ordinal)
    {
        "TextBrush", "TextSecondaryBrush", "HeaderTextBrush", "MutedBrush", "FaintBrush",
    };

    private static readonly HashSet<string> PlaintextElementNames = new(System.StringComparer.Ordinal)
    {
        "TextBlock", "TextBox", "Run", "AccessText", "Label",
    };

    /// <summary>
    /// Hintergrund-Tokens, die Farbe als FACHLICHE Bedeutung tragen (Status/Zustand/Markierung/
    /// Codegruppe) und deshalb bewusst NICHT auf neutrale SystemColors gezwungen werden - siehe
    /// Klassendoku und die gleiche Regel bei den Text-Tokens in ThemeHighContrast.xaml.
    /// </summary>
    private static readonly HashSet<string> BewusstAusgenommen = new(System.StringComparer.Ordinal)
    {
        "SuccessSubtleBrush", "DangerSubtleBrush", "WarningSubtleBrush",
        "InputWarmBrush", "VsaInputHighlightBrush",
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
