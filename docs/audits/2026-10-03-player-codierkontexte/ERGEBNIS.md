# Player: Aufbau der Codierkontexte – 03.10.2026

## Umfang

Ein abgegrenztes Paket mit Ausgangspunkt `master` `cc7fa12fa`.
Die frühen Verbindungen für Befunde, Analyse und Rohrgrenzen liegen jetzt im eigenen
`PlayerWindowCodingContextFactory`-Baustein. Die drei vorhandenen Kontextklassen,
Sitzungsbesitzer und aktuellen Rückrufe werden verwendet. Die älteren späteren Presenter
aus dem Hauptarbeitsstand gehören nicht zu diesem Paket.

Zwei Agenten hatten getrennte Schreibzuständigkeiten für Produktion und Tests.
Builds und Tests wurden zentral ausgeführt; eine weitere Prüfung hat die eingefrorenen
Produktions- und Kontext-Testdateien unabhängig freigegeben. Kundenoriginale und der
ältere Hauptarbeitsstand werden nicht verändert.

## Geschütztes Verhalten

- Erstellung liest keine Quelle und startet keine Aktion.
- Analyse liest nach einem Wechsel aktuelle Rohrende-Ereignisse aus Sitzung, Ansicht und Import.
- Befunderkennung liest aktuelle Sitzungs- und Ansichtsereignisse.
- Rohranfang verwendet den später verfügbaren Dienst, importierten Meter und sauberen Frame;
  Bildextraktion, Fotoanbindung, Kalibrierung und Duplikatschutz bleiben erhalten.
- Besitzer, neun Eingaben, sechs Aktionen und die Reihenfolge vor `InitializeComponent`
  sind zusätzlich durch Anschlussprüfungen geschützt.
- Bestehende OSD-/Aufnahmeprüfungen, öffentlicher Vertrag und gespeicherte Formate bleiben erhalten.

Die sieben Verhaltenserwartungen waren zuerst am bisherigen Aufbau grün. Danach
wurde nur der Testaufbau auf die echte Factory umgestellt; Legacyhilfen sind entfernt.
Der Baseline-Lauf mit den Anschlussprüfungen bestand alle 23 Fälle ohne Überspringen.
Der Baseline-Release-Build hatte null Warnungen und Fehler.

## Wartbarkeitsmessung

Gemessen werden alle Quelldateien in `UI/Views/Windows`, die `partial class PlayerWindow`
erklären. Leer-/Kommentarzeilen zählen mit, eigenständige Helfer und generierter Code nicht.

| Stand | Fensterklasse insgesamt | Teildateien | Hauptdatei |
| --- | ---: | ---: | ---: |
| Ausgangspunkt | 4.246 | 73 | 568 |
| Nach diesem Paket | 4.237 | 73 | 559 |

Dies ist ein kleiner erster Lieferbaustein. Er macht den Aufbau getrennt prüfbar und
entfernt wiederholte Quellenverbindungen. Das Ziel unter 1.000 Zeilen für die ganze
Fensterklasse bleibt offen; spätere Wartbarkeitswellen sind weiterhin einzeln zu prüfen.

Der Gesamt-UI-Lauf hat die veraltete Größenkonstante im Wartbarkeitswächter entdeckt.
`ExistingLargePartialTypes` wird auf 4.237 gesenkt; alle anderen Regeln bleiben erhalten.
Der erste Lauf hatte genau diesen einen Fehler. Der eingefrorene Endstand umfasst
jetzt sieben Dateien einschließlich dieses Testwächters; Produktions- und Kontext-Testdateien blieben
identisch. Build, Kontext-/Wächterprüfungen und die ganze UI-Suite werden damit erneut
geprüft. Die grünen Infrastruktur-/Pipeline-Läufe gelten für unveränderte Quellen.

## Abschlussprüfungen vor dem Commit

- Vollständiger Release-Build: null Warnungen und Fehler.
- Endfokus einschließlich Wartbarkeitswächtern: 31 bestanden, null übersprungen/Fehler.
- Endgültige unabhängige Code-/Testprüfung einschließlich Größenwächter: freigegeben.
- Alle sieben eingefrorenen Quell-/Testdateien sind während der Abschlussprüfungen unverändert.
- Architekturkarte mit dem tatsächlichen Anschluss abgeglichen und erfolgreich validiert.

| Release-Testbereich | Bestanden | Übersprungen | Fehler |
| --- | ---: | ---: | ---: |
| Infrastructure | 8.235 | 6 | 0 |
| Pipeline | 3.048 | 3 | 0 |
| UI | 7.914 | 49 | 0 |
| ProjectModernizer | 62 | 0 | 0 |
| Gesamt | 19.259 | 58 | 0 |

Die 49 übersprungenen UI-Einstiege sind die bestehenden isolierten WPF-Kindprozessfälle;
ihre übergeordneten Tests starten und prüfen den jeweiligen Kindprozess. Die neuen
Kontextfälle und Anschlussprüfungen wurden ohne Überspringen ausgeführt. Infrastruktur/
Pipeline enthalten neun bestehende, hier nicht aktivierte Live-/Umgebungsfälle.
Der unveränderte Pre-Push-Hook prüft vor dem Hochladen alle vier Bereiche erneut.

## Grenzen

Die neuen Verhaltenstests verwenden echte Kontextklassen mit kontrollierten Quellen.
Sie starten keinen echten Player und führen keine Videoanalyse mit KI-Modellen aus.
Die tatsächlichen Fensteranschlüsse werden durch Architekturprüfungen geschützt.
Es werden keine neuen UI/Ai-Typen, Laufzeiten oder registrierten Dienste eingeführt.
