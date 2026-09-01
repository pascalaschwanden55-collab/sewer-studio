# Prüfmittel-Audit SewerStudio

**Stand:** 1. September 2026  
**Ziel:** Nicht nochmals wahllos nach klassischen Fehlern suchen, sondern prüfen,
ob Tests, Fehlermeldungen und technische Wächter die Wahrheit sagen.

## Kurzurteil

Die Prüfung hat einen echten Parallelitätsfehler, mehrere irreführende
Erfolgsmeldungen und deutliche Lücken in wichtigen Tests gefunden. Die gefundenen
Fehler sind im Arbeitsstand behoben und durch gezielte Tests geschützt.

Die wichtigste offene Lücke ist nicht mehr ein einzelner Codefehler: Im Repository
fehlt weiterhin ein kleiner, anonymisierter Bestand echter Projekte für einen
wiederholbaren Import-/Export-Vergleich und für belastbare Zeitmessungen.
Kundenoriginale wurden für dieses Audit bewusst nicht geöffnet.

## 1. Sind grüne Tests verlässlich?

### Befund

`StaTestRunner` gab allen 70 Aufrufen in 24 WPF-Testdateien dasselbe feste
15-Sekunden-Limit. Kein Aufruf übergab ein eigenes Limit. Die Zeit wurde während
des parallel laufenden Testprojekts als echte Uhrzeit gemessen. Dadurch konnte ein
korrekter, aber unter Maschinenlast langsamer Test mit der falschen Behauptung
„reagiert nicht mehr“ abbrechen.

Die vorangegangene Wiederholungsprüfung war fünfmal grün, darunter ein kalter Lauf
und ein Lauf ohne Parallelität. Das widerlegt den Mechanismus nicht: Der kalte Lauf
war rund 40 Prozent langsamer als die warmen Läufe.

### Korrektur

- Standardlimit von 15 auf 60 Sekunden erhöht.
- Fehlermeldung nennt nun das konkrete Limit und unterscheidet zwischen Blockade
  und hoher Maschinenlast.
- Ein Wächtertest schützt den neuen Standardwert.

Das höhere Limit verlangsamt normale Testläufe nicht. Es wirkt nur, wenn ein Test
tatsächlich nicht rechtzeitig endet.

## 2. Erkennen wichtige Tests absichtlich eingebaute Fehler?

Die Prüfung erfolgte gezielt mit Stryker.NET 4.16.0. Das Werkzeug verändert
Produktivcode probeweise und prüft, ob ein Test dadurch rot wird. Gemessen wurden
nur fachlich wichtige, kleine Bereiche; ein Lauf über die ganze Lösung wäre zu
langsam und zu ungenau. Methodik und Bedeutung der Zustände sind in der
[offiziellen Stryker-Dokumentation](https://stryker-mutator.io/docs/stryker-net/configuration/)
beschrieben.

| Bereich | Vorher | Nachher | Ergebnis |
|---|---:|---:|---|
| Fachzahl-/Kostenparser | 75,19 % | 80,62 % | zusätzliche Kultur-, Gruppierungs- und Fehlerfälle geschützt |
| Haltungsname / `HoldingIdNormalizer` | 68,75 % | 92,50 % | Datumsstücke, OCR-Verklebungen, Schlüsselreihenfolge und Grenzen geschützt |
| VSA-Zuordnung | 63,03 % | 82,26 % | Grenzwerte, Familien, Bogen-Veto, Ersatzcodes und Katalogprüfung geschützt |
| Import-/Schreibpfad-Wächter | 61,96 % | 89,13 % | Datei/Ordner, Laufwerkswurzel, UNC, Junction, fehlende Eltern und ungültige Zeichen geschützt |

Die verbleibenden Mutanten sind nicht automatisch Fehler. Darunter sind
gleichwertige Änderungen, absichtlich defensive Rückfälle und Zweige, die nur auf
bestimmten Windows-Dateisystemen erreichbar sind. Trotzdem zeigen die
Vorher-Werte klar: Die alte Anzahl grüner Tests überschätzte die tatsächliche
Fehlererkennung an mehreren wichtigen Stellen.

### Einschränkung des Werkzeugs

Stryker erzeugt in diesem Repository zunächst Mutanten für das ganze gewählte
Quellprojekt und filtert erst danach auf die Zieldateien. Beim Infrastrukturprojekt
entstanden dadurch 64'669 Mutanten; 7'709 künstliche Varianten waren nicht
kompilierbar und wurden übersprungen. Das sind keine Buildfehler im SewerStudio-Code.
Für einen dauerhaften CI-Einsatz sollte die Mutationprüfung deshalb als kleine,
gezielte Nachtprüfung bleiben und nicht als Vollprüfung bei jedem Commit laufen.

## 3. Erfährt der Benutzer die Wahrheit?

Ein Roslyn-basierter Suchlauf hat 1'387 `catch`-Blöcke klassifiziert:

| Rohklasse | Anzahl | Bedeutung |
|---|---:|---|
| wirft weiter / Abbruch | 291 | Fehler wird nicht verschluckt |
| gemeldet | 279 | Log, sichtbare Warnung oder Fehlerkanal erkannt |
| Rückfall | 451 | Ersatzwert oder Ergebnisobjekt |
| Aufräumen | 6 | reines Aufräumen erkannt |
| zunächst „still“ | 360 | manuell zu beurteilen |

Die 360 sind **keine 360 bestätigten Fehler**. Die automatische Einordnung erkennt
zum Beispiel Warnlisten, Fehler-Ergebnisobjekte und absichtlich fail-closed
behandelte Dateiprüfungen nicht zuverlässig. Die Liste diente als Inventar für die
manuelle Auswahl der wirkungsstarken Fälle.

### Bestätigte und behobene Fälle

- Der Dichtheits-Export meldete Erfolg, obwohl der Katasterabgleich fehlgeschlagen
  war. Ergebnis und Status enthalten nun eine sichtbare Warnung.
- Die Bereinigung doppelter Primärschäden konnte beim Import scheitern, ohne dass
  Zusammenfassung oder Importprotokoll dies zeigten. Der Import läuft weiterhin,
  meldet den Teilfehler aber sichtbar und im Protokoll.
- Automatisches Speichern im Training Center meldet Fehler nun im Status und Log.
- Laden und Speichern der Hydraulik-Einstellungen protokolliert Fehler nun.
- Der Chromium-Installationscheck unterscheidet im Log zwischen „nicht installiert“
  und „Installationsordner konnte nicht gelesen werden“.
- Fehler beim Stoppen von Player-Timern, Schließen eines abgedockten Fensters und
  Beenden eines abgebrochenen Playwright-Prozesses hinterlassen nun eine Spur.

Ein neuer Wächter verhindert vollständig leere `catch`-Blöcke im Produktivcode.
Er ersetzt keine fachliche Beurteilung, verhindert aber den schlechtesten Fall:
einen Fehler ohne Rückgabe, Warnung oder Protokoll.

## 4. Veränderlicher gemeinsamer Zustand über Threads

Der Suchlauf fand anfangs 34 veränderliche statische Felder. Nach der Korrektur
bleiben 32. Die verbliebenen Felder verwenden überwiegend `lock`, `Volatile` oder
`Interlocked`, oder sie gehören eindeutig zum einmaligen WPF-Start bzw. zum
Oberflächen-Thread.

### Bestätigter Parallelitätsfehler

`DossierPreviewPageRenderer` hielt zwei Rückrufe für Textersetzungen in statischen
Feldern. Zwei gleichzeitige Vorschauen auf getrennten STA-Threads konnten sich die
Rückrufe überschreiben. Dadurch konnte Vorschau A Text aus Vorschau B erhalten.

Korrektur:

- Rückrufe liegen nun in einem `AsyncLocal`-Bereich pro Ausführung.
- Ein gleichzeitiger Test mit zwei STA-Threads erzwingt die frühere Überschneidung
  und prüft, dass beide Texte getrennt bleiben.

### Weiterer gehärteter Zustand

Der austauschbare VSA-Katalog wird nun mit `Volatile.Read` und `Volatile.Write`
gelesen und gesetzt. Das verhindert veraltete Sicht auf einen parallel ersetzten
Katalog. Bei den übrigen 32 Feldern wurde in dieser Prüfung kein weiterer konkret
reproduzierbarer Wettlauf gefunden.

## 5. Leistung mit echten Datenmengen

Diese Prüfung kann noch nicht ehrlich als durchgeführt gelten.

Vorhanden sind kleine Fixtures für `BendSuggestions`, `DossierLookup`,
`TrainingExport` und `Yolo`. Der `NightlySoakRunner` prüft die echte Video-/KI-Kette
und misst Ressourcen. Es gibt aber keinen anonymisierten, eingefrorenen
Projektbestand, der Projektladen, Seitenwechsel, Vorschau, Dossier und Export
gemeinsam abdeckt.

### Erforderlicher fester Bestand

Für eine belastbare Prüfung werden drei bis vier anonymisierte Projektkopien
benötigt:

1. ein kleines Referenzprojekt mit bekannten Import- und Exportzahlen,
2. ein Projekt mit alten/ungewöhnlichen Importformaten,
3. ein grosses Projekt nahe der echten Grössenordnung von etwa 3'000 Videos,
4. optional ein bewusst beschädigtes Projekt für Fehler- und Wiederherstellungspfade.

Je Projekt müssen Hash, erwartete Haltungen/Schächte/Protokolle/Medien, erwartete
Warnungen und ein eingefrorenes Export-Soll festgehalten werden. Nur anonymisierte
Kopien dürfen in diesen Bestand gelangen.

### Messregel

- kalten Erstlauf getrennt ausweisen,
- danach mindestens fünf warme Läufe,
- Median und langsamsten Lauf für Projektladen, ersten Seitenwechsel, Vorschau,
  Dossier und Export festhalten,
- Ergebnisdateien zusätzlich inhaltlich gegen das eingefrorene Soll vergleichen.

Ohne diesen Bestand wäre jede konkrete Zeit- oder Vollständigkeitszahl erfunden.

## 6. Abnahme

Der vollständige Release-Build ist erfolgreich: **0 Warnungen, 0 Fehler**. Der
erste Lauf in die normalen Ausgabeordner wurde ausschliesslich durch zwei bereits
laufende MCP-Server blockiert, die ihre eigene DLL offen hielten. Diese Prozesse
wurden gemäss Projektregel nicht beendet. Derselbe vollständige Build wurde deshalb
in einen getrennten temporären Ausgabeordner ausgeführt und war dort fehlerfrei.

| Testprojekt | Bestanden | Übersprungen | Fehler |
|---|---:|---:|---:|
| Infrastructure | 5'550 | 5 | 0 |
| Pipeline | 2'490 | 2 | 0 |
| UI | 6'232 | 3 | 0 |
| ProjectModernizer | 62 | 0 | 0 |
| **Gesamt** | **14'334** | **10** | **0** |

Die vollständige UI-Suite löste während der Korrektur zwei berechtigte Wächter aus:
den Junction-Zähler nach dem neuen Schutztest und den 1'000-Zeilen-Wächter nach der
neuen Training-Center-Fehlermeldung. Beide Ursachen wurden korrigiert; danach liefen
die kompletten Suiten grün. Zusätzlich wurde ein zu dateigebundener Quelltext-Test
so angepasst, dass er die neue Persistenz-Teildatei prüft.

Sidecar- und QGIS-Tests waren für diese Änderungen nicht erforderlich, weil weder
Sidecar- noch QGIS-Code geändert wurde.

Die temporären Stryker-Konfigurationen und Messwerkzeuge gehören nicht zum
Produktstand und werden nach der Messung entfernt beziehungsweise bleiben unter
dem ignorierten `.tmp`-Ordner.

## Offene Entscheidung

Für Punkt 5 braucht es eine freigegebene anonymisierte Kopie. Bis sie vorliegt,
bleibt die Ende-zu-Ende- und Lastprüfung offen. Das Audit greift dafür nicht auf
`LastProjectPath`, Kundenordner oder andere vorhandene Originale zu.
