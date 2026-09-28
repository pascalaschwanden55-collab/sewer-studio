using System.IO;
using System.Text.RegularExpressions;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Waechter zur Optikanalyse 28.09.2026, Aufgabe 4 («Fensterregel - uebrige Fenster + Waechter»):
/// prueft die Knopfregel (Plan-Abschnitt «Global Constraints», Punkt 5) in JEDEM Fenster-XAML des
/// UI-Projekts - Views/*.xaml (nur die oberste Ebene), Dialogs/*.xaml und Views/Windows/*.xaml -
/// ausser den namentlich begruendeten Ausnahmen unten. Vier Regeln je Fenster:
/// (a) hoechstens EIN <c>PrimaryButton</c>,
/// (b) traegt ein Fenster ein <c>IsDefault="True"</c>, steht unmittelbar davor (gleiche
///     Dokumentreihenfolge, kein anderer Button dazwischen) ein Knopf mit <c>IsCancel="True"</c>,
/// (c) kein lokal definierter <c>Style x:Key="..." TargetType="Button"</c> mehr im Fenster,
/// (d) kein <c>Background=</c> direkt an einem <c>&lt;Button</c>-Tag.
/// Dieser Waechter ersetzt NICHT die engeren Aufgabe-3-Waechter (<c>DesignAuditDossierFensterTests</c>)
/// oder das lookless <c>NovaDialogHeader</c>/<c>DialogButtonBar</c>-Theme selbst - er ist der
/// umfassende Nachfolger, der ALLE Fenster erfasst.
/// </summary>
public sealed class DesignAuditKnopfleistenTests
{
    private static readonly string UiRoot = RepoFile("src", "AuswertungPro.Next.UI");

    /// <summary>
    /// Namentliche Ausnahmen mit Begruendung. Eine Ausnahme nimmt eine Datei komplett aus allen
    /// vier Pruefungen heraus - nie stillschweigend, immer mit Grund hier dokumentiert.
    /// </summary>
    private static readonly (string Datei, string Grund)[] Ausnahmen =
    [
        ("PlayerWindow.xaml",
            "Video-Fenster mit eigenem Vollbild-Bedienkonzept (Zeitleiste/Playback); laut Aufgabe 4 ausdruecklich ausgenommen."),
        ("LiveFrameWindow.xaml",
            "Video-Fenster (Live-Ring-Overlay); wie PlayerWindow ausgenommen."),
        ("StartupSplashWindow.xaml",
            "Startanimation, kein Dialogfenster; laut Aufgabe 4 ausdruecklich ausgenommen."),
        ("PhotoMeasurementWindow.xaml",
            "Video-/Messfenster mit eigenem Werkzeugkasten (Foto-Overlay-Messwerkzeuge); laut Aufgabe 4 ausdruecklich ausgenommen."),
        ("WebGisVorschauWindow.xaml",
            "Geschuetzte WebGIS-Datei (Global Constraints Punkt 2) - nicht Teil dieses Auftrags, wird spaeter angepasst."),
        ("WebGisSchreibBestaetigungWindow.xaml",
            "Geschuetzte WebGIS-Datei (Global Constraints Punkt 2) - nicht Teil dieses Auftrags, wird spaeter angepasst."),
        ("WebGisHolenWindow.xaml",
            "Geschuetzte WebGIS-Datei (Global Constraints Punkt 2) - nicht Teil dieses Auftrags, wird spaeter angepasst."),
        ("NovaDialogWindow.xaml",
            "Knoepfe werden dynamisch im Code gebaut (Aufgabe 1): ConfirmCancel stellt Abbrechen bewusst ganz links, " +
            "ConfirmWarn(defaultNo) hat bewusst keinen PrimaryButton - beides gehoert zum Design, nicht zur Regelverletzung."),
        ("ObjektakteWindow.xaml",
            "Reiner Host von ObjektakteView ohne eigenen Kopf/Fuss (Entscheid Aufgabe 4a) - Umbau liegt ausserhalb des Auftrags."),
        ("FloatingGridWindow.xaml",
            "Der Andocken-Knopf braucht DynamicResource statt StaticResource, weil WindowOpenCloseSmokeTests dieses Fenster " +
            "ohne laufende Application instanziiert (Entscheid Aufgabe 4a) - keine weiteren Knoepfe betroffen."),
        ("TrainingStudioWindow.xaml",
            "Eigene mehrspaltige Arbeitsflaeche mit mehreren gleichzeitig sichtbaren, je Schritt hervorgehobenen Aktionen " +
            "(Severity-Farbknoepfe 1-5, Schritt-Buttons je Spalte); nur Kopf/Schliessen wurden auf NovaDialogHeader " +
            "umgestellt, das Innenleben bleibt laut Aufgabe 4 bewusst unveraendert."),
        ("TrainingCenterWindow.xaml",
            "Eigene Werkzeugleiste mit farbig bedeutungstragenden Aktionsknoepfen (Selbsttraining starten/pausieren/" +
            "abbrechen in Erfolgs-/Warn-/Gefahrfarbe); nur der Kopf wurde ergaenzt (NovaDialogHeader in derselben Karte, " +
            "damit keine bestehende Grid.Row-Zuordnung verschoben werden musste), die Arbeitsflaeche bleibt unveraendert."),
        ("VideoAnalysisPipelineWindow.xaml",
            "Eigene, bewusst gestaltete Kopfzeile (Sci-Fi-Branding mit NeuralSphere/Akzentbalken) samt Andocken-Knopf " +
            "im Content-Bereich; die lokalen BtnPrimary/BtnCancel-Stile wurden entfernt und die Fussleiste auf " +
            "PrimaryButton/SecondaryButton + IsDefault/IsCancel umgestellt, die uebrige Arbeitsflaeche bleibt unveraendert."),
    ];

    private static readonly HashSet<string> AusgenommeneDateien =
        new(Ausnahmen.Select(a => a.Datei), StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Alle Fenster-XAMLs im gepruefen Umfang: Views/*.xaml (nur oberste Ebene, keine
    /// Unterordner wie Views/Controls oder Views/Pages), Dialogs/*.xaml, Views/Windows/*.xaml -
    /// jeweils nur echte <c>&lt;Window</c>-Wurzeln (UserControls/ResourceDictionaries im selben
    /// Ordner, z. B. PlayerCodingSidePanel.xaml oder PlayerWindow.Resources.xaml, fallen automatisch weg).
    /// </summary>
    private static IEnumerable<string> AlleFensterDateien()
    {
        var ordner = new[]
        {
            Path.Combine(UiRoot, "Views"),
            Path.Combine(UiRoot, "Dialogs"),
            Path.Combine(UiRoot, "Views", "Windows"),
        };

        foreach (var dir in ordner)
        {
            foreach (var pfad in Directory.GetFiles(dir, "*.xaml", SearchOption.TopDirectoryOnly).OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
            {
                var xaml = File.ReadAllText(pfad);
                if (xaml.Contains("<Window", StringComparison.Ordinal))
                    yield return pfad;
            }
        }
    }

    private static IEnumerable<string> GeprueftePfade()
        => AlleFensterDateien().Where(p => !AusgenommeneDateien.Contains(Path.GetFileName(p)));

    [Fact]
    public void Ausnahmeliste_verweist_nur_auf_tatsaechlich_vorhandene_Fenster()
    {
        var fehlend = Ausnahmen
            .Select(a => a.Datei)
            .Where(datei => !AlleFensterDateien().Any(p => string.Equals(Path.GetFileName(p), datei, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        Assert.True(fehlend.Count == 0,
            "Ausnahmeliste nennt Dateien, die nicht (mehr) im gepruefen Umfang liegen: " + string.Join(", ", fehlend));
    }

    [Fact]
    public void Mindestens_vierzig_Fenster_werden_tatsaechlich_geprueft()
    {
        // Schuetzt davor, dass ein Tippfehler im Ordnerpfad den Waechter leerlaufen laesst
        // (ein Test ueber 0 Dateien waere immer gruen und wuerde nichts pruefen).
        var anzahl = GeprueftePfade().Count();
        Assert.True(anzahl >= 40, $"Nur {anzahl} Fenster im Pruefumfang - Pfade/Ausnahmeliste pruefen.");
    }

    [Fact]
    public void Hoechstens_ein_PrimaryButton_je_Fenster()
    {
        var verstoesse = new List<string>();
        foreach (var pfad in GeprueftePfade())
        {
            var xaml = File.ReadAllText(pfad);
            var anzahl = Regex.Matches(xaml, @"Style=""\{(?:Static|Dynamic)Resource PrimaryButton\}""").Count;
            if (anzahl > 1)
                verstoesse.Add($"{Path.GetFileName(pfad)}: {anzahl} PrimaryButton-Knoepfe statt hoechstens einem");
        }

        Assert.True(verstoesse.Count == 0, string.Join("\n", verstoesse));
    }

    [Fact]
    public void IsCancel_Knopf_steht_direkt_links_vom_IsDefault_Knopf_falls_beide_existieren()
    {
        // Heuristik (wie die bestehenden DesignAudit*-Waechter): "direkt links" wird ueber die
        // Dokumentreihenfolge der <Button-Oeffnungstags geprueft - der Button-Tag unmittelbar VOR
        // dem IsDefault-Tag (kein anderer Button-Tag dazwischen) muss IsCancel="True" tragen. Das
        // deckt sich in dieser Codebasis mit der visuellen Reihenfolge, weil jede Knopfleiste eine
        // einzelne links-nach-rechts angeordnete StackPanel/Grid-Zeile ist (DialogButtonBar-Muster).
        var verstoesse = new List<string>();
        foreach (var pfad in GeprueftePfade())
        {
            var xaml = File.ReadAllText(pfad);
            var tags = Regex.Matches(xaml, @"<Button\b[^>]*>", RegexOptions.Singleline)
                .Select(m => m.Value)
                .ToList();

            for (var i = 0; i < tags.Count; i++)
            {
                if (!tags[i].Contains("IsDefault=\"True\"", StringComparison.Ordinal))
                    continue;

                var vorgaenger = i > 0 ? tags[i - 1] : null;
                if (vorgaenger is null || !vorgaenger.Contains("IsCancel=\"True\"", StringComparison.Ordinal))
                {
                    verstoesse.Add(
                        $"{Path.GetFileName(pfad)}: IsDefault-Knopf ohne unmittelbar vorangehenden IsCancel-Knopf " +
                        $"(Vorgaenger: {(vorgaenger is null ? "keiner" : vorgaenger)})");
                }
            }
        }

        Assert.True(verstoesse.Count == 0, string.Join("\n", verstoesse));
    }

    [Fact]
    public void Kein_lokaler_Button_Style_mehr_in_gepruefen_Fenstern()
    {
        var verstoesse = new List<string>();
        foreach (var pfad in GeprueftePfade())
        {
            var xaml = File.ReadAllText(pfad);
            if (Regex.IsMatch(xaml, @"<Style\s+x:Key=""[^""]+""\s+TargetType=""Button"""))
                verstoesse.Add(Path.GetFileName(pfad));
        }

        Assert.True(verstoesse.Count == 0,
            "Lokale Style x:Key=... TargetType=\"Button\"-Definition gefunden in: " + string.Join(", ", verstoesse));
    }

    [Fact]
    public void Kein_Background_direkt_an_einem_Button_Tag()
    {
        var verstoesse = new List<string>();
        foreach (var pfad in GeprueftePfade())
        {
            var xaml = File.ReadAllText(pfad);
            foreach (Match tag in Regex.Matches(xaml, @"<Button\b[^>]*>", RegexOptions.Singleline))
            {
                if (Regex.IsMatch(tag.Value, @"\bBackground="""))
                {
                    verstoesse.Add($"{Path.GetFileName(pfad)}: {tag.Value.Substring(0, Math.Min(80, tag.Value.Length))}...");
                    break;
                }
            }
        }

        Assert.True(verstoesse.Count == 0, "Background= direkt an <Button gefunden in:\n" + string.Join("\n", verstoesse));
    }
}
