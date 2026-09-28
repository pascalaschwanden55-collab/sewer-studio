# AP04 – Arbeitsprotokoll: Verhalten statt Quelltext, Produktabdeckung getrennt

Stand: 28.09.2026. Plan: [UMSETZUNGSPLAN.md](UMSETZUNGSPLAN.md), Paket AP04 (W01, W11).

## Teil 1 – Aufnahmebindung als Verhalten prüfen

**Welche falsche Wirkung soll verhindert werden?** Ein KI-Befund erhält den Meter oder die Zeit
eines späteren Bildes statt des analysierten.

Bestand: `CodingMultiModelInferenceWorkflowTests.AnalyzedFrame_keeps_bytes_time_and_once_resolved_meter_after_delayed_inference`
prüft das Planbeispiel bereits als Verhalten (Bild A analysiert, Meter ändert sich danach, alle
Folgeschritte behalten Bild A, 12,3 s und 7,8 m).

Lücke eine Ebene tiefer: Ob der Befund-Befehl den Meter des Belegs nimmt oder die übergebene
Meterquelle, war nur über den Lambda-Text im Fenster geprüft
(`ResolveMeterForFrame: (_, _) => frame.Meter`).

- Neu: `CodingMultiModelFindingEventCommandWorkflowTests.AnalyzedFrame_uses_meter_and_time_of_the_frame_even_if_the_resolver_is_stale`
  – Beleg 12,3 m / 45 s, Meterquelle liefert 99, Player-Zeit 99 s. Ergebnis und
  Streckenschaden-Tracking erhalten 12,3 m und 45 s; die Meterquelle wird nie gefragt.
- Gegenprobe im Befehl: Meter wieder aus der Meterquelle → rot; Zeit wieder aus dem Player → rot.
- Danach ersetzt: In `DesignAuditPlayerCodingSidePanelTests.Player_ai_events_use_analyzed_frame_meter_not_stale_selected_meter`
  prüft die Multi-Model-Zeile nur noch, dass das Fenster den beleggebundenen Einstieg
  `ExecuteAnalyzedFrame` aufruft (Strukturregel), nicht mehr den Lambda-Text.
- Bewusst nicht ersetzt: die Zeile für den Grenz-Klassifikator. Dessen Befehl ruft die übergebene
  Meterquelle tatsächlich auf; dort ist die Verdrahtung im Fenster die Wirkung.
- Strukturtests wie die `UI/Ai`-Sperre und das Verbot von `_playerTimelineHost` im
  Multi-Model-Partial bleiben Strukturtests.

## Teil 2 – Produktabdeckung getrennt messen

- Neu: `.github/scripts/measure-product-coverage.ps1`. Zählt nur Dateien unter `src/`, ohne
  `obj/`, `bin/`, `*.g.cs`, `*.g.i.cs`, `*.Designer.cs`. Vereinigt alle Berichte nach
  normalisiertem Pfad (Gross/Klein, `\`/`/`, Repo-Wurzel abgeschnitten) und Zeilennummer; eine
  Zeile ist abgedeckt, wenn irgendein Bericht sie trifft. Teilwerte für die gerade umgebauten
  Bereiche. Nur Anzeige, keine Grenze; Exit 2 nur bei technischem Fehler.
- CI: Schritt „Produktabdeckung messen“ vor „Testabdeckung pruefen“. Die Verlaufsgrenze
  (`check-coverage.ps1`, `coverage-baseline.json`) ist unverändert.
- Handprüfbares Beispiel: `tests/Fixtures/Coverage/ProduktAbdeckung/` mit README
  (2 von 5 Produktzeilen = 40 %; die alte Mischzahl ergäbe 5 von 12 = 41,67 %).
  Test `ProductCoverageScriptTests` führt das Skript darauf aus; Strukturtest
  `CoverageGateArchitectureTests.Produktabdeckung_wird_getrennt_von_der_Verlaufsgrenze_gemessen`.
- Gegenprobe im Skript: erzeugter Code wieder mitgezählt → rot; Zeilen je Bericht statt einmal
  gezählt → rot.
- Echter Formatnachweis (lokal, **keine** Grenzgrundlage): nur Pipeline-Tests → 1'429
  Produktdateien, 24'786 von 128'090 Produktzeilen = 19,35 %; die Mischzahl desselben Laufs
  30,09 %.

## Restgrenze

- Eine Produktgrenze wird erst nach mindestens einem echten CI-Lauf festgelegt. Der Push ist
  derzeit gesperrt (`remote.origin.pushurl = blocked://ferien`).
- Weitere Quelltexttests der Player-Partials bleiben; sie prüfen überwiegend Verdrahtung im
  Fenster, die ohne sichtbares Fenster nicht als Verhalten ausführbar ist (Thema AP09).
