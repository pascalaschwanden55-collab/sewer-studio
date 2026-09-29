using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Nova-Fixwelle 2b (P5): Sichtbare Beschriftungen schreiben echte Umlaute — auch die, die
/// nicht im XAML stehen, sondern in C# entstehen.
///
/// Der bestehende Umlaut-Waechter (<see cref="DesignAuditFeinschliffTests"/>) prueft nur
/// XAML-Attribute. In der Abnahme fielen deshalb zwei Stellen durch: „Lernbasis: 0 Faelle"
/// auf der Haltungsseite und die Gruppenbeschreibung „Bewertung, Schaeden und
/// Pruefresultate." in den Eingabefeldern der Schaechte.
///
/// Bewusst eine kurze Positivliste von Dateien statt eines Rundumschlags: Nur hier ist
/// belegt, dass JEDER mehrteilige Text eine sichtbare Beschriftung ist. Kommt eine weitere
/// Quelle sichtbarer Laufzeittexte dazu, gehoert sie in diese Liste.
///
/// Geprueft werden nur Zeichenketten MIT Leerzeichen. Feldnamen und Katalogschluessel
/// (<c>Gefaelle_Promille</c>, <c>VSA_Zustandsnote_D</c>) haben keine und bleiben unberuehrt —
/// sie sind Datenschluessel, keine Beschriftungen, und duerfen ihre Schreibweise nie aendern.
/// </summary>
public sealed class DesignAuditLaufzeittexteTests
{
    /// <summary>Dateien, deren mehrteilige Zeichenketten in sichtbare Beschriftungen fliessen.</summary>
    private static readonly string[][] Quellen =
    [
        ["src", "AuswertungPro.Next.Application", "DataPage", "LearningReadinessPresenter.cs"],
        ["src", "AuswertungPro.Next.UI", "DataPage", "SchaechteRecordDetailsBuilder.cs"],
        ["src", "AuswertungPro.Next.UI", "DataPage", "DataPageRecordDetailsBuilder.cs"],
        // Aufgabe 10b: UserError.Describe() ist ausschliesslich Nutzertext — jede
        // Zeichenkette in dieser Datei ist eine sichtbare Meldung, ohne Log-/Obsolete-/
        // Datenwert-Ausnahmen wie in den gemischten Dateien unten. Deshalb hier in der
        // vollstaendigen Positivliste statt nur in der Negativliste.
        ["src", "AuswertungPro.Next.Application", "Common", "UserError.cs"]
    ];

    /// <summary>
    /// Deutsche Woerter in Ersatzschreibweise. Die Liste ist bewusst konkret: Ein blosses
    /// „ae/oe/ue irgendwo" traefe auch „Neu", „Quelle" oder „Muster".
    /// </summary>
    private static readonly string[] Ersatzschreibweisen =
    [
        "Faell", "Schaetz", "aehnlich", "Gruen", "Schaed", "Pruef", "Verknuepf",
        "Loesch", "Oeffn", "Groess", "Naechst", "Ueber", "Zustaend", "Maengel", "Bemuehung"
    ];

    [Fact]
    public void Sichtbare_Laufzeittexte_tragen_echte_Umlaute()
    {
        var funde = (from teile in Quellen
                     let pfad = RepoFile(teile)
                     from text in Zeichenketten(File.ReadAllText(pfad))
                     from wort in Ersatzschreibweisen
                     where text.Contains(wort, System.StringComparison.Ordinal)
                     select $"{Path.GetFileName(pfad)}: \"{text}\" ({wort})").ToList();

        Assert.True(funde.Count == 0, "Sichtbare Texte ohne Umlaut:\n" + string.Join("\n", funde));
    }

    /// <summary>
    /// Der Waechter darf nicht leerlaufen: Er muss in jeder gelisteten Datei wirklich Texte
    /// finden. Sonst wuerde eine umbenannte oder verschobene Datei still gruen bleiben.
    /// </summary>
    [Fact]
    public void Jede_gelistete_Datei_liefert_auch_wirklich_Texte()
    {
        foreach (var teile in Quellen)
        {
            var pfad = RepoFile(teile);
            Assert.True(File.Exists(pfad), $"{pfad} fehlt");
            Assert.NotEmpty(Zeichenketten(File.ReadAllText(pfad)));
        }
    }

    /// <summary>
    /// Alle Zeichenketten mit mindestens einem Leerzeichen, ohne Kommentarzeilen.
    /// Kommentare duerfen und sollen weiter in Ersatzschreibweise stehen (CLAUDE.md).
    /// </summary>
    private static string[] Zeichenketten(string quelle)
        => quelle.Split('\n')
            .Where(zeile => !zeile.TrimStart().StartsWith("//", System.StringComparison.Ordinal))
            .SelectMany(zeile => Regex.Matches(zeile, "\"([^\"\\\n]*)\"").Select(m => m.Groups[1].Value))
            .Where(text => text.Contains(' '))
            .ToArray();

    /// <summary>
    /// Aufgabe 10b: gezielte Ruecklauf-Sperre fuer einzelne bereinigte Dialog-/Statustexte in
    /// Dateien, die daneben bewusst NICHT bereinigte Zeichenketten behalten — Log-Zeilen
    /// (<c>BestEffort.ReportWarning</c>/<c>request.Log</c>), <c>[Obsolete]</c>-Hinweise fuer
    /// Entwickler oder Datenwerte, die mit Excel-Farbregeln/Katalogen exakt uebereinstimmen
    /// muessen (z. B. „Pruefung bestanden" in <c>DataPageDropdownOptionGroupFactory.cs</c>).
    /// Bei diesen gemischten Dateien wuerde <see cref="Sichtbare_Laufzeittexte_tragen_echte_Umlaute"/>
    /// (verlangt lueckenlos JEDE Zeichenkette sauber) faelschlich rot werden — deshalb hier nur
    /// je ein konkreter, tatsaechlich behobener Altwert je Datei statt der ganzen Datei.
    /// </summary>
    private static readonly (string[] Datei, string DarfNichtMehrVorkommen)[] BehobeneEinzeltexte =
    [
        (["src", "AuswertungPro.Next.UI", "App.xaml.cs"], "geoeffnete Programmfenster"),
        (["src", "AuswertungPro.Next.UI", "Settings", "SettingsFullBackupWorkflow.cs"], "Datensicherung laeuft bereits"),
        (["src", "AuswertungPro.Next.UI", "Settings", "SettingsPathWorkflow.cs"], "Projektpfad waehlen"),
        (["src", "AuswertungPro.Next.UI", "Settings", "FullBackupOperationState.cs"], "Berechne Groessen"),
        (["src", "AuswertungPro.Next.UI", "Settings", "SettingsCodexArtifactCleanupPresentationBuilder.cs"], "Groesste Bereiche"),
        (["src", "AuswertungPro.Next.UI", "Settings", "SettingsCodexArtifactCleanupWorkflow.cs"], "geschuetzten oder belegten"),
        (["src", "AuswertungPro.Next.UI", "Settings", "SettingsFullBackupPresentationBuilder.cs"], "Groesse unbekannt"),
        (["src", "AuswertungPro.Next.UI", "Settings", "SettingsKnowledgeBackupWorkflow.cs"], "werden ueberschrieben"),
        (["src", "AuswertungPro.Next.UI", "Settings", "SettingsProgramCleanupPresentationBuilder.cs"], "Temporaere Arbeitsdaten"),
        (["src", "AuswertungPro.Next.UI", "Settings", "SettingsProgramCleanupWorkflow.cs"], "Suche temporaere Programmdaten"),
        (["src", "AuswertungPro.Next.UI", "Settings", "SettingsProgramSnapshotWorkflow.cs"], "UNVOLLSTAENDIG"),
        (["src", "AuswertungPro.Next.UI", "DataPage", "DataPagePrintController.cs"], "Bitte zuerst eine Haltung auswaehlen"),
        (["src", "AuswertungPro.Next.UI", "DataPage", "DataPageCostRestoreController.cs"], "speichern/oeffnen"),
        (["src", "AuswertungPro.Next.UI", "DataPage", "DataPageDichtheitPdfController.cs"], "Kein Dichtheitspruefungsprotokoll fuer"),
        (["src", "AuswertungPro.Next.UI", "DataPage", "DataPageDropdownOptionGroupFactory.cs"], "Pruefungsresultat-Liste"),
        (["src", "AuswertungPro.Next.UI", "DataPage", "DataPageHoldingRenameController.cs"], "nicht ueberschrieben"),
        (["src", "AuswertungPro.Next.UI", "DataPage", "DataPageMeasureSuggestionController.cs"], "Geschaetzte Kosten"),
        (["src", "AuswertungPro.Next.UI", "DataPage", "DataPageOriginalPdfController.cs"], "gefunden fuer Haltung"),
        (["src", "AuswertungPro.Next.UI", "DataPage", "DataPageRecordCollectionController.cs"], "wirklich geloescht werden"),
        (["src", "AuswertungPro.Next.UI", "DataPage", "DataPageRecordCommandRouter.cs"], "zuerst eine Zeile auswaehlen"),
        (["src", "AuswertungPro.Next.UI", "DataPage", "DataPageRowNavigationController.cs"], "gueltige Zahl eingeben"),
        (["src", "AuswertungPro.Next.UI", "DataPage", "DataPageVideoAnalysisController.cs"], "manuell codierte Eintraege"),
        (["src", "AuswertungPro.Next.UI", "DataPage", "DataPageVideoPathWorkflowController.cs"], "Video-Ordner auswaehlen"),
        (["src", "AuswertungPro.Next.UI", "DataPage", "DataPageVideoPlaybackController.cs"], "Bitte pruefen, ob 'VideoLAN"),
        (["src", "AuswertungPro.Next.UI", "DataPage", "DataPageVideoRelinkController.cs"], "Video auswaehlen"),
        (["src", "AuswertungPro.Next.UI", "DataPage", "SchaechteShaftRenameController.cs"], "nicht ueberschrieben"),
        (["src", "AuswertungPro.Next.UI", "LiveControl", "LiveControlRetryBridge.cs"], "Datenseite nicht geoeffnet"),
        (["src", "AuswertungPro.Next.UI", "ViewModels", "ShellViewModel.cs"], "Projekte-Verzeichnis waehlen"),
        (["src", "AuswertungPro.Next.UI", "Services", "SchaechteDropdownCommandFactory.cs"], "Eigentuemer-Liste"),
        (["src", "AuswertungPro.Next.UI", "ProjectPage", "ProjectPageDropdownCommandFactory.cs"], "Eigentuemer-Liste"),
        (["src", "AuswertungPro.Next.UI", "Ai", "Training", "TrainingCenterDistributionWorkflow.cs"], "hinzugefuegt"),
        (["src", "AuswertungPro.Next.UI", "Ai", "Training", "TrainingYoloExportWorkflow.cs"], "wird geprueft"),
        (["src", "AuswertungPro.Next.Infrastructure", "Ai", "Training", "ExportPlans", "TrainingYoloExportCoordinator.cs"], "vollstaendige"),
        (["src", "AuswertungPro.Next.UI", "Views", "Windows", "DossierBatchWindow.xaml.cs"], "geladen werden: \" + ex.Message"),
        (["src", "AuswertungPro.Next.UI", "ViewModels", "Pages", "DossiersPageViewModel.Actions.cs"], "geöffnet werden: \" + ex.Message"),
    ];

    [Fact]
    public void Behobene_Einzeltexte_fallen_nicht_auf_Ersatzschreibweise_oder_Rohtext_zurueck()
    {
        var funde = BehobeneEinzeltexte
            .Select(f => (Pfad: RepoFile(f.Datei), f.DarfNichtMehrVorkommen))
            .Where(f => File.ReadAllText(f.Pfad).Contains(f.DarfNichtMehrVorkommen, System.StringComparison.Ordinal))
            .Select(f => $"{Path.GetFileName(f.Pfad)}: enthaelt wieder \"{f.DarfNichtMehrVorkommen}\"")
            .ToList();

        Assert.True(funde.Count == 0, "Rueckfall in Ersatzschreibweise/Rohtext:\n" + string.Join("\n", funde));
    }

    /// <summary>
    /// Wie <see cref="Jede_gelistete_Datei_liefert_auch_wirklich_Texte"/>, nur fuer die
    /// gezielten Einzeltexte: jede gelistete Datei muss wirklich existieren, sonst bliebe
    /// eine verschobene/umbenannte Datei still gruen.
    /// </summary>
    [Fact]
    public void Jede_Datei_mit_behobenem_Einzeltext_existiert()
    {
        foreach (var (datei, _) in BehobeneEinzeltexte)
            Assert.True(File.Exists(RepoFile(datei)), $"{RepoFile(datei)} fehlt");
    }

    /// <summary>
    /// Aufgabe 10c1 (Sprachbereinigung restlicher C#-Laufzeittexte im UI-Projekt): waehrend
    /// dieser Aufgabe bereinigte Woerter (Ersatzschreibweise -&gt; echter Umlaut) duerfen im
    /// GANZEN UI-Projekt nicht zurueckfallen — nicht nur in den einzelnen oben gelisteten
    /// Dateien. Anders als <see cref="Sichtbare_Laufzeittexte_tragen_echte_Umlaute"/> (feste
    /// Dateiliste, JEDE Zeichenkette muss sauber sein) durchsucht dieser Test ALLE C#-Dateien
    /// des UI-Projekts (ausser den laut Auftrag geschuetzten WebGIS-Dateien) nach genau diesen
    /// Wortformen als GANZES Wort (Wortgrenze) — Feldschluessel mit Unterstrich
    /// (<c>Ausgefuehrt_durch</c>, <c>Nova_Pruefung</c>, <c>Primaere_Schaeden</c>) bleiben davon
    /// unberuehrt, weil der Unterstrich selbst ein Wortzeichen ist und die Wortgrenze direkt
    /// davor/danach aufhebt.
    ///
    /// Geprueft wird NUR eine Zeichenkette, die (nach Entfernen von <c>{...}</c>-Ausdruecken)
    /// mindestens ein Leerzeichen enthaelt — derselbe Grundsatz wie beim bestehenden
    /// <see cref="Sichtbare_Laufzeittexte_tragen_echte_Umlaute"/>-Waechter. Ein einzelnes
    /// ASCII-Wort ohne Leerzeichen ist im ganzen Projekt viel eher ein Schluessel/Tag/Datenwert
    /// als ein Anzeigetext (siehe Fix-Runde 1: <c>DataPageAnsichtUmschalter.LoeschenMarke</c>,
    /// <c>CostCalculatorLineOrderController.GroupOrder</c>, <c>LiveControlColorParser.NamedColors</c>
    /// waren genau deshalb faelschlich betroffen) und wird deshalb bewusst NICHT automatisch
    /// geprueft.
    ///
    /// Ausgenommen sind ausserdem: Kommentarzeilen (<c>//</c> am Zeilenanfang) UND ein
    /// anhaengender Zeilenkommentar hinter echtem Code (derselbe naive, aber im Projekt bereits
    /// etablierte Ansatz wie in <see cref="DesignAuditFeinschliffTests"/>: der Text ab dem
    /// ersten <c>//</c> wird abgeschnitten, bevor nach Zeichenketten gesucht wird),
    /// <c>[Obsolete(...)]</c>-Hinweise (Entwicklertext), der Inhalt von Interpolations-/
    /// Formatausdruecken <c>{...}</c> (C#-Code wie <c>{result.BereitsVollstaendig}</c>, keine
    /// Beschriftung) sowie die drei Excel-Farbregel-Werte in
    /// <see cref="GeschuetzteGanzeZeichenketten"/>, die absichtlich „Pruefung" statt „Prüfung"
    /// schreiben, weil <c>ExcelReportStyle.Farbregeln</c> exakt diese Zeichenketten matcht.
    /// Zeichenketten-Literale mit ESCAPETEN Anfuehrungszeichen (<c>\"...\"</c>, wie sie
    /// Architektur-Tests verwenden, die Quellcode als Text vergleichen) werden korrekt als EIN
    /// Literal erkannt, nicht an der ersten escapten Anfuehrung abgeschnitten — sonst waeren
    /// genau solche Stellen (siehe Fix-Runde 1: <c>ObservationCatalogWindowInputNormalizerArchitectureTests</c>,
    /// <c>SchaechtePageArchitectureGuardTests</c>) fuer den Waechter unsichtbar.
    ///
    /// WICHTIG fuer diesen Waechter selbst: <see cref="Aufgabe10c1BereinigteWoerter"/> MUSS die
    /// ALTE Ersatzschreibweise (ASCII) enthalten, nicht die neue Umlautschreibweise — der Test
    /// prueft ja, dass die ASCII-Form NICHT mehr vorkommt. Ein automatisches Bereinigungsskript
    /// darf diese Liste (und <see cref="BehobeneEinzeltexte"/> weiter oben) deshalb nie anfassen.
    ///
    /// Bewusst NICHT in der Wortliste (bleiben ASCII, kein Rueckfall zu pruefen — Fix-Runde 1
    /// nach Review hat mehrere davon ueberhaupt erst als echte Regressionen aufgedeckt):
    /// „Ausgefuehrt"/„ausgefuehrt" (Feldschluessel-Alias auch ohne Unterstrich, siehe
    /// <c>SchachtSanierungPflichtfeldValidator.AusgefuehrtDurchAliases</c>, und der Fallback-
    /// Spaltenschluessel in <c>SchaechtePageViewModel.cs</c>), „Schaechte"/„Uebersicht" (ASCII-
    /// Navigationsschluessel, <see cref="AuswertungPro.Next.UI.ViewModels.ShellNavigationTitles"/>),
    /// „Eigentuemer" (dokumentierter ASCII-Feldname, siehe <c>SchaechtePageViewModel.cs</c>
    /// Kommentar „das Feld heisst Eigentuemer — beides ist dieselbe Spalte" — AUSSER in echter
    /// PDF-Fliesstext-Prosa wie <c>CostCalculatorPdfExportModelBuilder.cs</c>, dort gilt die
    /// Ausnahme bewusst NICHT), „Pruefungsresultat"/„Referenzpruefung" (Feldschluessel),
    /// „geschaetzt"/„Gefuellt" (interne Datenwerte, keine Beschriftung), „Massnahme(n)" (schon
    /// ohne Umlaut korrekt geschrieben), „gruen"/„gruene" (bleibt ASCII als Eingabe-Alias-
    /// Schluessel in <c>LiveControlColorParser.NamedColors</c>, dieselbe Ausnahme wie das dort
    /// dokumentierte „weiss"/„weiß"-Paar), „Aenderungen"/„Aenderung" (Dossier-Feld-/Spaltenschluessel,
    /// siehe <c>DossierPreviewFieldCatalog</c> Application, <c>DossierWordTemplateExportService</c>
    /// Infrastructure — beide bleiben ASCII), „loeschen" (Menue-Marke <c>DataPageAnsichtUmschalter.LoeschenMarke</c>,
    /// muss das XAML-<c>Tag="loeschen"</c> treffen; „Loeschen"/„Löschen" gross geschrieben sind
    /// dagegen unproblematische reine Anzeigetexte und bleiben in der Liste), „Qualitaet"
    /// (Gruppenname in <c>CostCalculatorLineOrderController.GroupOrder</c>, spiegelt
    /// <c>CatalogItemGrouping</c> Infrastructure und <c>position_templates.json</c>), „Schaeden"
    /// (steckt als Wortbestandteil in der Feld-Alias-Zeichenkette „Primaere Schaeden" in
    /// <c>SchachtDamageLineBuilder.DamageFieldCandidates</c>, geschrieben von
    /// <c>SchachtProtocolApplier</c>/<c>SchachtProFieldNames.PrimaereSchaedenAscii</c>
    /// Infrastructure), „hoehe" (steckt in mehreren Punkt-getrennten Datenfeldschluesseln wie
    /// <c>vsa.hoehe.mm</c>, <c>deckel.hoehe</c>, <c>haltungspunkt.hoehe</c> — ein Punkt ist kein
    /// Wortzeichen und haette die Wortgrenzenpruefung nicht automatisch geschuetzt).
    /// </summary>
    private static readonly string[] Aufgabe10c1BereinigteWoerter =
    [
        "waehlen", "Waehlen", "waehle", "Waehle", "fuer", "Fuer",
        "verfuegbar", "pruefen", "Pruefen", "pruefe", "Pruefe",
        "ungueltig", "Ungueltig", "ungueltige", "Ungueltige", "ungueltigen", "ungueltiges",
        "aendern", "Aendern", "geaendert", "Geaendert",
        "geaenderte", "veraendert", "unveraendert", "unveraenderlich", "uebernommen", "uebernommene",
        "Uebernommen", "uebernehmen", "Uebernehmen", "uebergeben", "Uebernahme", "Goldpruefung",
        "Goldpruefungs", "Goldfaelle", "Pruefung", "laeuft", "Loeschen",
        "geloescht", "Haltungslaenge", "geoeffnet", "geoeffneten", "Oeffner", "oeffnen",
        "Oeffnen", "geprueft", "gepruefte", "ueber", "uebersprungen", "Uebersprungen",
        "Uebersprungene", "Eintraege", "Protokolleintraege", "Laenge", "bestaetigt", "bestaetigen",
        "Bestaetigen", "Bestaetigung", "bestaetigte", "auswaehlen", "auswaehlbar", "Ausgewaehlte",
        "ausgewaehlt", "ausgewaehlte", "groesser", "waehrend", "Waehrend", "Faelle",
        "Faellen", "beschaedigt", "Beschaedigte", "gehoert", "ueberschrieben", "ueberschreibt",
        "Unvollstaendige", "unvollstaendig", "gewaehlt", "gewaehlten", "ergaenzen", "ergaenzt",
        "Ergaenzt", "verknuepfte", "verknuepfen", "gezaehlt",
        "gueltig", "gueltige", "gueltigen", "gueltiges", "hinzufuegen", "moeglich",
        "moegliche", "naeherung", "Naeherung", "noetig", "persoenliches", "Persoenliche",
        "spaetere", "spaeter", "uebrigen", "uebrige", "vollstaendig", "zusaetzlichen",
        "Zusaetzliche", "Abhaengigkeitspaket", "Bildflaeche", "Bildgroesse", "Dichtheitspruefung", "Flaeche",
        "Geraetesicherheit", "hashgeprueften", "klaeren", "Kostenuebersicht", "Maskenflaeche",
        "Nettobetraege", "Preisaenderungen", "Pruefplatz", "Pruefspur", "Pruefungsfortschritt", "Qualitaetspruefung",
        "Vorschlaege", "Zugehoerige", "zugehoerige", "zugehoerigen", "Zuruecksetzen", "ausdruecklich",
        "ausgefuellt", "gehaengt", "geschuetzt", "geschuetzte", "geschuetzten", "rueckgaengig",
        "temporaere", "temporaeren", "unterstuetzt", "verstaendlich", "wuerde", "zurueckgegeben",
        "zurueckgehaltene", "zuruecknehmen", "naechsten", "Naechstes", "naeher", "benoetigt",
        "enthaelt", "muessen", "laesst", "koennen", "Schaerfe", "trainingsfaehig",
        "Anschluesse", "Fuellung", "laedt", "Laeufe", "Aufraeumen",
        "nachgeruestet", "aufloesbar", "Eigentuemerdossiers", "Saetze", "Saetzen", "Verfuegung",
        "aufgeloest", "schlaegt", "Zeitueberschreitung", "duerfen", "Schaetzung",
    ];

    /// <summary>
    /// Excel-Farbregel-Werte (<c>ExcelReportStyle.Farbregeln</c>,
    /// <c>DataPageDropdownOptionGroupFactory.cs</c>/<c>SchaechteDropdownCommandFactory.cs</c>):
    /// bleiben absichtlich in Ersatzschreibweise, weil der exakte Text verglichen wird.
    /// </summary>
    private static readonly string[] GeschuetzteGanzeZeichenketten =
    [
        "Pruefung bestanden",
        "Pruefung knapp nicht bestanden",
        "Pruefung nicht bestanden (grob undicht)",
    ];

    private static readonly Regex Aufgabe10c1ZeichenkettenLiteral =
        new("\"((?:[^\"\\\\]|\\\\.)*)\"", RegexOptions.Compiled);

    private static readonly Regex Aufgabe10c1WortRegex = new(
        @"\b(" + string.Join("|", Aufgabe10c1BereinigteWoerter.Distinct().Select(Regex.Escape)) + @")\b",
        RegexOptions.Compiled);

    [Fact]
    public void Aufgabe10c1_Bereinigte_Ersatzschreibweisen_fallen_im_gesamten_UI_Projekt_nicht_zurueck()
    {
        var uiRoot = RepoFile("src", "AuswertungPro.Next.UI");
        var treffer = new System.Collections.Generic.List<string>();
        var durchsuchteDateien = 0;

        foreach (var datei in Directory.EnumerateFiles(uiRoot, "*.cs", SearchOption.AllDirectories))
        {
            if (Aufgabe10c1IstAusgenommeneDatei(datei))
                continue;

            durchsuchteDateien++;
            var zeilen = File.ReadAllLines(datei);
            for (var i = 0; i < zeilen.Length; i++)
            {
                var getrimmt = zeilen[i].TrimStart();
                if (getrimmt.StartsWith("//", System.StringComparison.Ordinal) ||
                    getrimmt.StartsWith("[Obsolete(", System.StringComparison.Ordinal))
                    continue;

                // Ein anhaengender Zeilenkommentar wird vor der Literalsuche abgeschnitten —
                // derselbe naive, im Projekt bereits etablierte Ansatz wie in
                // DesignAuditFeinschliffTests. Ein Kommentar ueber ein bereits erledigtes
                // Wort ("// war Ausfuehrt...") darf den Waechter sonst nicht anschlagen lassen.
                var zeileOhneKommentar = zeilen[i];
                var kommentarAb = zeileOhneKommentar.IndexOf("//", System.StringComparison.Ordinal);
                if (kommentarAb >= 0)
                    zeileOhneKommentar = zeileOhneKommentar[..kommentarAb];

                foreach (Match m in Aufgabe10c1ZeichenkettenLiteral.Matches(zeileOhneKommentar))
                {
                    var inhalt = m.Groups[1].Value;
                    if (GeschuetzteGanzeZeichenketten.Contains(inhalt))
                        continue;

                    var pruefbar = Aufgabe10c1OhneAusdruecke(inhalt);

                    // Nur ein Leerzeichen im pruefbaren Rest deutet auf einen echten
                    // Anzeigetext hin — derselbe Grundsatz wie in
                    // Sichtbare_Laufzeittexte_tragen_echte_Umlaute. Ein einzelnes Wort ohne
                    // Leerzeichen ist im ganzen Projekt viel eher ein Schluessel/Tag/Datenwert
                    // (siehe Fix-Runde 1 der Aufgabe-10c1-Review) und wird deshalb hier bewusst
                    // nicht automatisch geprueft.
                    if (!pruefbar.Contains(' '))
                        continue;

                    foreach (Match wort in Aufgabe10c1WortRegex.Matches(pruefbar))
                        treffer.Add($"{Path.GetFileName(datei)}:{i + 1}: \"{wort.Value}\" in \"{inhalt}\"");
                }
            }
        }

        Assert.True(durchsuchteDateien > 200,
            $"Nur {durchsuchteDateien} Dateien durchsucht — Pfad/Ausschluss pruefen (Waechter darf nicht leerlaufen).");
        Assert.True(treffer.Count == 0,
            "Ersatzschreibweise im UI-Projekt zurueckgefallen (Aufgabe 10c1):\n" + string.Join("\n", treffer));
    }

    private static bool Aufgabe10c1IstAusgenommeneDatei(string pfad)
        => pfad.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", System.StringComparison.OrdinalIgnoreCase)
        || pfad.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", System.StringComparison.OrdinalIgnoreCase)
        || Path.GetFileName(pfad).Contains("WebGis", System.StringComparison.Ordinal)
        || Path.GetFileName(pfad) == "ExportWebGisBereich.cs";

    /// <summary>
    /// Ersetzt jeden Interpolations-/Formatausdruck <c>{...}</c> (samt verschachtelter Klammern)
    /// durch gleich viele Leerzeichen, damit ein Eigenschafts-/Variablenname wie
    /// <c>{result.BereitsVollstaendig}</c> nie als Beschriftungstext geprueft wird. Escapte
    /// doppelte Klammern <c>{{</c>/<c>}}</c> (literales einzelnes Zeichen) zaehlen nicht als
    /// Ausdrucksanfang.
    /// </summary>
    private static string Aufgabe10c1OhneAusdruecke(string inhalt)
    {
        var ergebnis = new System.Text.StringBuilder(inhalt.Length);
        var i = 0;
        while (i < inhalt.Length)
        {
            if (inhalt[i] == '{' && i + 1 < inhalt.Length && inhalt[i + 1] == '{')
            {
                ergebnis.Append("  ");
                i += 2;
                continue;
            }

            if (inhalt[i] == '}' && i + 1 < inhalt.Length && inhalt[i + 1] == '}')
            {
                ergebnis.Append("  ");
                i += 2;
                continue;
            }

            if (inhalt[i] == '{')
            {
                var tiefe = 1;
                var j = i + 1;
                while (j < inhalt.Length && tiefe > 0)
                {
                    if (inhalt[j] == '{') tiefe++;
                    else if (inhalt[j] == '}') tiefe--;
                    j++;
                }

                ergebnis.Append(' ', j - i);
                i = j;
                continue;
            }

            ergebnis.Append(inhalt[i]);
            i++;
        }

        return ergebnis.ToString();
    }
}
