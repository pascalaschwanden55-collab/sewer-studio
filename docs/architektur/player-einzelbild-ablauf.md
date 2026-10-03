# Player: gebundenes Einzelbild analysieren und Ereignisse anzeigen

Pilot des Wartbarkeitspakets AP09 (30.09.2026). Diese Seite beschreibt einen einzigen Ablauf im
Codiermodus: Der Player nimmt ein Bild auf, bindet Bild, Aufnahmezeit und Meter aneinander, lässt
die Mehrmodell-Analyse (YOLO/DINO/SAM) laufen und zeigt Ergebnis und neue Ereignisse an.
Live-Erkennung (Qwen), manueller Codiermodus, Training Center und Wiedergabe gehören nicht dazu.

## Aufbau der Codierkontexte (03.10.2026)

`PlayerWindowCodingContextFactory` unter `UI/Player` verbindet die bestehenden
`CodingFindingContext`, `CodingAnalysisContext` und `CodingBoundaryContext`.
Zwei interne Records beschreiben Eingaben und Ergebnis; es gibt keine neue
Fachlogik, Registrierung oder Änderung an öffentlichen Schnittstellen und Formaten.

Der Fensterkonstruktor übergibt den vorhandenen Sitzungshost und verzögerte Leser
für aktuellen Sitzungsdienst, Importereignisse, Kalibrierung, Bildformat, Schnappschuss,
ersten sauberen Frame, OSD-Meter und Videozeit. Der Baustein erzeugt Finding → Analysis
→ Boundary, ohne diese Leser oder Bildaktionen auszuführen. Der Host liefert weiterhin
die jeweils aktuellen Ansichtsereignisse und den Endmeter. Sitzungswechsel und noch
fehlende Sitzungen werden wie bisher behandelt.

Die sechs Grenzaktionen bleiben dieselben: Katalog, Trace, Bildextraktion,
Fotoanbindung, Kalibrierung und Aktualisierung. Insbesondere bleiben
`TryExtractFrameAtSecondsAsync` und `AttachBoundaryAnalyzedFramePhoto` am Fenster
angeschlossen. Nach dem Sitzungshost werden alle drei Kontextfelder zugewiesen,
bevor `InitializeComponent` die Steuerelemente erstellt. Der AP09-Ablauf bleibt erhalten.

Sieben Verhaltenserwartungen wurden zuerst am bisherigen Kontextaufbau geprüft,
anschliessend unverändert auf die Factory umgestellt. Eine zusätzliche Anschlussprüfung
schützt Besitzer und Reihenfolge. Bestehende Analyse-/Befund-/Grenzprüfungen sichern
die vollständigen Argumentzuordnungen; OSD- und Aufnahmebindungsprüfungen bleiben erhalten.
Für die Wartbarkeitsmessung zählen alle Teildateien der Fensterklasse zusammen.

Einstieg sind «Aktuellen Frame analysieren», der 5-s-Live-Takt und die markierte Stelle. Alle landen in
`PlayerWindow.RunCodingAnalysisAsync` (`Views/Windows/PlayerWindow.Coding.Ai.cs:22`).
`CodingAnalysisCommandWorkflow` (`Ai/Coding/CodingAnalysisCommandWorkflow.cs:39`) sperrt Doppelstarts, fängt
Abbruch und Fehler und gibt die Analyse im `finally` frei. Bei Mehrmodell ruft er `RunMultiModelAnalysisAsync`
(`:59`). Ab dort beginnt der Pilot.

## Aufrufweg seit AP09

| # | Station | Datei:Zeile | Aufgabe |
|---|---|---|---|
| 1 | `RunCodingMultiModelAnalysisAsync` | `Views/Windows/PlayerWindow.Coding.Ai.MultiModel.cs:12` | eine flache Liste: welcher Player-Teil welchen Schritt liefert |
| 2 | `CodingEinzelbildAnalyseUseCase.ExecuteAsync` | `Application/UseCases/CodingEinzelbild/CodingEinzelbildAnalyseUseCase.cs:88` | ganzer Ablauf in fünf nummerierten Abschnitten (siehe unten), ohne WPF |
| 3 | `CodingEinzelbildAnalyseUseCase.AuswertenAsync` | `…/CodingEinzelbildAnalyseUseCase.cs:156` | Analyse, dann Modellfehler → Grenze → Struktur → Ergebnis |
| 4 | `CodingEinzelbildStatusAnzeige.Zeigen` | `UI/Player/CodingEinzelbildStatusAnzeige.cs:12` | fachliche Meldung → Statustext, Farbe, Puls |
| 5 | `CodingMultiModelAnalysisResultWorkflow.Execute` | `Ai/Coding/CodingMultiModelAnalysisResultWorkflow.cs:36` | Ergebnisstatus, Bildhinweis, Masken, Übergabe der sichtbaren Befunde |
| 6 | `ShowMultiModelResults` → `CodingMultiModelResultsRenderWorkflow` | `PlayerWindow.Coding.Ai.Rendering.cs:14`, `Ai/Coding/CodingMultiModelResultsRenderWorkflow.cs:34` | SAM-Masken und Referenzkreis zeichnen |
| 7 | `AddMultiModelFindingsAsEvents` → `CodingMultiModelFindingEventCommandWorkflow` → `CodingMultiModelFindingEventWorkflow` | `PlayerWindow.Coding.AiEvents.MultiModel.cs:13`, `Ai/Coding/CodingMultiModelFindingEventCommandWorkflow.cs:75`, `Ai/Coding/CodingMultiModelFindingEventWorkflow.cs:45` | Streckentracker, je Befund Code, Abdeckung, Folgebeleg, Ampel, Ereignis |

Die fünf Abschnitte von Station 2: (1) Laufzeit – ohne Dienst oder Abbruchquelle geschieht nichts;
(2) Bild aufnehmen und als Goldbild merken, OSD lesen; (3) Dateneinblendung überspringen;
(4) Aufnahmebindung – Endmeter nur mit Codiersitzung, DN, Meter genau einmal, Beleg, Klassifikator-Eingabe
(`CodingMultiModelClassifierInputPolicy`, gleicher Ordner); (5) Analysieren und verteilen.

Grenze und Struktur (BCD/BCE, BCA/BCC) zweigen in Station 3 in ihre eigenen Befehle ab
(`PlayerWindow.Coding.Ai.Classifier.Boundary.cs:13`, `…Structural.cs:11`). Der `CodingReplay`-Messhost
(`tools/CodingReplay/ReplayCodingAnalyzer.cs:245`) nutzt `CodingMultiModelInferenceWorkflow.ExecuteAsync`
(`Ai/Coding/CodingMultiModelInferenceWorkflow.cs:43`); dieser Einstieg verteilt ebenfalls über Station 3.

**Tests ohne Fenster:** `CodingEinzelbildAnalyseUseCaseTests` (Pipeline.Tests, nur Application) und
`CodingEinzelbildAblaufTests` (UI.Tests: Anwendungsfall plus Statusanzeige wie im Player; Erwartungen am Stand
vor AP09 aufgenommen und unverändert).

## Stand vor AP09 (d810e74bc)

Stationen 1–8 bis zur Ergebnisbehandlung: `RunCodingMultiModelAnalysisAsync` mit vier Ebenen verschachtelter
Aktionen → `CodingMultiModelAnalysisCommandWorkflow` → `CodingMultiModelRuntimeGateWorkflow` (zwei Nullprüfungen)
→ `CodingMultiModelAnalysisStartWorkflow` → `CodingEndMeterResolveWorkflow` → `CodingMultiModelInferenceWorkflow.ExecuteAnalyzedFrameAsync`
→ `…ExecuteAsync` → `CodingMultiModelClassifierInputPolicy`. Die vier Workflows und `ExecuteAnalyzedFrameAsync`
sind entfernt; Reihenfolge, Texte und Aufnahmebindung sind unverändert.

## Daten

- **Anfrage:** Mehrmodell-Dienst und Abbruchquelle aus `CodingAiController`, Aufnahmezeit (Sekunden) des Players.
- **Aufnahmebeleg** `CodingAnalyzedFrameEvidence` (`Application/UseCases/CodingPointFollowUp/CodingAnalyzedFrameEvidence.cs:8`):
  PNG-Bytes, Aufnahmezeit, einmal aufgelöster Meter mit Herkunft (`CodingMeterResolution`, `Application/Ai/CodingMeterResolver.cs:5`).
  Grenze, Struktur und Befunde verwenden genau diesen Beleg; nach der Inferenz wird nichts neu gelesen.
- **Analyseergebnis** `SingleFrameResult` und daraus `SegmentedFinding` liegen in Infrastructure. Der Anwendungsfall
  kennt sie nur als Typparameter und liest davon allein den Fehlertext.
- **Ergebnis** `CodingEinzelbildErgebnis`: Ausgang (kein Dienst, keine Abbruchquelle, kein Bild, Einblendung,
  Modellfehler, Grenze, Struktur, Ergebnis), Beleg und Analyse.
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
  PNG. Tests: `CodingEinzelbildAnalyseUseCaseTests`, `CodingEinzelbildAblaufTests`,
  `CodingMultiModelFindingEventCommandWorkflowTests`.
- **Menschliche Änderungen:** `CodingMultiModelEventAppender` übernimmt eine im `EventAdded` begonnene
  Bearbeitung und markiert sie; Tracker-Zeilen werden im selben Tick nach Bearbeitung nicht ergänzt.
  Tests: `CodingMultiModelEventAppenderTests`, `CodingMultiModelFindingEventWorkflowTests`, `CodingPointFollowUpPolicyTests`.
- **Abbau beim Schliessen:** Verlassen des Codiermodus und Schliessen des Fensters rufen
  `CodingAiController.DisposeAnalysisCancellation`/`Dispose` (`PlayerWindow.Wiring.cs:103`,
  `PlayerWindowCodingModeExitControllerFactory.cs:115`). Danach fehlt die Abbruchquelle und der Ablauf
  startet nicht; eine laufende Analyse endet mit Abbruch in `CodingAnalysisCommandWorkflow`. Das Token wird vor
  dem ersten Warten gelesen.
- **Detektor-Ampel:** Unqualifizierte Detektorbelege bleiben höchstens gelb (`CodingMultiModelQualityGatePolicy`).
- **Kein `ConfigureAwait(false)`** im Anwendungsfall: Die Anschlüsse greifen nach jedem Warten auf die Oberfläche zu.
