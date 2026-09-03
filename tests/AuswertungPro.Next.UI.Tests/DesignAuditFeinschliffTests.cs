using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Waechter aus dem Design-Audit vom 2026-09-03 (docs/DESIGN-AUDIT-2026-09-03.md, Q1-Q6).
/// Jeder Test liest die XAML-/Code-Dateien des UI-Projekts direkt von der Platte und
/// nennt im Fehlerfall jede Fundstelle mit Datei und Zeile.
/// </summary>
public sealed class DesignAuditFeinschliffTests
{
    private static readonly string UiRoot = RepoFile("src", "AuswertungPro.Next.UI");

    // Sichtbare Attribute: Beschriftung, Menuetext, Text, Tooltip, Fenstertitel.
    private static readonly Regex SichtbaresAttribut = new(
        "\\b(Content|Header|Text|ToolTip|Title)=\"([^\"]*)\"",
        RegexOptions.Compiled);

    // Ersatzschreibweisen, die im Deutschen praktisch nur als Umlaut-Ersatz vorkommen.
    // Bewusst NICHT enthalten: "ss" (Schweizer Schreibweise ist korrekt) und Woerter wie
    // "neue", "Steuer", "Bauer", "Quelle", in denen ae/oe/ue echte Buchstabenfolgen sind.
    private static readonly Regex UmlautErsatz = new(
        "oeffn|pruef|\\bfuer\\b|\\bueber|waehl|uebernehm|zurueck|aender|menue|naechst|drueck|" +
        "temporaer|bestaetig|rueckmeld|zugehoerig|\\bgruen|verknuepf|loesch|laenge|groesse|hoehe|" +
        "gefaell|schaecht|spaet|vorschlaeg|zusaetzl|verfuegbar|gueltig|moeglich|noetig|erfuellt|" +
        "waehrend|schluessel|ausfuehr|ergaenz|erklaer|uebersicht|ueberspring|ausgewaehlt|zaehl|" +
        "fuellen|buendel|rueckgaengig|ueberschreib|kuerzel|laeuft|staerke|wuensch|hoeher|groesser|" +
        "\\bkuerz|praefix|gebaeud|haeus|kanaele|strassenzuege|uebertrag|ueberpruef|ausloes|" +
        "loeschen|zuruecksetz|waehle|geoeffnet|ueblich|uebrig|aehnlich|erhoeh|gefuehrt|flaech|dafuer|wofuer",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    [Fact]
    public void Sichtbare_Texte_verwenden_echte_Umlaute()
    {
        var treffer = new List<string>();

        foreach (var datei in AlleXamlDateien())
        {
            var zeilen = File.ReadAllLines(datei);
            for (var i = 0; i < zeilen.Length; i++)
            {
                foreach (Match m in SichtbaresAttribut.Matches(zeilen[i]))
                {
                    var wert = SichtbarerAnteil(m.Groups[2].Value);
                    if (wert is null)
                        continue;

                    if (UmlautErsatz.IsMatch(wert))
                        treffer.Add($"{Relativ(datei)}:{i + 1}: {m.Groups[1].Value}=\"{wert}\"");
                }
            }
        }

        Assert.True(
            treffer.Count == 0,
            "Sichtbare Texte schreiben Umlaute als ae/oe/ue. Die Konvention gilt nur fuer den Quellcode, nicht fuer das, was der Nutzer liest:\n"
            + string.Join("\n", treffer));
    }

    [Fact]
    public void Menuepunkte_haben_ein_Symbol_oder_sind_checkbar()
    {
        var treffer = new List<string>();
        var menuePunkt = new Regex("<MenuItem(?=[\\s/>])(.*?)(/?)>", RegexOptions.Compiled | RegexOptions.Singleline);
        var naechstesElement = new Regex("<MenuItem\\.Icon>|</MenuItem>|<MenuItem(?=[\\s/>])|<Separator", RegexOptions.Compiled);

        foreach (var datei in AlleXamlDateien())
        {
            var text = File.ReadAllText(datei);
            foreach (Match m in menuePunkt.Matches(text))
            {
                var attribute = m.Groups[1].Value;
                if (attribute.Contains("IsCheckable=\"True\"", StringComparison.Ordinal))
                    continue; // Der Haken belegt den Icon-Platz (Icon-Leitbild vom 14.07.).
                if (attribute.Contains("Icon=", StringComparison.Ordinal))
                    continue;

                var header = Regex.Match(attribute, "Header=\"([^\"]*)\"");
                if (!header.Success)
                    continue; // Eigener Header-Inhalt (z. B. Schieberegler oder Icon+Text) — nicht Gegenstand dieser Regel.
                if (header.Groups[1].Value.StartsWith('_') || header.Groups[1].Value.StartsWith('{'))
                    continue; // Menueleisten-Kopf (Datei/Werkzeuge/Ansicht) oder gebundener Text.

                if (m.Groups[2].Value != "/")
                {
                    var weiter = naechstesElement.Match(text, m.Index + m.Length);
                    if (weiter.Success && weiter.Value == "<MenuItem.Icon>")
                        continue;
                }

                var zeile = text[..m.Index].Count(c => c == '\n') + 1;
                treffer.Add($"{Relativ(datei)}:{zeile}: {header.Groups[1].Value}");
            }
        }

        Assert.True(
            treffer.Count == 0,
            "Menuepunkte ohne Fluent-Symbol (gleiche Aktion = gleiches Glyph, Referenz: docs/UI-SYMBOLE-EFFEKTE-PLAN-2026-07-14.md):\n"
            + string.Join("\n", treffer));
    }

    [Fact]
    public void Bedienelemente_verwenden_Fluent_Glyphen_statt_Textsymbolen()
    {
        // Geometrische Formen, Sonderzeichen/Dingbats/Drehpfeile, Emoji und die gebogenen Undo-/Redo-Pfeile.
        // Gerade Pfeile (← → ↑ ↓) bleiben erlaubt: Sie stehen fuer Tasten oder Richtungen im Fliesstext.
        var textsymbol = new Regex("[\\u25A0-\\u25FF\\u2600-\\u27FF\\u21B6\\u21B7]|[\\uD83C-\\uDBFF][\\uDC00-\\uDFFF]", RegexOptions.Compiled);
        var bedienAttribut = new Regex("\\b(Content|Header|Text)=\"([^\"]*)\"", RegexOptions.Compiled);
        var treffer = new List<string>();

        foreach (var datei in AlleXamlDateien())
        {
            var zeilen = File.ReadAllLines(datei);
            for (var i = 0; i < zeilen.Length; i++)
            {
                foreach (Match m in bedienAttribut.Matches(zeilen[i]))
                {
                    var wert = SichtbarerAnteil(m.Groups[2].Value);
                    if (wert is not null && textsymbol.IsMatch(wert))
                        treffer.Add($"{Relativ(datei)}:{i + 1}: {m.Groups[1].Value}=\"{wert}\"");
                }
            }
        }

        // Im Code erzeugte Bedienelemente: nur Zeichenketten-Literale, keine Kommentare.
        // Ausnahmen mit Grund: DataPageConverters liefert ein Tabellen-Haekchen als Zellinhalt;
        // ShellViewModel setzt den ueblichen Windows-Punkt fuer "ungespeichert" in den Fenstertitel.
        var ausnahmen = new[] { "DataPageConverters.cs", "ShellViewModel.cs" };
        var literal = new Regex("\"[^\"\\n]*\"", RegexOptions.Compiled);
        foreach (var datei in Directory.EnumerateFiles(UiRoot, "*.cs", SearchOption.AllDirectories))
        {
            if (IstBuildAusgabe(datei) || ausnahmen.Contains(Path.GetFileName(datei)))
                continue;

            var zeilen = File.ReadAllLines(datei);
            for (var i = 0; i < zeilen.Length; i++)
            {
                var code = zeilen[i];
                var kommentar = code.IndexOf("//", StringComparison.Ordinal);
                if (kommentar >= 0)
                    code = code[..kommentar];

                foreach (Match m in literal.Matches(code))
                {
                    if (textsymbol.IsMatch(m.Value))
                        treffer.Add($"{Relativ(datei)}:{i + 1}: {m.Value}");
                }
            }
        }

        Assert.True(
            treffer.Count == 0,
            "Textsymbole/Emoji als Bedienelement — bitte ui:FluentIcon bzw. FluentIcon-Glyph verwenden:\n"
            + string.Join("\n", treffer));
    }

    [Fact]
    public void Alle_Fenster_treten_sanft_auf()
    {
        // Hauptfenster (Splash uebergibt, startet maximiert), Video- und Startfenster bleiben aussen vor —
        // die drei letzten werden in DesignAuditThemeResourceTests ausdruecklich ohne Eintritt gehalten.
        var ausnahmen = new[] { "MainWindow.xaml", "PlayerWindow.xaml", "LiveFrameWindow.xaml", "StartupSplashWindow.xaml" };
        var treffer = new List<string>();

        foreach (var datei in AlleXamlDateien())
        {
            if (ausnahmen.Contains(Path.GetFileName(datei)))
                continue;

            var xaml = File.ReadAllText(datei);
            if (!xaml.Contains("<Window ", StringComparison.Ordinal) && !xaml.Contains("<Window\n", StringComparison.Ordinal) && !xaml.Contains("<Window\r\n", StringComparison.Ordinal))
                continue;

            var hatEintritt = xaml.Contains("ui:WindowFx.Entrance=\"True\"", StringComparison.Ordinal);
            var hatNamespace = xaml.Contains("xmlns:ui=\"clr-namespace:AuswertungPro.Next.UI\"", StringComparison.Ordinal);
            if (!hatEintritt || !hatNamespace)
                treffer.Add(Relativ(datei));
        }

        Assert.True(
            treffer.Count == 0,
            "Fenster ohne ui:WindowFx.Entrance=\"True\" (mit xmlns:ui). Entweder alle Fenster treten auf oder keines:\n"
            + string.Join("\n", treffer));
    }

    [Fact]
    public void Tastaturfokus_ist_in_allen_Bedienstilen_sichtbar()
    {
        // Diese impliziten Stile in Controls.xaml erhalten Tastaturfokus, zeigten ihn aber nicht.
        string[] stile = ["CheckBox", "RadioButton", "ComboBox", "Expander", "TreeViewItem", "TabItem", "Slider", "GridViewColumnHeader"];
        var controls = File.ReadAllText(Path.Combine(UiRoot, "Theme", "Controls.xaml"));
        var fehlend = new List<string>();

        foreach (var stil in stile)
        {
            var start = controls.IndexOf($"<Style TargetType=\"{{x:Type {stil}}}\"", StringComparison.Ordinal);
            Assert.True(start >= 0, $"Impliziter Stil fuer {stil} nicht gefunden.");

            // Nur die Setter auf oberster Ebene des Stils zaehlen (bis zum ersten Template/Trigger-Block).
            var ende = controls.IndexOf("<Setter Property=\"Template\">", start, StringComparison.Ordinal);
            if (ende < 0)
                ende = controls.IndexOf("</Style>", start, StringComparison.Ordinal);
            var kopf = controls[start..ende];

            if (!kopf.Contains("<Setter Property=\"FocusVisualStyle\" Value=\"{DynamicResource KeyboardFocusVisual}\"/>", StringComparison.Ordinal))
                fehlend.Add(stil);
        }

        Assert.True(fehlend.Count == 0, "Ohne KeyboardFocusVisual: " + string.Join(", ", fehlend));
    }

    [Fact]
    public void Feste_Farben_gibt_es_nur_in_Video_Fenstern()
    {
        // Diese Dateien zeichnen ueber oder neben laufendem Video und bleiben bewusst dunkel.
        var videoDateien = new[]
        {
            "PlayerWindow.xaml", "PlayerCodingSidePanel.xaml", "LiveFrameWindow.xaml",
            "PhotoMeasurementWindow.xaml", "StartupSplashWindow.xaml", "PipeGraphTimeline.xaml"
        };
        var festeFarbe = new Regex("\\b(Background|Foreground|BorderBrush|Fill|Stroke)=\"#[0-9A-Fa-f]{6,8}\"", RegexOptions.Compiled);
        var treffer = new List<string>();

        foreach (var datei in AlleXamlDateien())
        {
            if (videoDateien.Contains(Path.GetFileName(datei)))
                continue;

            var zeilen = File.ReadAllLines(datei);
            for (var i = 0; i < zeilen.Length; i++)
            {
                foreach (Match m in festeFarbe.Matches(zeilen[i]))
                    treffer.Add($"{Relativ(datei)}:{i + 1}: {m.Value}");
            }
        }

        Assert.True(
            treffer.Count == 0,
            "Feste Farben ausserhalb der Video-Fenster brechen im Dunkel-Design — bitte DynamicResource verwenden:\n"
            + string.Join("\n", treffer));
    }

    [Fact]
    public void Player_Abdunkelungen_kommen_aus_gemeinsamen_Video_Tokens()
    {
        var controls = File.ReadAllText(Path.Combine(UiRoot, "Theme", "Controls.xaml"));
        foreach (var token in new[] { "VideoBackgroundBrush", "VideoScrimBrush", "VideoScrimStrongBrush", "VideoScrimSoftBrush", "VideoScrimBlackBrush", "VideoScrimBlackStrongBrush", "VideoScrimBlackSoftBrush" })
            Assert.Contains($"x:Key=\"{token}\"", controls);

        var player = File.ReadAllText(Path.Combine(UiRoot, "Views", "Windows", "PlayerWindow.xaml"));
        foreach (var wert in new[] { "#DD111318", "#EE111318", "#CC111318", "#CC000000", "#DD000000", "#B8000000", "#FF000000" })
            Assert.DoesNotContain($"Background=\"{wert}\"", player);
    }

    /// <summary>Literal bleibt; bei Bindungen zaehlt nur der sichtbare StringFormat-Text; sonst nichts.</summary>
    private static string? SichtbarerAnteil(string wert)
    {
        if (!wert.StartsWith('{'))
            return wert;

        var format = Regex.Match(wert, "StringFormat=(?:\\{\\})?([^,}]*)");
        return format.Success ? format.Groups[1].Value : null;
    }

    private static IEnumerable<string> AlleXamlDateien()
        => Directory.EnumerateFiles(UiRoot, "*.xaml", SearchOption.AllDirectories).Where(d => !IstBuildAusgabe(d));

    private static bool IstBuildAusgabe(string pfad)
        => pfad.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
        || pfad.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);

    private static string Relativ(string pfad) => Path.GetRelativePath(UiRoot, pfad);
}
