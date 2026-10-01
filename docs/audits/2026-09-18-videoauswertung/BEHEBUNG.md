# Videoauswertung: Behebung der Auditbefunde 05-10 und 14

Stand 18.09.2026. Bezug: Audit vom 18.09.2026, zweites Reparaturpaket nach
`docs/audits/2026-09-18-dateischutz/BEHEBUNG.md`.

## Behobene Befunde

| Nr. | Befund | Kern der Korrektur |
|---|---|---|
| 05 | Vorzeitig endende Videos erscheinen vollstaendig analysiert | `MultiModelRunCompleteness` reicht den Abschlussstatus des Frame-Streams bis ins Gesamtergebnis; der Checkpoint wird nur bei vollstaendiger Extraktion geschlossen |
| 06 | Qwen-Fehler werden als erfolgreiche Antworten gezaehlt | `Error`/`Outcome` werden vor der Erfolgsmeldung ausgewertet; leeres Ergebnis bleibt Erfolg, Nutzerabbruch wird weitergereicht |
| 07 | Vollstaendiger SAM-Maskenverlust endet als gesundes Null-Ergebnis | Maskenverluste laufweit gesammelt; bewusste Score-Verwerfung wird von technischem Verlust getrennt |
| 08 | Befunde ab Nummer 251 nicht uebernehmbar | `Take(250)` im `PipelineResultPresenter` entfernt |
| 09 | Videoanalyse verwendet nicht den Rohrdurchmesser der Haltung | neue `PipelinePipeDiameterPolicy`; `PipelineRequest`/`PipelineConfig` tragen den geprueften DN additiv |
| 10 | Andere Reihenfolge der Modellergebnisse vermischt getrennte Schaeden | `TemporalFindingDeduplicator` ordnet ueber die letzte Box zu statt ueber den Listenplatz |
| 14 | Fertige Analyseanzeige uebernimmt Warnungen nicht dauerhaft | `IsDone` schuetzt den Abschluss; Warnungen, Phase und Status werden gemeinsam gesetzt |

## Geaenderte Dateien

Neu: `Application/Ai/PipelinePipeDiameterPolicy.cs`,
`Infrastructure/Ai/Pipeline/MultiModelRunCompleteness.cs`.

Geaendert: `PipelineConfig.cs`, `VideoPipelineContracts.cs`, `EnhancedVisionPromptBuilder.cs`,
`MultiModelAnalysisService.cs` und `.Helpers.cs`, `TemporalFindingDeduplicator.cs`,
`VideoAnalysisPipelineService.cs`, `DataPageVideoAnalysisController.cs`,
`PipelineProgressMapper.cs`, `PipelineResultPresenter.cs`, `VideoAnalysisPipelineModels.cs`,
`VideoAnalysisPipelineWindow.xaml` und `.xaml.cs`.

## Nachweis

- Vollstaendiger Debug-Build der Solution: erfolgreich, 0 Fehler.
- Testlauf je Projekt mit eigenem Ausgabeordner (ein gemeinsamer Ordner laesst den
  Testhost eine DLL sperren und ueberspringt Projekte bei Exit 0):

  | Projekt | Ergebnis |
  |---|---|
  | Infrastructure | 6779 gesamt, 0 Fehler, 6 uebersprungen |
  | UI | 7354 gesamt, 0 Fehler, 29 uebersprungen |
  | Pipeline | 2691 gesamt, 0 Fehler, 3 uebersprungen |
  | ProjectModernizer | 62 gesamt, 0 Fehler |

- **Sabotageprobe 18.09.2026** (Nachweis, dass die neuen Tests tragen):
  - `Take(250)` wieder eingebaut -> 4 Faelle in `PipelineCompletionSafetyTests` rot
    (`count: 251` und `count: 1001`, je mit und ohne Nur-letzter-Auswahl).
  - Raeumliche Frame-Zuordnung abgeschaltet -> 8 von 9 Faellen in
    `TemporalFindingDeduplicatorContinuityTests` rot.
  - Beide Eingriffe wurden danach bytegleich zurueckgenommen.

## Grenzen

- Die Erkennungsqualitaet der Modelle wurde nicht gemessen und nicht veraendert. Das aktive
  Detect-Modell bleibt ausdruecklich nicht freigegeben.
- Der uebergebene Rohrdurchmesser macht Maskenmasse nachvollziehbar; er ersetzt keine
  Kalibrierung. Die 70-%-Bildbreitenannahme bleibt eine ausgewiesene Schaetzung.
- Keine Sichtprobe im laufenden Programm. Das WPF-Fenster ist nur ueber den
  Kindprozesstest belegt.
- Offen aus demselben Audit: Befund 04 (PDF-Zuordnung beim Portabelmachen) sowie die
  Befunde 11 bis 13 und 15 bis 18.
