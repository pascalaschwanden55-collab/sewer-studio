using System.Collections.Generic;
using System.Linq;

namespace AuswertungPro.Next.UI.Services;

/// <summary>
/// Optikanalyse 28.09.2026, Aufgabe 6 («Hilfe-Menü, F1, Tastenkürzel, Handbuch»): EIN Abschnitt des
/// Handbuchs. <see cref="Schluessel"/> entspricht bei einer Programmseite genau dem
/// <c>NavItem.Title</c> aus <c>ShellViewModel.NavItems</c> (z. B. "Uebersicht", "Schaechte") - so
/// findet F1 den Abschnitt der gerade gewählten Seite und der Wächter
/// <c>HandbuchAbdeckungTests</c> kann jede Seite der Leiste gegen einen vorhandenen Abschnitt prüfen.
/// <see cref="IstFachlich"/> markiert den einen Entwicklerabschnitt am Ende («Für Fachleute
/// (technisch)») - er ist kein Seitenabschnitt und steht im Fenster standardmässig eingeklappt.
/// </summary>
public sealed record HandbuchAbschnitt(string Schluessel, string Titel, string Text, bool IstFachlich = false);

/// <summary>
/// Reine, WPF-freie Textquelle des Handbuchs (ehemals Einstellungen -&gt; Hilfe). Jeder Abschnitt
/// beschreibt nur, was auf der jeweiligen Seite tatsächlich vorhanden ist (gegen die XAML geprüft,
/// 28.09.2026) - kurzer Zweck plus die wichtigsten Abläufe für einen Kanalinspekteur, keine
/// Programmierdetails. Entwicklermaterial (Umgebungsvariablen, Sidecar, Modell-Registry,
/// Merge-Engine) steht gesammelt im letzten, technischen Abschnitt.
/// </summary>
public static class HandbuchInhalt
{
    public const string FachleuteSchluessel = "Fachleute";

    public static IReadOnlyList<HandbuchAbschnitt> Abschnitte { get; } =
    [
        new("Uebersicht", "Übersicht",
            "Startseite bei offenem Projekt: vier Kennzahlen (Haltungen, Schächte, offene Prüfungen, " +
            "Zustandsklassen-Verteilung), die häufigsten Schadensgruppen und der Knopf «Nächste " +
            "Haltung prüfen», der direkt zur ersten noch offenen Haltung mit Video springt. Ein Klick " +
            "auf eine Schadensgruppe filtert die Haltungsliste danach. «Haltungen öffnen» springt zur " +
            "Datentabelle, «Vorschau-PDF» erzeugt eine kurze Projektübersicht als PDF.\n\n" +
            "Über Ansicht -> Klassische Übersicht lässt sich stattdessen die ältere Startseite mit " +
            "Projektliste, Vorschau und Vorschau-PDF einschalten - ohne Projekt ist sie ohnehin der " +
            "Startbildschirm (neues Projekt anlegen oder ein vorhandenes öffnen)."),

        new("Projekt", "Projekt",
            "Stammdaten des offenen Projekts: Name, Beschreibung, Auftraggeber, Gemeinde, " +
            "Inspektionsdatum, Firma und Kontaktperson. Diese Angaben erscheinen auf allen " +
            "exportierten Protokollen, Listen und Dossiers - hier einmal sauber erfassen, statt sie " +
            "später in jedem Export einzeln zu korrigieren."),

        new("Haltungen", "Haltungen",
            "Die zentrale Tabelle aller Kanalabschnitte (Haltungen). Über den Spaltenwahl-Schalter " +
            "lassen sich Ansichten wie «Kompakt», «Stammdaten», «Bewertung», «Sanierung», «Kosten» " +
            "oder «Alle Spalten» wählen; vier eigene Spalten zeigen KI-Stand, Prüfstand, ob ein Video " +
            "hinterlegt ist und ob ein Protokoll vorliegt. F3 springt in die Suche.\n\n" +
            "Rechtsklick auf eine Zeile öffnet Beobachtungen, startet die Videoanalyse oder das " +
            "Abspielen des Videos, druckt das Protokoll, öffnet das Original-PDF oder die " +
            "Sanierungsmassnahmen. Der Knopf «Weitere Aktionen» bündelt seltenere Wege in fünf " +
            "Untermenüs: Daten abgleichen (Medien suchen, WebGIS, GeoShop, QGIS, Strassennamen), " +
            "Bearbeiten (Sanierungsmassnahmen, KI-Optimierung, Sanierungsvorschlag, Hydraulik " +
            "berechnen - Sanierungsmassnahmen steht bewusst sowohl im Rechtsklick-Kontextmenü als " +
            "auch hier), Reihenfolge (Zeile verschieben oder an eine Position setzen), Ansicht " +
            "(Ansicht wechseln, anpassen, abdocken) sowie Ausgabe (Hydraulik-PDF, Haltungsdossier " +
            "drucken). Statt der Tabelle lässt sich über Weitere Aktionen -> Ansicht auch die " +
            "aufklappbare Listenansicht einer Haltung wählen."),

        new("Schaechte", "Schächte",
            "Tabelle aller Schacht-Inspektionspunkte, gleich aufgebaut wie Haltungen: Spaltenwahl, " +
            "Statusspalten, Suche, Rechtsklick-Kontextmenü (Details, Sanierungsmassnahmen, Protokoll-" +
            "PDF, Gehe zu Ordner) und «Weitere Aktionen» mit denselben fünf Untermenüs wie bei den " +
            "Haltungen: Daten abgleichen (Vom WebGIS holen, GeoShop, QGIS, Stammdaten aus PDFs " +
            "ergänzen, Protokoll neu einlesen, Feldnamen aufräumen, Strassennamen ergänzen), " +
            "Bearbeiten (Sanierungsmassnahmen), Reihenfolge, Ansicht sowie Ausgabe (Protokoll-PDF, " +
            "Gehe zu Ordner) - Sanierungsmassnahmen, Protokoll-PDF und Gehe zu Ordner stehen bewusst " +
            "sowohl im Rechtsklick-Kontextmenü als auch unter «Weitere Aktionen». Neu, Löschen, " +
            "Verschieben und die Detailansicht funktionieren wie bei den Haltungen. Auch hier kann " +
            "statt der Tabelle die aufklappbare Listenansicht gewählt werden."),

        new("Import", "Import",
            "Holt Inspektionsdaten ins Projekt. Oben stehen die normalen Wege: PDF-Protokolle " +
            "(einzeln oder ganze Ordner), XTF/DSS-Katasterdateien, WinCan-, IBAK- und KINS-Projekte " +
            "sowie SchachtPro-QR-Codes aus Fotos. Darunter liegen Sonderfälle für einzelne Dateien. " +
            "Ein Import zeigt vor der Übernahme immer eine Vorschau; «Nur fehlende Felder auffüllen» " +
            "ergänzt leere Angaben, ohne vorhandene zu überschreiben. Nach jedem Import steht ein " +
            "Bericht bereit, der zeigt, was übernommen wurde und was nicht zugeordnet werden konnte."),

        new("Export", "Export",
            "Bündelt Excel-Export, Verteilung und den Kataster-/WebGIS-Austausch. Excel exportiert " +
            "Haltungen und Schächte nach einer festen Vorlage. Die Verteilung sortiert exportierte " +
            "Protokolle und Medien in Ordner je Haltung, Schacht oder Prüfstatus. Für den Kataster " +
            "stehen «Bestehende Katasterdaten aktualisieren» (empfohlen, wenn eine Importkopie " +
            "vorliegt) und «XTF erstellen» als vollständiger Neu-Export zur Wahl, dazu die " +
            "WebGIS-Übertragung mit Anmelden, Prüfen/Schreiben und Holen."),

        new("Medienkonflikte", "Medienkonflikte",
            "Zeigt Videos und PDFs, die sich beim Import keiner Haltung eindeutig zuordnen liessen - " +
            "etwa weil der Dateiname mehrdeutig war oder zu keiner bekannten Haltung passte. Hier " +
            "lässt sich jede Datei manuell einer Haltung zuweisen oder als nicht zugehörig markieren, " +
            "bevor sie im Projekt fehlt."),

        new("Druckcenter", "Druckcenter",
            "Filterbare Druckansicht mit Kosten-, Eigentümer- und Positionszusammenfassung. Über " +
            "Suche, Eigentümer, Material, Status und Jahr lässt sich die Liste eingrenzen; dazu " +
            "kommen Kennzahlen zu Sanierungsquote und Kostenverteilung sowie das NPK-135-" +
            "Leistungsverzeichnis. Das Ergebnis lässt sich als PDF ausgeben."),

        new("Dossiers", "Eigentümerdossiers",
            "Baut je Liegenschaft ein Eigentümerdossier: die vorhandenen Word-Vorlagenfelder " +
            "(Ausgangslage, Änderungswesen, Eigentümer, Themen wie Schäden oder Sanierungskonzept) " +
            "füllen, dazu automatisch das Zustandsklassen-Erklärblatt sowie eine Haltungs- und " +
            "Schachtliste erzeugen und schliesslich die Original-Protokolle als Beilagen anhängen. " +
            "Die Vorschau zeigt genau das fertige PDF, bevor «Alles zu einem PDF» es tatsächlich " +
            "schreibt. Mehrere Liegenschaften lassen sich auch in einem Stapel anlegen."),

        new("Sanierungs-Matrix", "Sanierungs-Matrix",
            "Eine Tabelle mit einer Zeile je Haltung: pro Haltung wird genau EINE Hauptsanierung " +
            "gewählt, Meter, DN und Anschlüsse übernimmt das Programm automatisch aus den " +
            "Stammdaten. Unten steht die Gesamtsumme exkl. MWST, «Preise/Katalog» öffnet den " +
            "gemeinsamen Preiskatalog, «Speichern» übernimmt die Auswahl ins Projekt. Ein Doppelklick " +
            "auf eine Zeile öffnet die Einzelhaltung mit allen Positionen im Detail."),

        new("Schacht-Matrix", "Schacht-Matrix",
            "Dieselbe Matrix wie bei den Haltungen, nur je Schacht: eine Hauptmassnahme pro Zeile, " +
            "Gesamtsumme exkl. MWST unten, «Neu laden» verwirft nicht gespeicherte Änderungen und " +
            "«Speichern» übernimmt die Auswahl."),

        new("Schattenauswertung", "Schattenauswertung",
            "Stellt das eigene fachliche Urteil neben das der KI, ohne dass dabei Projektdaten " +
            "verändert werden - ein reiner Vergleich zur Selbstkontrolle, wie gut die KI-Einschätzung " +
            "mit der eigenen Bewertung übereinstimmt."),

        new("VSA", "VSA-Bewertung",
            "Berechnet die Zustandsklasse (0-4) für alle Haltungen und Schächte nach VSA-KEK 2020 " +
            "aus den erfassten Schadenscodes. Der schlechteste Einzelbefund bestimmt die " +
            "Gesamtklasse einer Haltung; Kanäle und Schächte verwenden dabei getrennte Regelwerke."),

        new("Diagnose", "Diagnose",
            "Ein laufendes Protokoll der Programmmeldungen, Fehler und KI-Diagnosen in Echtzeit. " +
            "Nützlich, wenn etwas nicht wie erwartet funktioniert und man Pascal oder dem " +
            "Entwickler-Support eine genaue Fehlermeldung mitteilen möchte."),

        new("Einstellungen", "Einstellungen",
            "Alle Programmeinstellungen in Gruppen: Projektpfade und Ordner, Sicherung " +
            "(automatische Speicherung, Wiederherstellungspunkte, Vollsicherung), Video-Player, " +
            "KI-Verbindung sowie dieser Hilfe-Bereich. Ein Suchfeld oben findet die passende Gruppe " +
            "über Stichworte, auch bei Umlauten."),

        new(FachleuteSchluessel, "Für Fachleute (technisch)",
            "Dieser Abschnitt richtet sich an Entwickler und Techniker, nicht an den täglichen " +
            "Gebrauch im Feld.\n\n" +
            "KI-Konfiguration über Umgebungsvariablen (alle optional, Standardwerte greifen sonst): " +
            "SEWERSTUDIO_AI_ENABLED (0/1) schaltet die KI-Funktionen, SEWERSTUDIO_OLLAMA_URL " +
            "(Standard http://localhost:11434) ist die Adresse des lokalen Ollama-Dienstes, " +
            "SEWERSTUDIO_AI_VISION_MODEL/SEWERSTUDIO_AI_TEXT_MODEL wählen das Bild- bzw. Textmodell, " +
            "SEWERSTUDIO_AI_EMBED_MODEL das Embedding-Modell der Wissensbank, " +
            "SEWERSTUDIO_SIDECAR_URL (Standard http://localhost:8100) die Adresse des Python-" +
            "Sidecars für die YOLO/DINO/SAM-Bildpipeline und SEWERSTUDIO_FFMPEG den Pfad zu FFmpeg " +
            "für die Video-Bildextraktion.\n\n" +
            "Sidecar: eigener Python-Prozess, der YOLO (Lokalisierung), Grounding DINO (Beschreibung) " +
            "und SAM (Segmentierung) über HTTP bereitstellt; C# steuert die Pipeline, Zuordnung und " +
            "Qualitätsprüfung. Qwen läuft separat über Ollama für Text- und Bildbeurteilung.\n\n" +
            "Modell-Registry: Jedes eingesetzte KI-Modell ist versioniert und trägt einen " +
            "Qualifikationsstatus (u. a. «qualified»/«not_deployed»); nur ausdrücklich freigegebene " +
            "Modelle werden im normalen Programmbetrieb verwendet.\n\n" +
            "Merge-Engine: Alle Importwege (PDF, XTF, WinCan, IBAK, KINS, GeoShop, WebGIS) führen " +
            "importierte Werte über dieselbe Regel mit dem lokalen Datenbestand zusammen - " +
            "Handeingaben sind grundsätzlich geschützt, Katasterquellen haben die niedrigste " +
            "Priorität und werden nur bei einer leeren Zielangabe oder durch eine neuere, " +
            "unmarkierte Quelle ersetzt.", IstFachlich: true),
    ];

    /// <summary>
    /// Findet den Abschnitt zu einem Seitenschlüssel (z. B. dem <c>NavItem.Title</c> der gerade
    /// gewählten Seite). Unbekannt oder leer -> der erste Abschnitt («Übersicht») als Startpunkt.
    /// </summary>
    public static HandbuchAbschnitt Finde(string? schluessel)
        => Abschnitte.FirstOrDefault(a => a.Schluessel == schluessel) ?? Abschnitte[0];
}
