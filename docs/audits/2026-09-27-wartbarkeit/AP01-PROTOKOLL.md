# AP01 – Arbeitsprotokoll: Wartbarkeits-Gate geschlossen

Stand: 28.09.2026. Plan: [UMSETZUNGSPLAN.md](UMSETZUNGSPLAN.md), Paket AP01.

## Ausgangsstand

- `MaintainabilityFitnessTests.No_new_production_file_exceeds_1000_lines` rot:
  `AnnotationWorkbenchService.cs` 1'034 Zeilen, `MultiModelAnalysisService.cs` 1'016 Zeilen.
  Beide Dateien waren schon im Commit `e4bf6273d` so gross.
- Übrige UI-Tests grün (7'458), Pipeline 2'853, Infrastruktur 7'498.

## Workbench: Gold-Maskenentscheidung ausgelagert

**Verantwortung:** Entscheiden, ob eine SAM-Maske ein Goldsample tragen darf, und nur eine
geprüfte Maske auf das Sample schreiben.

- Neu: `src/AuswertungPro.Next.UI/Services/WorkbenchGoldMask.cs` (`Evaluate`, `IsValid`,
  `AreaPixels`, `ApplyTo`). Bleibt in der UI-Schicht, weil er `SamMaskValidator`
  (Infrastructure) verwendet – wie der Workbench-Dienst selbst.
- `AnnotationWorkbenchService.SaveCoreAsync` ruft ihn an derselben Stelle auf; die private
  Methode `EvaluateGoldMask` und der Kopierblock der Maskenfelder entfallen.
- Unverändert: Bildmasse müssen zum gespeicherten Goldbild passen; ein Lesefehler der
  Bildmasse (auch Abbruch) ergibt nie Gold; die Fläche kommt aus dem RLE, nicht vom Sidecar;
  eine abgelehnte Maske wird nicht gespeichert.
- Schutz: die bestehenden `AnnotationWorkbenchServiceTests` (Masse passen nicht, Lesefehler,
  fehlende Masse, degradiert, Fläche aus RLE, gültige Maske, Reparatur).
- Gegenprobe: Masseprüfung vorübergehend ausgeschaltet → 2 Tests rot, danach zurückgesetzt.

## Mehrmodell-Analyse: YOLO-cls-Vorfilter ausgelagert

**Verantwortung:** Entscheiden, ob ein Bild vor YOLO/DINO/SAM/Qwen übersprungen wird.

Abweichung vom Planvorschlag (Vorbereitung, Wiederaufnahme oder Fehlerabschluss):
Wiederaufnahme (`RestoreCheckpointAsync`) und Abschluss (`BuildResult`, `ReportCompletion`)
sind bereits eigene Methoden. Der Vorfilter war der grösste noch eingebettete, klar
abgegrenzte Block: drei fast gleiche Überspringen-Zweige mit fest eingebauter Schwelle.

- Neu: `src/AuswertungPro.Next.Infrastructure/Ai/Pipeline/ClsPrefilterRule.cs` – reine Regel
  (`Decide`), Reihenfolge unbrauchbar → LEER (nur Klassifikatorregime) → OTHER/NORMAL,
  Schwelle `SkipConfidence = 0.70` (strikt grösser).
- `AnalyzeAsync` hat statt drei Zweigen einen: Überspringen buchen (Zähler, Trace, Telemetrie,
  Fortschritt, Dedup-Fenster, Checkpoint `Advance`); nur LEER führt zusätzlich das
  Voting-Fenster nach und schreibt Klassifikatorcode, Konfidenz und Modell in den Trace.
- Unverändert: Trace-Pfade und Drop-Gründe, Fortschrittstexte, Checkpoint-Art, Fallback auf
  die Detektion bei einem Vorfilterfehler, Nutzerabbruch wird weitergereicht.
- Geändert: nur der Wortlaut der Debug-Logzeile (eine statt drei Formulierungen).
- Schutz: neu `tests/AuswertungPro.Next.Pipeline.Tests/MultiModelClsPrefilterTests.cs`
  (20 Fälle). Die Videolauf-Fälle waren **vor** dem Umbau am unveränderten Code grün.
- Gegenprobe: Schwelle vorübergehend 0,60 → 3 Tests rot, danach zurückgesetzt.
- Nebenbei entfernt: drei verwaiste Kommentarzeilen am Dateiende ohne zugehörigen Code.

## Ergebnis

| Datei | vorher | nachher |
|---|---:|---:|
| `AnnotationWorkbenchService.cs` | 1'034 | 974 |
| `MultiModelAnalysisService.cs` | 1'016 | 967 |

Keine Grenze erhöht, keine neue Teildatei, keine neue Abhängigkeit, kein neues gespeichertes
Format. Öffentliche Fassaden (`IAnnotationWorkbenchService`, `MultiModelAnalysisService`)
unverändert.

Prüfungen: siehe Commit-Beschreibung (Release-Build und alle vier .NET-Testprojekte).

## Restgrenze

- `AnalyzeAsync` bleibt mit rund 780 Zeilen die eigentliche Baustelle (AP05). Beide Dateien
  liegen nur knapp unter 1'000 Zeilen; die nächste fachliche Erweiterung dort sollte zuerst
  eine weitere Verantwortung abgeben.
- Rücknahme: je Datei den neuen Baustein und seine Delegation gemeinsam zurücknehmen; keine
  Datenmigration.
