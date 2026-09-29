using System.IO;
using System.Text.RegularExpressions;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Waechter zur Optikanalyse 28.09.2026, Aufgabe 11 («Leer- und Ladezustände, Fortschrittsbalken»):
/// (a) jede echte <c>&lt;ProgressBar</c> traegt einen der zwei benannten Theme-Stile
/// <c>ProgressBarThin</c> (4 px) oder <c>ProgressBarStandard</c> (8 px) — entweder als
/// <c>Style="{StaticResource ...}"</c>-Attribut oder als lokaler <c>&lt;ProgressBar.Style&gt;</c> mit
/// <c>BasedOn="{StaticResource ...}"</c>. Namentliche Ausnahmen mit Begruendung sind erlaubt.
/// (b) die Liste der Seiten/Fenster, die in dieser Aufgabe ein <c>EmptyStateControl</c> bekommen haben
/// (oder es schon ueber <c>StatusHost</c>/eine gleichwertige eigene Regel hatten), bleibt bestehen —
/// ein spaeteres Entfernen faellt hier auf.
/// </summary>
public sealed class DesignAuditLeerUndLadezustandTests
{
    private static readonly string UiRoot = RepoFile("src", "AuswertungPro.Next.UI");

    // Erfasst ein echtes ProgressBar-ELEMENT (Attribut- oder Selbstschluss-Form), aber keine
    // Property-Element-Syntax wie <ProgressBar.Value> oder <ProgressBar.Foreground> (negativer
    // Lookahead auf den Punkt direkt nach dem Namen).
    private static readonly Regex ProgressBarElement =
        new(@"<ProgressBar(?!\.)\b[^>]*>", RegexOptions.Compiled);

    /// <summary>
    /// Datei + Begruendung. Nur diese Dateien duerfen ein ProgressBar-Element ohne
    /// ProgressBarThin/ProgressBarStandard enthalten.
    /// </summary>
    private static readonly (string Datei, string Grund)[] ProgressBarAusnahmen =
    [
        ("VsaCodeExplorerWindow.xaml",
            "Die vier \"ProgressBar0\"..\"ProgressBar3\" sind Border-Elemente (Wizard-Schrittanzeige mit " +
            "vier UNABHAENGIG eingefaerbten Segmenten je Quantifizierungsschritt), keine echten " +
            "<ProgressBar>-Steuerelemente — ein einzelner ProgressBar-Wert/-Balken kann das nicht " +
            "abbilden (kein Balken mit einem Value/Maximum, sondern vier eigenstaendige Kacheln)."),
        ("StartupSplashWindow.xaml",
            "\"ProgressBar\" ist dort ein benannter Border fuer die eigene Startanimation " +
            "(StartupSplashChoreografie), kein <ProgressBar>-Steuerelement; das Fenster ist laut " +
            "Knopfregel/Aufgabe 4 ohnehin als Video-/Startfenster ausgenommen."),
    ];

    private static IEnumerable<string> AlleXamlDateien()
    {
        foreach (var datei in Directory.EnumerateFiles(UiRoot, "*.xaml", SearchOption.AllDirectories))
        {
            if (datei.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || datei.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                continue;
            }

            yield return datei;
        }
    }

    private static bool TraegtBenanntenStil(string tag)
        => tag.Contains("StaticResource ProgressBarThin}", StringComparison.Ordinal)
            || tag.Contains("StaticResource ProgressBarStandard}", StringComparison.Ordinal)
            || tag.Contains("DynamicResource ProgressBarThin}", StringComparison.Ordinal)
            || tag.Contains("DynamicResource ProgressBarStandard}", StringComparison.Ordinal);

    [Fact]
    public void Theme_definiert_die_zwei_Fortschrittsbalken_Stile()
    {
        var controls = File.ReadAllText(Path.Combine(UiRoot, "Theme", "Controls.xaml"));

        Assert.Contains("x:Key=\"ProgressBarThin\"", controls, StringComparison.Ordinal);
        Assert.Contains("x:Key=\"ProgressBarStandard\"", controls, StringComparison.Ordinal);

        AssertStyleContains(controls, "ProgressBarThin", "Property=\"Height\" Value=\"4\"");
        AssertStyleContains(controls, "ProgressBarStandard", "Property=\"Height\" Value=\"8\"");

        // Die alte Sonderfassung fuer die Importkarte ist zugunsten der zwei einheitlichen Stile
        // aufgehoben (Aufgabe 11) — sie darf nicht wieder auftauchen.
        Assert.DoesNotContain("x:Key=\"ImportProgressBar\"", controls, StringComparison.Ordinal);
        Assert.DoesNotContain("x:Key=\"ImportBarHeight\"", controls, StringComparison.Ordinal);
    }

    [Fact]
    public void Mindestens_zwanzig_ProgressBar_Elemente_werden_tatsaechlich_geprueft()
    {
        // Schuetzt davor, dass ein Tippfehler im Suchmuster den Waechter leerlaufen laesst.
        var anzahl = AlleXamlDateien()
            .Where(p => !IstAusgenommen(p))
            .Sum(p => ProgressBarElement.Matches(File.ReadAllText(p)).Count);

        Assert.True(anzahl >= 20, $"Nur {anzahl} gepruefte ProgressBar-Elemente - Suchmuster/Umfang pruefen.");
    }

    [Fact]
    public void Jede_ProgressBar_traegt_ProgressBarThin_oder_ProgressBarStandard()
    {
        var verstoesse = new List<string>();

        foreach (var pfad in AlleXamlDateien())
        {
            if (IstAusgenommen(pfad))
                continue;

            var xaml = File.ReadAllText(pfad);
            foreach (Match match in ProgressBarElement.Matches(xaml))
            {
                var tagEnde = match.Index + match.Length;

                // Attribut-Form: der Stil steht im Tag selbst.
                if (TraegtBenanntenStil(match.Value))
                    continue;

                // Property-Element-Form: <ProgressBar ...>\n <ProgressBar.Style>\n <Style ... BasedOn="{StaticResource ProgressBarThin}">
                // Nur relevant, wenn das Element nicht selbstschliessend ist. Die Suche bleibt auf
                // GENAU dieses Element begrenzt (bis zu dessen eigenem schliessendem </ProgressBar>),
                // damit sie nie den Stil eines spaeteren Balkens im selben Dokument mitliest.
                if (!match.Value.EndsWith("/>", StringComparison.Ordinal))
                {
                    var eigenesEnde = xaml.IndexOf("</ProgressBar>", tagEnde, StringComparison.Ordinal);
                    if (eigenesEnde > tagEnde)
                    {
                        var rest = xaml[tagEnde..eigenesEnde];
                        var styleBlockStart = rest.IndexOf("<ProgressBar.Style>", StringComparison.Ordinal);
                        if (styleBlockStart >= 0)
                        {
                            var styleBlockEnde = rest.IndexOf("</ProgressBar.Style>", styleBlockStart, StringComparison.Ordinal);
                            if (styleBlockEnde > styleBlockStart)
                            {
                                var block = rest[styleBlockStart..styleBlockEnde];
                                if (TraegtBenanntenStil(block))
                                    continue;
                            }
                        }
                    }
                }

                verstoesse.Add($"{Path.GetFileName(pfad)}: {match.Value.Substring(0, Math.Min(90, match.Value.Length))}...");
            }
        }

        Assert.True(verstoesse.Count == 0,
            "ProgressBar ohne ProgressBarThin/ProgressBarStandard gefunden:\n" + string.Join("\n", verstoesse));
    }

    [Fact]
    public void Ausnahmeliste_verweist_nur_auf_tatsaechlich_vorhandene_Dateien()
    {
        var fehlend = ProgressBarAusnahmen
            .Select(a => a.Datei)
            .Where(datei => !AlleXamlDateien().Any(p => string.Equals(Path.GetFileName(p), datei, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        Assert.True(fehlend.Count == 0,
            "Ausnahmeliste nennt Dateien, die nicht (mehr) existieren: " + string.Join(", ", fehlend));
    }

    private static bool IstAusgenommen(string pfad)
        => ProgressBarAusnahmen.Any(a => string.Equals(a.Datei, Path.GetFileName(pfad), StringComparison.OrdinalIgnoreCase));

    private static void AssertStyleContains(string xaml, string key, params string[] expectedParts)
    {
        var marker = $"x:Key=\"{key}\"";
        var start = xaml.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Style {key} was not found.");

        var end = xaml.IndexOf("</Style>", start, StringComparison.Ordinal);
        Assert.True(end >= 0, $"Style {key} has no closing tag.");
        var style = xaml[start..end];

        foreach (var expected in expectedParts)
            Assert.Contains(expected, style);
    }

    // ── EmptyStateControl: Liste der in Aufgabe 11 versorgten Listen/Tabellen ──

    /// <summary>
    /// Datei (relativ zu src/AuswertungPro.Next.UI) + Kurzbeschreibung der versorgten Liste.
    /// Jede Datei muss "EmptyStateControl" enthalten — das haelt fest, dass der Leerzustand nicht
    /// spaeter stillschweigend wieder verschwindet.
    /// </summary>
    private static readonly (string[] Pfad, string Beschreibung)[] EmptyStateDateien =
    [
        (["Views", "Pages", "SanierungsMatrixPage.xaml"], "Sanierungs-Matrix (Haltungen)"),
        (["Views", "Pages", "SchachtSanierungsMatrixPage.xaml"], "Schacht-Matrix"),
        (["Views", "Pages", "SchattenauswertungPage.xaml"], "Schattenauswertung"),
        (["Views", "Windows", "ImportPreviewWindow.xaml"], "Import-Vorschau (Aenderungen)"),
        (["Views", "Windows", "CodeCatalogEditorWindow.xaml"], "Code-Katalog-Editor"),
        (["Views", "Windows", "MeasureTemplateEditorWindow.xaml"], "Massnahmen-Editor (Vorlagen + Positionen)"),
        (["Views", "Windows", "ObservationCatalogWindow.xaml"], "Beobachtungskatalog (Suchliste)"),
        (["Views", "Windows", "VerteilenWindow.xaml"], "Verteilen-Vorschau"),
        (["Views", "Windows", "XtfLieferungWindow.xaml"], "SIA405-Lieferung (Objektliste)"),
        (["Views", "Windows", "DossierHoldingPickerWindow.xaml"], "Dossier-Haltungspicker"),
        (["Views", "Windows", "DossierShaftPickerWindow.xaml"], "Dossier-Schachtpicker"),
        (["Views", "Windows", "BeobachtungenWindow.xaml"], "Beobachtungen-Fenster"),
        (["Views", "ProtocolHistoryWindow.xaml"], "Protokoll-Historie"),
        (["Views", "ProtocolObservationsWindow.xaml"], "Beobachtungen/Schaeden-Fenster"),
        (["Views", "Windows", "PersonalGoldAlbumWindow.xaml"], "Goldalbum (auf EmptyStateControl umgestellt)"),
        (["Views", "Windows", "StrassenUebernahmeWindow.xaml"], "Strasse uebernehmen"),
        (["Views", "Windows", "SchachtMassnahmenWindow.xaml"], "Schacht-Massnahmen (deine Liste)"),
        (["Views", "Windows", "SchachtMassnahmenKatalogEditorWindow.xaml"], "Schacht-Massnahmen-Katalog-Editor"),
        (["Views", "Pages", "BuilderPage.xaml"], "Druckcenter (Haltungsliste)"),
        (["Views", "Pages", "DossiersPage.xaml"], "Dossiers-Cockpit (Liegenschaftsliste)"),

        // Fix-Runde 1 (Koordinator-Rueckmeldung, Pascal "alles"): die restlichen Haltungs-/
        // Schachtansicht-Unterlisten und zwei Dossier-Fenster. Laufen alle nur im echten
        // Kindprozess (App-Ressourcen vorhanden) - siehe Ausnahmeliste unten fuer die drei
        // Dateien, die das NICHT tun und deshalb bewusst NICHT versorgt sind.
        (["Views", "Pages", "Haltungsansicht", "HaltungUebersichtPanel.xaml"], "Haltungs-Uebersicht (Leerzustand + Schadenliste)"),
        (["Views", "Pages", "Schachtansicht", "SchachtUebersichtPanel.xaml"], "Schacht-Uebersicht (Leerzustand + Schadenliste)"),
        (["Views", "Pages", "Haltungsansicht", "HaltungsansichtView.xaml"], "Abgedockte Haltungsansicht (Liste + Schadenliste)"),
        (["Views", "Pages", "Schachtansicht", "SchachtansichtView.xaml"], "Abgedockte Schachtansicht (Liste + Schadenliste)"),
        (["Views", "Windows", "DossierAreaWindow.xaml"], "Gebietsangaben (Themenliste)"),
        (["Views", "Windows", "DossierBatchWindow.xaml"], "Dossiers aus dem Projekt erzeugen (Vorschlagsliste)"),

        // Fix-Runde 2 (Koordinator-Rueckmeldung): EmptyStateControl.xaml:67 setzte den optionalen
        // Aktionsknopf mit Style="{StaticResource SecondaryButton}" - das loeste beim BAML-Laden
        // (nicht erst bei Sichtbarkeit) auf und warf ohne Application-Kontext eine
        // XamlParseException in bare-construction Unit-Tests (new HaltungAufklappListe()/
        // new SchachtAufklappListe()/new PlayerCodingSidePanel() auf blossem STA-Thread, z. B.
        // DataPageAnsichtUmschalterTests, NovaListenWiederverbindenTests,
        // PlayerCodingSidePanelControllerInitializerTests,
        // PlayerCodingSidePanelEventBinderTests, HaltungAufklappListeFokusTests). Umgestellt auf
        // Style="{DynamicResource SecondaryButton}" (loest lazy zur Laufzeit auf, faellt ohne
        // Application-Ressourcen einfach auf unstyled zurueck statt zu werfen) - danach liefen alle
        // 19 vorher roten Tests wieder gruen, und diese drei Dateien tragen jetzt ganz normal ein
        // EmptyStateControl wie jede andere Datei dieser Liste.
        (["Views", "Pages", "Haltungsansicht", "HaltungAufklappListe.xaml"], "Haltungs-Aufklappliste"),
        (["Views", "Pages", "Schachtansicht", "SchachtAufklappListe.xaml"], "Schacht-Aufklappliste"),
        (["Views", "Windows", "PlayerCodingSidePanel.xaml"], "Player-Seitenpanel (KI-Befunde + Import)"),
    ];

    [Fact]
    public void Jede_versorgte_Liste_traegt_ein_EmptyStateControl()
    {
        var fehlend = new List<string>();

        foreach (var (segmente, beschreibung) in EmptyStateDateien)
        {
            var pfad = RepoFile(new[] { "src", "AuswertungPro.Next.UI" }.Concat(segmente).ToArray());
            Assert.True(File.Exists(pfad), $"Datei nicht gefunden: {string.Join("/", segmente)}");

            var xaml = File.ReadAllText(pfad);
            if (!xaml.Contains("EmptyStateControl", StringComparison.Ordinal))
                fehlend.Add($"{string.Join("/", segmente)} ({beschreibung})");
        }

        Assert.True(fehlend.Count == 0,
            "Kein EmptyStateControl mehr gefunden in:\n" + string.Join("\n", fehlend));
    }

    [Fact]
    public void Medienkonflikte_behalten_ihren_Leerzustand_ueber_StatusHost()
    {
        // War schon vor Aufgabe 11 vollstaendig (EmptyIcon/EmptyTitle/EmptyMessage am StatusHost) -
        // dieser Test haelt fest, dass die Seite nicht regressiert.
        var xaml = File.ReadAllText(RepoFile("src", "AuswertungPro.Next.UI", "Views", "Pages", "MediaConflictsPage.xaml"));

        Assert.Contains("EmptyTitle=\"Keine offenen Medienkonflikte\"", xaml, StringComparison.Ordinal);
        Assert.Contains("<controls:StatusHost", xaml, StringComparison.Ordinal);
    }
}
