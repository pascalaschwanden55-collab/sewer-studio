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
}
