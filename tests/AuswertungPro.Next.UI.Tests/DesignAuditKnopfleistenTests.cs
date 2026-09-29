using System.IO;
using System.Text.RegularExpressions;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Waechter zur Optikanalyse 28.09.2026, Aufgabe 4 («Fensterregel - uebrige Fenster + Waechter»),
/// Fix-Runde 1 (regelgranulare Ausnahmen statt Datei-weiter Sperren): prueft die Knopfregel
/// (Plan-Abschnitt «Global Constraints», Punkt 5) in JEDEM Fenster-XAML des UI-Projekts -
/// Views/*.xaml (nur die oberste Ebene), Dialogs/*.xaml und Views/Windows/*.xaml - ausser den
/// namentlich begruendeten Ausnahmen unten. Vier Regeln je Fenster:
/// (a) hoechstens EIN "primaerklassiger" Knopf - siehe <see cref="PrimaerklassigMuster"/> unten
///     fuer die genaue Zaehlregel bei <c>SuccessButton</c>,
/// (b) traegt ein Fenster ein <c>IsDefault="True"</c>, steht unmittelbar davor (gleiche
///     Dokumentreihenfolge, kein anderer Button dazwischen) ein Knopf mit <c>IsCancel="True"</c>,
/// (c) kein lokal definierter <c>Style x:Key="..." TargetType="Button"</c> mehr im Fenster,
/// (d) kein <c>Background=</c> direkt an einem <c>&lt;Button</c>-Tag.
/// Dieser Waechter ersetzt NICHT die engeren Aufgabe-3-Waechter (<c>DesignAuditDossierFensterTests</c>)
/// oder das lookless <c>NovaDialogHeader</c>/<c>DialogButtonBar</c>-Theme selbst - er ist der
/// umfassende Nachfolger, der ALLE Fenster erfasst.
///
/// **Ausnahmen sind regelgranular, nicht Datei-weit** (Fix-Runde 1, Koordinator-Rueckmeldung):
/// eine Ausnahme nennt Datei UND genau die Regel(n), von denen sie befreit ist - alle anderen
/// Regeln gelten fuer diese Datei unveraendert weiter. Nur die geschuetzten/aus dem Auftrag
/// ausgeschlossenen Video- und WebGIS-Fenster bleiben Datei-weit ausgenommen (siehe Begruendung
/// je Eintrag in <see cref="Ausnahmen"/>).
/// </summary>
public sealed class DesignAuditKnopfleistenTests
{
    private static readonly string UiRoot = RepoFile("src", "AuswertungPro.Next.UI");

    /// <summary>
    /// Die vier Regeln als Flags, damit eine Ausnahme genau die betroffene(n) Regel(n) nennen
    /// kann statt die ganze Datei stillzulegen.
    /// </summary>
    [Flags]
    private enum Regel
    {
        Keine = 0,
        A_HoechstensEinPrimaerknopf = 1 << 0,
        B_IsCancelDirektLinksVomIsDefault = 1 << 1,
        C_KeinLokalerButtonStyle = 1 << 2,
        D_KeinBackgroundAmButton = 1 << 3,
        Alle = A_HoechstensEinPrimaerknopf | B_IsCancelDirektLinksVomIsDefault
             | C_KeinLokalerButtonStyle | D_KeinBackgroundAmButton,
    }

    /// <summary>
    /// Namentliche, regelgranulare Ausnahmen mit Begruendung. Eine Ausnahme nimmt eine Datei nur
    /// von GENAU den genannten Regeln aus - nie stillschweigend, immer mit Grund hier dokumentiert.
    /// Datei-weite Ausnahmen (<see cref="Regel.Alle"/>) bleiben nur den geschuetzten/aus dem
    /// Auftrag ausgeschlossenen Fenstern vorbehalten (Video, WebGIS).
    /// </summary>
    private static readonly (string Datei, Regel Ausgenommen, string Grund)[] Ausnahmen =
    [
        ("PlayerWindow.xaml", Regel.Alle,
            "Video-Fenster mit eigenem Vollbild-Bedienkonzept (Zeitleiste/Playback); laut Aufgabe 4 ausdrücklich ausgenommen."),
        ("LiveFrameWindow.xaml", Regel.Alle,
            "Video-Fenster (Live-Ring-Overlay); wie PlayerWindow ausgenommen."),
        ("StartupSplashWindow.xaml", Regel.Alle,
            "Startanimation, kein Dialogfenster; laut Aufgabe 4 ausdrücklich ausgenommen."),
        ("PhotoMeasurementWindow.xaml", Regel.Alle,
            "Video-/Messfenster mit eigenem Werkzeugkasten (Foto-Overlay-Messwerkzeuge); laut Aufgabe 4 ausdrücklich ausgenommen."),
        ("WebGisVorschauWindow.xaml", Regel.Alle,
            "Geschuetzte WebGIS-Datei (Global Constraints Punkt 2) - nicht Teil dieses Auftrags, wird später angepasst."),
        ("WebGisSchreibBestaetigungWindow.xaml", Regel.Alle,
            "Geschuetzte WebGIS-Datei (Global Constraints Punkt 2) - nicht Teil dieses Auftrags, wird später angepasst."),
        ("WebGisHolenWindow.xaml", Regel.Alle,
            "Geschuetzte WebGIS-Datei (Global Constraints Punkt 2) - nicht Teil dieses Auftrags, wird später angepasst."),
        ("NovaDialogWindow.xaml", Regel.C_KeinLokalerButtonStyle,
            "NUR Regel (c): der lokale Style \"NovaDialogDangerButton\" (Aufgabe 1) ist die danger-" +
            "gestylte Ja-Variante für ConfirmWarn(defaultNo) - optisch identisch mit dem programmweiten " +
            "DangerButton, aber laut CLAUDE.md-Entscheid Aufgabe 1 bewusst NICHT dorthin verschoben " +
            "(\"Aufgabe 1 bleibt unangetastet\"). IsDefault/IsCancel werden hier vollständig im " +
            "Code-Behind gesetzt (ConfirmCancel/ConfirmWarn), nicht in XAML - Regel (b) hat dadurch " +
            "nichts zu prüfen und ist real erfuellt. Regeln (a) und (d) sind ebenfalls sauber " +
            "(genau 1 literales PrimaryButton, kein Background= an einem Button) - keine Ausnahme nötig."),
        ("TrainingStudioWindow.xaml", Regel.A_HoechstensEinPrimaerknopf,
            "NUR Regel (a): 2 primaerklassige Knoepfe sind in der \"2 - Fachliche Codierung\"-Spalte " +
            "echt gleichzeitig nötig - \"Akzeptieren (A)\" (SuccessButton) und \"Korrektur speichern (K)\" " +
            "(PrimaryButton) sind zwei gleichwertige Abschluesse DESSELBEN Codierschritts (KI-Vorschlag " +
            "war richtig vs. KI-Vorschlag wurde korrigiert), immer gemeinsam sichtbar/aktiviert " +
            "(IsEnabled={Binding IsAnnotationEntryEnabled} auf dem gemeinsamen Elternpanel), kein " +
            "Rang zwischen beiden. Alle anderen frueher primaerklassigen Knoepfe des Fensters " +
            "(Durchgang starten / Foto mit gewaehltem Modell prüfen / Codieren...(Katalog) / " +
            "Weiteres Ereignis / Bild fertig) sind auf ToolbarButtonAccent/SecondaryButton " +
            "zurueckgestuft, die 5 Schadensstufen-Knoepfe tragen jetzt die geteilten Severity1..5Button-" +
            "Stile statt lokalem Background= - Regeln (b)/(c)/(d) sind dadurch real sauber."),
    ];

    /// <summary>
    /// Primaerklassige Knopfstile: <c>PrimaryButton</c> UND <c>SuccessButton</c>. SuccessButton ist
    /// <c>BasedOn="{StaticResource PrimaryButton}"</c> (Theme.xaml) - eine gefuellte Flaeche mit
    /// derselben optischen Gewichtsklasse wie PrimaryButton, nur in Erfolgsfarbe statt Akzentfarbe.
    /// Rule (a) zaehlt deshalb BEIDE zusammen: zwei "shoutende" gefuellte Knoepfe im selben Fenster
    /// sind das Problem, unabhaengig davon, ob der zweite gruen oder blau ist.
    /// DangerButton und WarningButton zaehlen NICHT mit: beide sind
    /// <c>BasedOn="{StaticResource SecondaryButton}"</c> (Umriss statt Flaeche, siehe Controls.xaml) -
    /// dieselbe Sichtgewichtsklasse wie eine neutrale Nebenaktion, nur farblich als riskant/
    /// destruktiv markiert. Ebenso zaehlen ToolbarButtonAccent und die Severity1..5Button-Stile
    /// nicht mit: eigene Stilfamilien fuer Werkzeugleisten- bzw. Stufen-Auswahl, keine
    /// Fenster-Hauptaktion im Sinn der Knopfregel.
    /// </summary>
    private static readonly Regex PrimaerklassigMuster =
        new(@"Style=""\{(?:Static|Dynamic)Resource (?:PrimaryButton|SuccessButton)\}""", RegexOptions.Compiled);

    private static Regel AusnahmeFuer(string dateiname)
    {
        var eintrag = Ausnahmen.FirstOrDefault(a => string.Equals(a.Datei, dateiname, StringComparison.OrdinalIgnoreCase));
        return eintrag.Datei is null ? Regel.Keine : eintrag.Ausgenommen;
    }

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

    /// <summary>Alle gepruefen Pfade, bei denen die uebergebene Regel NICHT ausgenommen ist.</summary>
    private static IEnumerable<string> GeprueftePfade(Regel regel)
        => AlleFensterDateien().Where(p => !AusnahmeFuer(Path.GetFileName(p)).HasFlag(regel));

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
        var anzahl = GeprueftePfade(Regel.A_HoechstensEinPrimaerknopf).Count();
        Assert.True(anzahl >= 40, $"Nur {anzahl} Fenster im Pruefumfang - Pfade/Ausnahmeliste prüfen.");
    }

    [Fact]
    public void Hoechstens_ein_Primaerknopf_je_Fenster()
    {
        var verstoesse = new List<string>();
        foreach (var pfad in GeprueftePfade(Regel.A_HoechstensEinPrimaerknopf))
        {
            var xaml = File.ReadAllText(pfad);
            var anzahl = PrimaerklassigMuster.Matches(xaml).Count;
            if (anzahl > 1)
                verstoesse.Add($"{Path.GetFileName(pfad)}: {anzahl} primaerklassige Knoepfe (PrimaryButton/SuccessButton) statt hoechstens einem");
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
        foreach (var pfad in GeprueftePfade(Regel.B_IsCancelDirektLinksVomIsDefault))
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
        foreach (var pfad in GeprueftePfade(Regel.C_KeinLokalerButtonStyle))
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
        foreach (var pfad in GeprueftePfade(Regel.D_KeinBackgroundAmButton))
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
