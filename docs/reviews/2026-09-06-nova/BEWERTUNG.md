# Prüfung der Nova-Entwürfe

Stand: 6. September 2026

**Urteil:** Die Gestaltung ist eine gute Grundlage. Vor der Übernahme brauchen Datenauswahl, Arbeitsabläufe und Platzaufteilung eine Überarbeitung. Ein Niveau von 9/10 ist mit diesen HTML-Dateien noch nicht nachgewiesen.

Geprüft wurden die beiden Desktop-Dateien vom 6. September 2026. In der Komplettversion wurden alle 15 Seiten und drei Fenster geöffnet. Ausgewählte Arbeitsabläufe wurden mit Browser-Automatisierung nachgeprüft. Nicht alle 272 Schaltflächen wurden einzeln auf Funktion geprüft. Dies ist eine Prüfung der HTML-Entwürfe, keine neue Vollprüfung der WPF-Anwendung.

Die Bildschirmprüfung nutzte Chromium bei 1440 × 1000, 1366 × 768 und ergänzend 1000 × 768. Für lesbare Umlaute wurde nur den Prüfkopien eine UTF-8-Angabe vorangestellt; das restliche HTML blieb unverändert. Echtes Windows-DPI, Screenreader, reale Importe und KI-Leistung wurden dabei nicht getestet. Es wurden keine Produktionsdateien geändert.

## Beibehalten

Hell/Dunkel/System, fachliche Gliederung, Zustandsfarben Z0–Z4 und zusammengehörige Eingabebereiche. Die kleine Vorschau zeigt dieselbe Grundgestaltung; die Komplettversion erweitert sie um Arbeitsseiten und Fenster.

## Befunde

### N01 · Priorität 1 · Auswahl und Daten müssen zusammenpassen

**Beobachtet:** Nach Auswahl von 07.6588-6587 wechseln nur die Überschriften. Die Felder zeigen weiterhin 78998-79002, Beton, DN 300 und 42.30 m. Die ausgewählte Tabellenzeile enthält PVC, DN 250 und 28.10 m.

**Vorschlag:** Eine gemeinsame Auswahl steuert Tabelle, Detailansicht, Eingabefelder und Medien. Ungespeicherte Änderungen sichtbar machen.

**Abnahme:** Zwischen zwei unterschiedlichen Haltungen wechseln: Alle Werte und Medien gehören jeweils zum gewählten Objekt. Änderungen lassen sich speichern, verwerfen oder weiterbearbeiten.

**Nachweis:** Komplett: Zeilen 600–645 und 1399–1405; Browser: record und rowAfter.

### N02 · Priorität 1 · Jede Suche bleibt auf ihrer Seite

**Beobachtet:** Die Feldsuche „Baujahr“ bei Haltungen blendet auch Schachtfelder aus: Nur eines von 29 Feldern bleibt sichtbar. Ein Ansichtswechsel bei Haltungen löscht ausserdem die Auswahlmarkierung der Schachtansicht.

**Vorschlag:** Suchen, Aufklappen und Ansichten auf die jeweilige Seite und Schaltflächengruppe begrenzen. Die eigene Schachtsuche anschliessen.

**Abnahme:** Bei Haltungen filtern und Ansichten wechseln: Schachtfelder und Schachtansicht bleiben unverändert. Zurücksetzen wirkt nur auf die aktive Suche.

**Nachweis:** Komplett: Zeilen 1369–1388; Browser: shaftLabelsBefore/After und chipsBefore/After.

### N03 · Priorität 1 · Mehr Platz für Tabelle, Video und Prüfung

**Beobachtet:** Bei 1366 × 768 bleibt mit geöffnetem Formular eine vollständige Tabellenzeile sichtbar. Im Training Studio liegen bei 1440 × 1000 rund 259 von 320 Pixeln der rechten Spalte ausserhalb des Sichtbereichs. Beim Player liegen Transporttasten unterhalb des ersten sichtbaren Ausschnitts.

**Vorschlag:** Verschiebbare Trennlinien, einklappbare Details und eine kompakte Werkzeugleiste vorsehen. Video an verfügbare Höhe anpassen. Wiedergabe und Freigabe sichtbar halten.

**Abnahme:** Vorgeschlagenes Ziel: mindestens sechs Datenzeilen im Standardzustand bei 1366 × 768. Player-Steuerung und Trainingsfreigabe bleiben ohne Seiten-Scrollen erreichbar.

**Nachweis:** Komplett: .stack/.drawer, Player und Training Studio ab Zeile 1295; Zusatzprobe: rows und training; Bildschirmbilder.

### N04 · Priorität 1 · Die Übersicht muss richtig rechnen

**Beobachtet:** „Dringend (Z0/Z1)“ nennt 12 Haltungen, die Legende nennt 8 + 15 = 23. 168 von 239 ergeben 70,29 %, auf ganze Prozent gerundet 70 %; angezeigt werden 71 %. Beide HTML-Dateien zeigen diese Widersprüche.

**Vorschlag:** Alle Zahlen aus denselben Beispieldaten berechnen. Klar benennen, ob „ausgewertet“ einen KI-Durchlauf oder eine fachliche Prüfung meint.

**Abnahme:** Zähler, Diagramm und gefilterte Liste stimmen überein. Fortschritt zeigt zusätzlich Zähler und Nenner. Fehlende Werte sind als unbekannt erkennbar.

**Nachweis:** Komplett: Übersicht, insbesondere Zeilen 491 und 515–516; Vorschau: Übersicht. Eigene Nachrechnung.

### N05 · Priorität 1 · Arbeitsablauf vollständig anklickbar machen

**Beobachtet:** „Nächste Haltung prüfen“ bleibt im Browser auf der Übersicht. Die globale Suche ist eine beschriftete Fläche. Player → Ereignis erfassen → Übernehmen schliesst alle Fenster; der Player kehrt nicht zurück.

**Vorschlag:** Einen vollständigen Weg mit Beispieldaten umsetzen: Objekt wählen, Video prüfen, Ereignis erfassen, übernehmen, nächste Haltung. Noch offene Aktionen ausdrücklich kennzeichnen.

**Abnahme:** Der Weg funktioniert mit Maus und Tastatur. Nach Codierung bleibt die gewählte Haltung samt Videoposition erhalten. Kein Knopf meldet einen nur vorgetäuschten Dateiimport oder Speichervorgang.

**Nachweis:** Komplett: Hauptaktion und Suche; Zeilen 1396–1398; Browser: nextButtonPage und windowsAfterCoding.

### N06 · Priorität 2 · Lesen soll auch nach Stunden leicht bleiben

**Beobachtet:** Tabellenköpfe und Navigationsgruppen verwenden 10,5 Pixel. Im sichtbaren Überblick wurde Text mit 9 Pixeln gefunden. Viele Beschriftungen sind sehr blass. Die Projektregel setzt für normale Programmtexte mindestens 11 voraus.

**Vorschlag:** Für Beschriftungen meist 12–13, für Daten gut lesbare 13–15 verwenden. Glasflächen bei Tabellen und Formularen beruhigen. Zustände zusätzlich durch Text oder Symbole erklären.

**Abnahme:** Normale Texte unterschreiten die Projektgrenze nicht. Textkontraste in Hell und Dunkel messen; mindestens 4,5:1 für normalen Text als Ziel. Die bestehenden WPF-Schriftgrössen und Farbvorgaben wiederverwenden.

**Nachweis:** Komplett: Zeilen 65 und 321; Browser: minTextSize; CLAUDE.md ab Zeile 724. Kontrast ist hier kein vollständiger WCAG-Konformitätstest.

### N07 · Priorität 2 · Tastatur und Dialoge zuverlässig bedienen

**Beobachtet:** Enter auf dem fokussierten Export-Navigationseintrag öffnet die Exportseite nicht. Die drei Fenster haben keine Dialogrolle. Nach dem Öffnen liegt der Tastaturfokus ausserhalb des Fensters.

**Vorschlag:** Echte Links oder Schaltflächen einsetzen. Dialoge benennen, Fokus hineinsetzen und dort halten. Beim Schliessen zum auslösenden Element zurückkehren.

**Abnahme:** Alle Hauptaktionen funktionieren per Tastatur. Ein sichtbarer Fokus zeigt die Position. Esc schliesst die oberste Dialogebene; die vorherige Aufgabe bleibt erhalten.

**Nachweis:** Komplett: Navigation, .win und Zeilen 1396–1398; Browser: dialog und keyboardAfterExportEnter.

### N08 · Priorität 2 · „Ruhig“ muss wirklich ruhig sein

**Beobachtet:** Der Hintergrund zeichnet auch nach Klick auf „ruhig“ weiter. Zwei Canvas-Aufnahmen im Abstand von 350 ms unterscheiden sich. Die Animationsschleife prüft die Einstellung nicht.

**Vorschlag:** Dekorative Bewegung wirklich anhalten, die Einstellung speichern und die Windows-Vorgabe für reduzierte Bewegung beachten. Verdeckte Ansichten sollen keine unnötige Dekoration berechnen.

**Abnahme:** Im ruhigen Modus bleibt das dekorative Bild unverändert. Nach Neustart gilt dieselbe Einstellung. Leistungsgewinn erst nach Messung beziffern.

**Nachweis:** Komplett: setMotion ab Zeile 1357 und step in Zeile 1462; Browser: canvasStillChangesWhenQuiet.

### N09 · Priorität 2 · Die Vorschau sauber als Datei liefern

**Beobachtet:** Beiden Dateien fehlen DOCTYPE, Zeichensatz, Sprache und viewport-Angabe. Beim lokalen Abruf ohne Zeichensatz-Angabe wurden Umlaute falsch dargestellt. Beide laufen im Browser im älteren BackCompat-Modus. Drei Schriftfamilien werden extern geladen.

**Vorschlag:** Vollständige HTML-Datei mit UTF-8, lang="de" und viewport liefern. Lokale Schriften verwenden und den direkten Offline-Aufruf prüfen.

**Abnahme:** Doppelklick und lokaler Webserver zeigen korrekte Umlaute. document.compatMode ist CSS1Compat. Die Grundbedienung benötigt keine Internetverbindung.

**Nachweis:** Beide Dateien ab Zeile 1; Browser: base und preview. Die Darstellung im Claude-Rahmen kann davon abweichen.

### N10 · Priorität 2 · Fachliche Arbeit vor technischen Einzelheiten

**Beobachtet:** Modellnamen, GPU-Anzeige, interne Importbegriffe und KI-Prozentwerte nehmen dauerhaft Platz ein. Bezeichnungen wie „KI-Sicherheit“ und „Abnahme“ erklären ihre Bedeutung nicht ausreichend.

**Vorschlag:** Im Alltag „Analyse bereit“, „Prüfung nötig“ oder „Fehler beheben“ zeigen. Modell- und Diagnosewerte aufklappbar machen. KI-Vorschlag, menschliche Bestätigung und Trainingsfreigabe eindeutig unterscheiden.

**Abnahme:** Ein Anwender erkennt die nächste Aufgabe und ihren Status ohne Modellwissen. Exporttexte unterscheiden eine revidierte XTF-Datei von einem Rückabgleich mit GEONIS.

**Nachweis:** Komplett: Statusleiste, Import, Export, Player und Training Studio. Dies ist eine Gestaltungsempfehlung, kein Nachweis fehlender Backend-Funktionen.

Die Kontrastvorgabe stammt aus [W3C: Kontrast für Text](https://www.w3.org/WAI/WCAG22/Understanding/contrast-minimum.html). Die empfohlenen Dialogabläufe folgen dem [W3C-Dialogmuster](https://www.w3.org/WAI/ARIA/apg/patterns/dialog-modal/). Diese Empfehlungen ersetzen keine vollständige Prüfung auf Barrierefreiheit.

## Reihenfolge der Umsetzung

1. **Verlässliche Abläufe (N01, N02, N04, N05):** Datenauswahl, Suchen, Summen und Rückkehr aus Dialogen reparieren. Fertig, wenn der Weg „Haltung wählen → prüfen → codieren → übernehmen → nächste Haltung“ mit konsistenten Daten funktioniert.

2. **Guter Arbeitsplatz (N03, N06, N07, N08):** Platzaufteilung, Lesbarkeit, Tastatur und ruhigen Modus überarbeiten. Fertig, wenn Liste, Video und Prüfaktionen bei kleinen Desktop-Fenstern nutzbar bleiben und die Bedienprüfungen bestehen.

3. **Vorlage sauber abschliessen (N09, N10):** Offline-Datei liefern, Zustände verständlich erklären und offene Funktionen kennzeichnen. Fertig, wenn alle 15 Seiten und drei Fenster geprüft sind und jeder offene Punkt ausdrücklich aufgeführt ist.

4. **Schrittweise ins Programm übernehmen (Separater Umsetzungsschritt):** Zuerst eine echte Haltungsseite umsetzen, fachlich prüfen, danach weitere Seiten angleichen. Fertig je Seite, wenn bestehende Abläufe und passende Tests bestehen; Windows-Skalierung und Originaldateischutz bleiben nachgewiesen.

## Nachprüfung

Die Messwerte stehen in [browserpruefung.json](nachweise/browserpruefung.json), die Dateifingerabdrücke in [originaldateien.json](nachweise/originaldateien.json). Beide Desktop-Originale sind unverändert. Die Browser-Prüfskripte liegen ebenfalls im Nachweisordner. Ihre URLs verweisen auf die lokal bereitgestellten UTF-8-Prüfkopien unter 127.0.0.1:8804; die Skripte sind kein vollständiger Anwendungstest.

Der [fertige Claude-Prompt](CLAUDE-PROMPT.md) beschreibt die nächste Überarbeitung. Der [HTML-Überblick](ueberblick.html) erklärt die wichtigsten Punkte mit Bildschirmbildern.
