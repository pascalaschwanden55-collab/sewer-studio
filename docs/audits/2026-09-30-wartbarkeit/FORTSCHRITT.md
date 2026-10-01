# Wartbarkeit: Umsetzungsstand (30.09.2026, abends)

Grundlage sind der [Audit-Bericht](BERICHT.md) vom 30.09.2026 und der
[Umsetzungsplan vom 27.09.](../2026-09-27-wartbarkeit/UMSETZUNGSPLAN.md). Alle Pakete sind in
`feature/webgis-uebertragung` gemergt, jeweils mit grünem Pre-Push-Hook (vier .NET-Testprojekte).

## Erledigte Pakete

| Paket | Inhalt | PR |
| --- | --- | --- |
| Q1, R1 | CLAUDE.md auf 210 Zeilen, Bereichsdateien unter `docs/architektur/`; Haltungsidentität mit gemeinsamer Beispieldatei | #26 |
| Q3, Q4 | 16 gleiche Theme-Stile nur noch in `Controls.xaml`; Testhilfen und Sprachlisten an einer Stelle | #27 |
| Q2, Q5 | Sperrklinke für Dateien > 900 und Typen > 1'500 Zeilen; Schnelllauf ohne Kindprozess-Tests | #28 |
| R3 | Rückgängig-Anbindung an einer Stelle, Registrierungstest ohne feste Zahl, Tastenkürzel-Test | #29 |
| R2 | WebGIS: refIds je einmal mit Status, `NieSenden`/`HolenUeberschreibtHand`, berechneter Schreibausgang | #30 |
| AP06 | Goldsample-Speichern als Anwendungsfall mit Grenze «dauerhaft gespeichert» | #31 |
| AP05 | Mehrmodell-Analyse: Laufzustand, Bildschritt, Modellschritte als eigene Klassen | #32, #34 |
| Ferien | Übernahme der Ferienarbeit F26 (Negativ-Set-Pfad, Parser-Grenzwerte, Overflow-Korrektur) | #33 |
| AP08 | XTF-Parser: SIA405 und VSA-KEK je nach Lesen, Beziehungen und Übernahme getrennt | #35, #40 |
| AP10 | Python-Negativsatzprüfung in `negativsatz_pruefung.py` aufgeteilt | #36 |
| AP09 | Player-Pilot «gebundenes Einzelbild analysieren» als Anwendungsfall | #37 |
| Befund | XTF-Import: doppelte SIA405-Haltungen, mehrere VSA-KEK-Untersuchungen (Variante C) | #38 |
| Befund | Proto-Negativsätze: keine Überschneidung, keine Gold-Testhaltungen | #39 |

## Messung nach Welle 3

Gemessen mit dem unveränderten Analysator (`../2026-09-27-wartbarkeit/nachweise/analysator`) auf
`f44740b62`. Rohwerte: [nachweise/messwerte-nach-welle3.json](nachweise/messwerte-nach-welle3.json).

| Messgrösse (Produktcode `src/`) | 30.09. Audit | nach Welle 3 |
| --- | ---: | ---: |
| Methoden über 200 Zeilen | 33 | 28 |
| Methoden über 100 Zeilen | 198 | 197 |
| Dateien mit 900–1'000 Zeilen | 23 | 21 |
| Dateien mit 975–1'000 Zeilen | 10 | 9 |
| Typen mit 1'800–2'000 Zeilen | 8 | 7 |
| Dateien in `UI/Ai` | 599 | 594 |
| CLAUDE.md | 6'756 Zeilen, 548 KB | 210 Zeilen, 14 KB |

Einzelne grosse Abläufe (Zeilen je Methode):

| Methode | vorher | nachher |
| --- | ---: | ---: |
| `MultiModelAnalysisService.AnalyzeAsync` | 782 | 121 (Bildschritt 160) |
| `AnnotationWorkbenchService.SaveCoreAsync` | 339 | entfällt (Anwendungsfall) |
| `LegacyXtfImportService.ParseSia405` | 355 | 13 |
| `LegacyXtfImportService.ParseVsaKek` | 331 | 17 |
| `gold_stock_audit._read_reviewed_negative_set` (Python) | 768 | 101 |
| `gold_stock_audit._read_proto_reviewed_negative_set` (Python) | 682 | 117 |

Die längsten verbleibenden Methoden sind der `PlayerWindow`-Konstruktor (539), `HaltungsgrafikSvgBuilder.
BuildHaltungsgrafikSvg` (502), der `ServiceProvider`-Konstruktor (406), `ProjectImportOrchestrator.Import` (401)
und `ParsedHoldingDistributionController.Distribute` (362). Leere `catch`-Blöcke (237) sind unverändert und
bleiben eine Kandidatenliste (Regel: bei jeder Berührung entscheiden).

## Offen

- **Fachliche Befunde aus den Paketen:** am 01.10.2026 behoben (PR #42 Videoanalyse: OSD-Meter nur bei
  höchstens 5 m/s zum letzten belegten OSD-Meter, kein fremder bestätigter Code bei eigenem abweichendem
  Vorschlag; PR #43 XTF-Import: fehlende Bezüge, ISO-Datum, Untersuchungen ohne Bezeichnung, verwaiste
  Schäden, VSA-KEK neben SIA405 gemeldet, `Link_G` = Gegenbefahrung, DINO-Trace hängt an). Noch offen:
  Die Meterschätzung (`EstimateMeter`) richtet sich nicht am letzten OSD-Meter aus – ein Sprung «geschätzt
  40,9 m → OSD 12,0 m» bleibt deshalb möglich (Entscheid Pascal); `Link_G` folgt der Fliessrichtung statt der
  WinCan-Namensmarke `_G` (Bestätigung Pascal).
- **Trainingsbestand:** Die drei echten Negativsätze passen nicht mehr zur aktiven Detect-Klassenkarte (neu
  ableiten); die Python-Prüfung vergleicht Satzbilder nicht mit den Eval-Schlüsseln (der C#-Export stoppt es).
- **WebGIS:** Breite/Höhe der Haltung am Eiprofil klären; drei früher als «falsch» bezeichnete refIds;
  Vergleichsliste und Übersicht zeigen/zählen nicht nachgeprüfte bzw. ungeklärte Massnahmen missverständlich.
- **Nicht umgesetzt aus dem Plan:** Z11 Python-Hilfsfunktionen (bei Berührung ersetzen); Z12 Reste.
  Der `StaTestRunner`-Fix (Absturz 0xC0000602) ist seit 01.10.2026 eingecheckt, nachdem derselbe
  Absturz die CI des PR nach `master` rot machte. Die CI ist seit dem 01.10.2026 wieder grün
  (PR #45: Referenzschnappschuss ohne lokalen WinCan-Katalog, urllib3 2.8.0, Temp-Langpfad für die
  Trainingsskripte, Objektakte-Spalten nach tatsächlicher Breite, Startzeiten für PowerShell).
