# Player: gebundenes Einzelbild analysieren und Ereignisse anzeigen

Pilot des Wartbarkeitspakets AP09 (30.09.2026). Diese Seite beschreibt einen einzigen Ablauf im
Codiermodus: Der Player nimmt ein Bild auf, bindet Bild, Aufnahmezeit und Meter aneinander, lässt
die Mehrmodell-Analyse (YOLO/DINO/SAM) laufen und zeigt Ergebnis und neue Ereignisse an.
Live-Erkennung (Qwen), manueller Codiermodus, Training Center und Wiedergabe gehören nicht dazu.

## Stand vor AP09 (d810e74bc)

Einstieg sind «Aktuellen Frame analysieren», der 5-s-Live-Takt und die markierte Stelle. Alle landen in
`PlayerWindow.RunCodingAnalysisAsync` (`PlayerWindow.Coding.Ai.cs:22`). `CodingAnalysisCommandWorkflow`
(`Ai/Coding/CodingAnalysisCommandWorkflow.cs:39`) sperrt Doppelstarts, fängt Abbruch und Fehler und gibt die
Analyse im `finally` frei. Bei Mehrmodell ruft er `RunMultiModelAnalysisAsync` (`:59`). Ab dort beginnt der Pilot.

| # | Station | Datei:Zeile | Aufgabe |
|---|---|---|---|
| 1 | `RunCodingMultiModelAnalysisAsync` | `Views/Windows/PlayerWindow.Coding.Ai.MultiModel.cs:10` | verschachtelt vier Ebenen Aktionen für 2–10 |
| 2 | `CodingMultiModelAnalysisCommandWorkflow.ExecuteAsync` | `Ai/Coding/CodingMultiModelAnalysisCommandWorkflow.cs:30` | Reihenfolge Laufzeitprüfung → Start → Endmeter → Inferenz |
| 3 | `CodingMultiModelRuntimeGateWorkflow.Execute` | `Ai/Coding/CodingMultiModelRuntimeGateWorkflow.cs:28` | nur zwei Nullprüfungen (Dienst, Abbruchquelle) |
| 4 | `CodingMultiModelAnalysisStartWorkflow.ExecuteAsync` | `Ai/Coding/CodingMultiModelAnalysisStartWorkflow.cs:35` | Status «Schritt 1», Bild, Goldbild merken, OSD lesen, Einblendung prüfen, «Schritt 2» |
| 5 | `CodingEndMeterResolveWorkflow.Execute` | `Ai/Coding/CodingEndMeterResolveWorkflow.cs:21` | Endmeter nur mit Codiersitzung |
| 6 | `CodingMultiModelInferenceWorkflow.ExecuteAnalyzedFrameAsync` | `Ai/Coding/CodingMultiModelInferenceWorkflow.cs:47` | Meter einmal auflösen, **Aufnahmebeleg** bilden |
| 7 | `CodingMultiModelInferenceWorkflow.ExecuteAsync` | `Ai/Coding/CodingMultiModelInferenceWorkflow.cs:63` | Klassifikator-Eingabe, Analyse, Fehler, dann Grenze → Struktur → Ergebnis |
| 8 | `CodingMultiModelClassifierInputPolicy.Build` | `Ai/Coding/CodingMultiModelClassifierInputPolicy.cs:10` | Regel DN 300 / Reichweite |
| 9 | `CodingMultiModelAnalysisResultWorkflow.Execute` | `Ai/Coding/CodingMultiModelAnalysisResultWorkflow.cs:36` | Ergebnisstatus, Bildhinweis, Masken, Übergabe der sichtbaren Befunde |
| 10 | `ShowMultiModelResults` → `CodingMultiModelResultsRenderWorkflow` | `PlayerWindow.Coding.Ai.Rendering.cs:14`, `Ai/Coding/CodingMultiModelResultsRenderWorkflow.cs:34` | SAM-Masken und Referenzkreis zeichnen |
| 11 | `AddMultiModelFindingsAsEvents` | `Views/Windows/PlayerWindow.Coding.AiEvents.MultiModel.cs:13` | Beleg an den Ereignisbefehl binden |
| 12 | `CodingMultiModelFindingEventCommandWorkflow.ExecuteAnalyzedFrame` → `Execute` | `Ai/Coding/CodingMultiModelFindingEventCommandWorkflow.cs:75`, `:94` | Zeit/Meter aus dem Beleg, Streckentracker, neue Tracker-Zeilen merken |
| 13 | `CodingMultiModelFindingEventWorkflow.Execute` | `Ai/Coding/CodingMultiModelFindingEventWorkflow.cs:45` | je Befund Code, Abdeckung, Folgebeleg, Ampel, Ereignis |
| 14 | Regeln | `CodingMultiModelEventFactory`, `CodingMultiModelEventAppender`, `CodingMultiModelQualityGatePolicy`, `Application/Ai/CodingMultiModelFindingAddDecisionPolicy`, `Application/UseCases/CodingPointFollowUp/*` | Ereignisentwurf, menschliche Änderung, Ampel, Folgebeleg |

Grenze und Struktur (BCD/BCE, BCA/BCC) zweigen nach Station 7 in eigene Befehle ab
(`PlayerWindow.Coding.Ai.Classifier.Boundary.cs:13`, `…Structural.cs:11`); sie sind nicht Teil des Umbaus.

Der `CodingReplay`-Messhost (`tools/CodingReplay/ReplayCodingAnalyzer.cs:245`) verwendet die Stationen 7, 9,
12 und 13 direkt. Ihre öffentlichen Verträge müssen deshalb bleiben.

## Daten

- **Anfrage:** Tätigkeitstext, Aufnahmezeit (Sekunden) des Players, Mehrmodell-Dienst und Abbruchquelle
  aus `CodingAiController`.
- **Aufnahmebeleg** `CodingAnalyzedFrameEvidence` (`Application/UseCases/CodingPointFollowUp/CodingAnalyzedFrameEvidence.cs:8`):
  PNG-Bytes, Aufnahmezeit, einmal aufgelöster Meter mit Herkunft (`CodingMeterResolution`, `Application/Ai/CodingMeterResolver.cs:5`).
  Grenze, Struktur und Befunde verwenden genau diesen Beleg; nach der Inferenz wird nichts neu gelesen.
- **Klassifikator-Eingabe:** DN (Kalibrierung oder 300), Meter des Belegs, Reichweite (Endmeter oder Meter, mindestens 1).
- **Analyseergebnis** `SingleFrameResult` (Infrastructure) und daraus `SegmentedFinding` (Infrastructure).
  Beide Typen darf `Application` nicht kennen.
- **Ausgabe:** Statuszeile mit Farbe, SAM-Masken, neue `CodingEvent`-Zeilen mit Foto des Belegs und Ampel.

## Zustandsbesitzer

| Zustand | Besitzer |
|---|---|
| Mehrmodell-Dienst, Abbruchquelle, laufende Analyse, QualityGate | `CodingAiController` (`Player/CodingAiController.cs:15`) über `_codingAiRuntimeOwner` |
| Analysiertes Bild für Goldsample und Rohrende beim Verlassen | `LiveDetectionController.StoreAnalyzedFrame` (`Player/LiveDetectionController.cs:109`) |
| Einblendungszustand (OSD-Vorspann) | `_codingFrameReadinessController` (`PlayerWindow.Coding.FrameReadiness.cs:30`) |
| Letzter OSD-Meter, OCR-Dienst | `CodingOsdMeterController` (`PlayerWindow.Coding.Osd.cs:27`) |
| Endmeter, Ereignisliste der Ansicht | `CodingSessionHost` (`Player/CodingSessionHost.cs:56`) |
| Ereignisse, Folgebeleg-Register, `HumanTouchedAtUtc` | `ICodingSessionService` über `_codingSessionRuntimeOwner` |
| Kalibrierung, DN | `CodingOverlayToolHost` (`Player/CodingOverlayToolHost.cs:50`) |
| Statuszeile, Masken | `_liveDetectionStatusController`, `CodingOverlayCanvas`, `_codingOverlayRenderState` |

## Schutzregeln

- **Aufnahmebindung:** Meter wird genau einmal vor der Inferenz aufgelöst; Beleg und Foto kommen aus demselben
  PNG. Tests: `CodingMultiModelInferenceWorkflowTests`, `CodingMultiModelFindingEventCommandWorkflowTests`.
- **Menschliche Änderungen:** `CodingMultiModelEventAppender` übernimmt eine im `EventAdded` begonnene
  Bearbeitung und markiert sie; Tracker-Zeilen werden im selben Tick nach Bearbeitung nicht ergänzt.
  Tests: `CodingMultiModelEventAppenderTests`, `CodingMultiModelFindingEventWorkflowTests`, `CodingPointFollowUpPolicyTests`.
- **Abbau beim Schliessen:** Verlassen des Codiermodus und Schliessen des Fensters rufen
  `CodingAiController.DisposeAnalysisCancellation`/`Dispose` (`PlayerWindow.Wiring.cs:103`,
  `PlayerWindowCodingModeExitControllerFactory.cs:115`). Danach fehlt die Abbruchquelle und der Ablauf
  startet nicht; eine laufende Analyse endet mit Abbruch in `CodingAnalysisCommandWorkflow`.
- **Detektor-Ampel:** Unqualifizierte Detektorbelege bleiben höchstens gelb (`CodingMultiModelQualityGatePolicy`).
