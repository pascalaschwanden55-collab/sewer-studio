# KI-Pipeline, Codiermodus und KI-Vorschläge

> Aus `CLAUDE.md` ausgelagert am 30.09.2026 (Wartbarkeitsaudit, Befund Z1). Der Text ist
> **unverändert** übernommen: Geltende Regeln stehen neben datierten Arbeitsständen und
> Messverläufen. Bei Widersprüchen gilt der jüngere Abschnitt und im Zweifel der Code
> samt seinem Test. Veränderliche Zahlen (Dienstanzahl, Testanzahl) sind Momentaufnahmen.
>
> Neue Erkenntnisse zu diesem Bereich hier eintragen, **nicht** in `CLAUDE.md`.

## Inhalt

- Fachregeln der Mehrmodell-Videoanalyse (Entscheid 01.10.2026)
- Grafik-Audit 23.09.2026: B01, B02, B04 behoben (24.09.2026)
- Gebundene Bild-/Zeit-/Meterbelege im Player (20.09.2026)
- Konservativer Folgebeleg-Abgleich (20.09.2026)
- Qualifizierte Detektorboxen im Codiermodus (20.09.2026)
- Bildklassifikator-Hinweis im Codiermodus (20.09.2026)
- Auditkorrekturen: Videoauswertung meldet ihre Luecken (18.09.2026)
- Aufbau der Mehrmodell-Videoanalyse (AP05, 30.09.2026)
- Aktueller Pipeline-Ablauf
- Codiermodus-Bildvergleich (20.09.2026, erste Messstufe)

## Fachregeln der Mehrmodell-Videoanalyse (Entscheid 01.10.2026)

- **Ein gelesener OSD-Meter gilt nur, wenn er plausibel ist.** Zusätzlich zu 0–500 m und
  gutem Bild muss er mit höchstens 5 m/s zum letzten **belegten** Meter passen, also zum
  letzten übernommenen OSD-Wert samt Bildzeit (`MultiModelLaufZustand.LetzterOsdMeter`),
  nie zur linearen Schätzung. Grenze und Vergleich stehen an einer Stelle:
  `MeterSequencePlausibility.IsReachable` (`MaxMetersPerSecond`, dieselbe Regel wie im
  Bogen-Copiloten), Toleranz 0,01 m für die Anzeigerundung. Langsames Rückwärtsfahren bleibt
  erlaubt; der erste Wert eines Laufs gilt wie bisher. Ein verworfener Wert lässt Meter und
  laufenden Meterstand unverändert und steht im Trace (`OsdMeterRejected`: «OSD-Meter
  unplausibel: x m nach y m in t s»). Nach einer Fortsetzung kommt der Anker aus dem Journal.
  Tests: `MultiModelOsdMeterPlausibilitaetTests`, `MultiModelQwenSchrittTests`.
- **Die Meterschätzung zählt vom letzten belegten OSD-Meter aus weiter** (01.10.2026). Nach jedem
  übernommenen OSD-Meter gilt geschätzt = Anker-Meter + Rate × (t − Ankerzeit), nie unter dem
  Anker; Rate wie bisher = angenommene Haltungslänge (`EstimatedReachLengthM`) je Videodauer.
  Anker ist derselbe Wert wie bei der 5-m/s-Prüfung (`LetzterOsdMeter`); ein verworfener Wert ist
  keiner. Ohne Anker unverändert die Gerade ab Videoanfang. Mit Anker entfällt
  `Math.Max(LastMeter, …)`: `LastMeter` wird bei der Übernahme auf den OSD-Wert gesetzt und wächst
  danach nur über die Schätzung. Beim Fortsetzen setzt ein OSD-Bild aus dem Journal `LastMeter`
  ebenso zurück (vorher Höchstwert aller Journalbilder). Eine Stelle:
  `MultiModelMeterSchaetzung.Schaetze`. Der Codiermodus (`CodingMeterResolver`) ist nicht
  betroffen. Tests: `MultiModelMeterAnkerTests`, Schnappschuss `gemischt`.
- **Ab zwei belegten OSD-Metern gilt die gemessene Geschwindigkeit** (01.10.2026). Rate =
  (m2 − m1) / (t2 − t1) der beiden letzten übernommenen OSD-Meter
  (`MultiModelLaufZustand.LetzterOsdMeter`/`VorletzterOsdMeter`, gesetzt nur über
  `UebernimmOsdMeter`, im Lauf und beim Fortsetzen aus dem Journal). Belastbar nur ab 1 s
  Abstand (`MultiModelMeterSchaetzung.MindestAbstandSek`: die 1-cm-Rundung verfälscht die Rate
  dann um höchstens 1 cm/s) und bis `MaxMetersPerSecond` (dieselbe Grenze wie die OSD-Folge);
  sonst die angenommene Rate. Rückwärts oder Stillstand: Rate 0, die Schätzung bleibt am Anker.
  Zwischen dem ersten und dem zweiten Anker gilt weiter die angenommene Rate (im Schnappschuss
  `gemischt` läuft die Schätzung dort weiter bis 38,86 m). Danach werden Folgebilder desselben
  Befunds näher beieinander geschätzt und von der Zusammenführung (1 m) wieder zu einem Befund
  verbunden. Codiermodus unverändert. Tests: `MultiModelMeterAnkerTests`, Schnappschuss `gemischt`.
- **Kein fremder Code bei Widerspruch.** `TemporalCodeVotingService` gibt einem Bild mit
  eigenem, vom bestätigten abweichenden Klassifikator-Vorschlag den bestätigten Code des
  Nachbarbilds nicht mehr über die Hysterese. Die Befunde des Bildes bleiben unbestätigt
  (bestehender Weg ohne bestätigten Code: Label-Code, Prüfung über das QualityGate). Ohne eigenen
  Vorschlag (auch LEER) hält das Mehrheitsfenster wie bisher; gleicher Vorschlag wie bisher.
  Gilt auch für den box-losen Grundgerüst-Befund (gleiche Stelle). Der Codiermodus-Einzelbildweg
  (`SingleFrameMultiModelService`) hat kein Voting und ist nicht betroffen.
  Tests: `TemporalCodeVotingServiceTests`, Schnappschuss `klassifikator_und_ausfall`.

## Grafik-Audit 23.09.2026: B01, B02, B04 behoben (24.09.2026)

Bericht `docs/audits/2026-09-23-code-grafik-plan/AUDIT-UND-PLAN.md`. Regeln, die nicht zurueckfallen duerfen:

- **KI-Markierungen im Player gehen durch `toPixel`, nie durch `Punkt.X * canvasWidth`.** Die Zeichenflaeche ist
  die ganze Flaeche, das Video darin formattreu (4:3 in 16:9 = seitlicher Rand). `CodingNormToPixel` kennt das
  Videorechteck (`CodingOverlayViewportMapper`). Rechteck, Linie und Punkt (`CodingAiRectangleOverlayRenderer`,
  `CodingAiPrimitiveOverlayRenderer`) rechneten mit der ganzen Flaeche und lagen bei 4:3-Videos daneben (B01);
  Bogen, Kreis und Massstab taten es schon richtig. `canvasWidth/Height` begrenzt nur noch die Beschriftung.
- **Passt die Beschriftung einer KI-Box nicht in die Flaeche, entfaellt sie** (vor dem ersten Layout ist der
  Canvas 1 x 1). Vorher warf `Math.Clamp` eine Ausnahme (B02).
- **`SamMaskDecoder.Downsample` setzt einen Anzeigepunkt, sobald IRGENDEIN Pixel seines Blocks gesetzt ist.**
  Die Stichprobe je Block liess 1 Pixel breite Risse ganz verschwinden (B04). Nur Anzeige; Vermessung und
  gespeicherte Maske bleiben unberuehrt. Die Geometrie rechnet je Achse `maskW/dsW` bzw. `maskH/dsH` zurueck.
- OFFEN, gefunden und bewusst nicht mitgeaendert: Der Rohrreferenzkreis (`CodingSchemaOverlayRenderer.AddPipeReference`,
  Fuellstand/Einragung) und der Referenz-DN-Kreis (`ReferenceDnGeometry`) setzen die Mitte noch mit der ganzen
  Flaeche; die Fuellstandlinie daneben verwendet `toPixel`. Der Kalibrierdurchmesser ist in normierten Koordinaten
  mehrdeutig (x und y verschieden skaliert, Radius = Norm x min(Breite, Hoehe)) und beruehrt die mm-Messung —
  eigenes Paket mit fachlicher Klaerung.
- Tests: `CodingAiOverlayRendererTests` (Pillarbox 1600 x 900 mit 4:3, Flaeche 1 und 20), `SamMaskDuenneStrukturTests`
  (senkrecht, waagrecht, schraeg, Einzelpunkt, HD, Fuellung), `SamMaskDecoderTests.Downsample_*`. Die neuen Tests
  waren vor der Korrektur rot (10 von 13).

## Gebundene Bild-/Zeit-/Meterbelege im Player (20.09.2026)

> Seit AP09 (30.09.2026) bildet `CodingEinzelbildAnalyseUseCase` (Application/UseCases/CodingEinzelbild) den
> Beleg; `ExecuteAnalyzedFrameAsync` ist entfernt. Aufrufweg: `docs/architektur/player-einzelbild-ablauf.md`.

- `CodingMultiModelInferenceWorkflow.ExecuteAnalyzedFrameAsync` bildet vor der
  Inferenz einen `CodingAnalyzedFrameEvidence` aus PNG, Aufnahmezeit und einmalig
  aufgeloestem Meter. Structural, BCE und TrackerCreateOpen verwenden diesen
  Beleg mit synchroner Fotoablage statt spaeteren globalen Quellen.
- `CodingMeterResolution.Source` unterscheidet SameFrameOsd/RecentOsd/
  VideoEstimate/SessionFallback. RecentOsd bleibt OSD; Folgebelegersetzung
  erfordert separat SameFrameMeterEvidence. Kein erneutes Lesen nach Inferenz.
- BCD behaelt seine Referenzposition und kennzeichnet Fotozeit, Bild-SHA und
  Herkunft getrennt in CodeMeta. Boundary-/Structural-Appender bewahren
  menschliche EventAdded-Aenderungen. Alte API-Einstiege bleiben kompatibel.
- Details und Verhaltenstests:
  `docs/quality/CODIERMODUS-AUFNAHMEBINDUNG-AUDIT-2026-09-20.md`.

## Konservativer Folgebeleg-Abgleich (20.09.2026)

- `Application/UseCases/CodingPointFollowUp` prueft eindeutige Geometrie, festen
  Ursprung (<1 m / <=15 s), gleiche Modell-/Bildquelle und staerkere YOLO-Belege
  bei erhaltener SAM-Qualitaet. Nur neue sitzungsregistrierte unveraenderte
  KI-Punkte duerfen ersetzt werden. Altbestand und menschliche Eingriffe sperren.
- Domain-Kontext additiv: `HumanTouchedAtUtc`, `ObservationHasTechnicalFailure`,
  flache `PreviousEvidence` aus `CodingProposalEvidenceSnapshot`. EventId/EntryId
  bleiben; Meter/Zeit/Bild/Masken/Herkunft wechseln gemeinsam mit altem Belegarchiv.
- Bestehende UI-Entscheidungs-/Edit-/Foto-/Delete-/Transferpfade markieren
  Beruehrung. MultiModel bindet Aufnahmezeit und `start.FrameBytes` an synchrone
  `AttachExactAnalyzedFramePhoto`, ohne spaeteren Screenshot-Ersatz. Replay
  speichert bytegleiche Framefotos und aktuelle/vorherige Ereignisbelege.
- Keine OSD-Maske, Modellaktivierung oder neuer Tracker. Kandidaten bleiben
  unqualifiziert. Regeln/Tests: `docs/quality/CODIERMODUS-FOLGEBELEGE-2026-09-20.md`.
- `CodingMultiModelFindingEventCommandWorkflow.ExecuteAnalyzedFrame` buendelt
  die Aufnahmebindung; alter Execute-Vertrag bleibt. Neue Trackerzeilen werden
  auch im selben Tick nicht nach menschlicher Bearbeitung ergaenzt. Offene
  Sonderpfad-Befunde: `docs/quality/CODIERMODUS-AUFNAHMEBINDUNG-AUDIT-2026-09-20.md`.

## Qualifizierte Detektorboxen im Codiermodus (20.09.2026)

- `Application/Ai/CodingLocalizedDetection` traegt Herkunft, Pixelbox, getrennte
  YOLO-/DINO-Werte, Hauptcode und Artefakthash. `SingleFrameResult.LocalizedDetections`
  und `SegmentedFinding.Origin` sind optionale additive Felder.
- `Infrastructure/Ai/Pipeline/CodingLocalizedDetectionPlan` bindet ausschliesslich
  qualifizierte, hashgleiche YOLO-Antworten an SAM. Gleiche Stelle/Hauptgruppe wird
  einmal segmentiert; fremde Gruppen bleiben getrennt. Ungueltige Quellen/Boxen,
  SONST und generisches BBD liefern keinen geratenen Code. Bildmasse aus bestehendem
  `ImageSizeReader`, kein neues Paket. Sidecar-Health erfasst Artefakt-SHA additiv.
- `SingleFrameMultiModelService` nutzt den Plan fuer „Jetzt analysieren“ und
  Live-Takt. Ohne passende Qualifikation/Identitaet bleibt DINO/SAM der Rueckfall;
  ungebundene YOLO-Antworten sind auch kein Negativfilter. Keine Modellaktivierung.
- `CodingAnalysisContext` gibt Herkunft an den bestehenden Segmentbuilder weiter.
  Player-Mapper, Ereignisfactory und QualityGate werten echte Befundquellen aus;
  YOLO wird nicht DINO genannt, Katalogtitel nicht als Modellstimme gezaehlt.
  Nur exakt auswaehlbare Hauptcodes; kein importbasiertes Verfeinern neuer
  YOLO-Vorschlaege. Modellfehler begrenzen deren Gruen auf Gelb.
- Neue offene Strecken erhalten denselben Erstbeleg, begrenzt auf im Tick neu
  erzeugte EventIds. Vorhandene Zusatzwerte bleiben erhalten. Die bestehenden
  `SuggestedByModelSha256`- und CodeMeta-Felder tragen Modellherkunft.
- Keine DI-/Paket-/Speicherformatumstellung, keine neue UI/Ai-Datei. Der separate
  `MultiModelAnalysisService`-Batchweg bleibt unveraendert. Verhaltenstests:
  `SingleFrameLocalizedDetectionTests`, `CodingLocalizedDetectionPlanTests`,
  `CodingDetectorEventIntegrationTests`; Anleitung/Grenzen:
  `docs/quality/CODIERMODUS-DETEKTOREREIGNISSE-2026-09-20.md`.
- Expliziter Messhost-Einstieg `AnalyzeCandidateFrameAsync` mit
  `CodingDetectorCandidateFrame`: Bild-SHA und erwartete/tatsaechliche Gewicht-SHA
  werden vor Health/Modellaufrufen gebunden. `BuildCandidate` verwendet dieselben
  Regeln ohne vorgetaeuschte Qualifikation. Result bleibt unqualifiziert/degraded,
  Quellen DevelopmentCandidate/RequiresReview, Ereignisampel maximal gelb.
  Kein Kandidatenparameter im normalen Playeraufruf; kein negativer Bildfilter
  durch leere Kandidatenantwort. Technische Antwortfehler muss der Messhost separat
  halten. Bestehende Klassifikator-Abkuerzungen sind kein Detektorkandidatenbeleg.
- `tools/CodingReplay run-video` haelt eine Sitzung ueber die feste Videobildfolge.
  `run-video-candidate` prueft separat erfasste Kandidatenboxen mit Bild-/Gewichts-SHA
  und nutzt den expliziten Einstieg. Lauf/Frames tragen
  `development_candidate_unqualified`; Ereignissnapshots behalten Quelle, Hash
  und Zweck. Keine Aktivierung und keine gemessene Live-Timer-Leistung.
- DINO-Warmup verwendet seit dem belegten `topk`-Fehler im Videolauf ein neutrales
  640x640-Bild: 64x64 bietet zu wenige Positionen fuer Swin-Bs 900 Queries.
  YOLO/Klassifikator behalten den bisherigen Dummy; echte Bildvorverarbeitung
  bleibt gleich. Vier CPU-Warmup-Tests schuetzen Erfolg, Fehler und Qualifikation.

## Bildklassifikator-Hinweis im Codiermodus (20.09.2026)

- `Application/UseCases/CodingClassifierHint/CodingClassifierImageHint` erhält ein
  bereits aufgelöstes BAB-/BAF-/BAI-/BAJ-/BBA-/BBB-Bildsignal als ungeprüften Hinweis.
  Klartext kommt vom aktiven Katalog; fehlender Text zeigt nur die Schadensgruppe.
  Kein Ereignisentwurf, keine Maskenzuordnung, kein Untercode oder Ortsnachweis.
- `CodingMultiModelAnalysisResultWorkflow` ergänzt den Hinweis am endgültigen
  Status, damit die folgende DINO-/SAM-Anzeige ihn nicht wieder überschreibt.
  Ohne lokalisierte Detektion bleibt das Ergebnis `ReviewRequired` statt grünem
  `NoDamage`. Modellfehler bleiben vorrangig. Segment-, Nähe-, Ereignis- und
  Detektorqualifikationsregeln bleiben erhalten; der Hinweis ist gelb.
- Bestehende öffentliche Workflow-Verträge und gespeicherte Daten bleiben gleich.
  Keine neue Registrierung, Pakete, Gewichte oder KI-Aufrufe. Keine neue UI/Ai-Datei.
  Nachweis: `CodingClassifierImageHintTests` und
  `CodingMultiModelAnalysisResultWorkflowTests.Classifier.cs`.
- Anlass: historischer Einzelbildvergleich mit fünf passenden Hauptgruppen bei
  sieben auswertbaren Bildern, aber keinem passenden Ereigniscode. Dies belegt
  Informationsverlust in diesem Weg, keine unabhängige Erkennungsquote.
  Bericht und Grenzen: `docs/quality/CODIERMODUS-BILDHINWEIS-2026-09-20.md`.

## Auditkorrekturen: Videoauswertung meldet ihre Luecken (18.09.2026)

Sieben Befunde mit derselben Wurzel: Ein Teillauf sah aus wie ein vollstaendiger.

- **Ein unvollstaendiger Lauf muss bis ins Gesamtergebnis sichtbar bleiben.**
  `MultiModelRunCompleteness` (Infrastructure/Ai/Pipeline) sammelt die verlorenen Schritte
  eines einzelnen Laufs: vorzeitiges Frame-Ende, technische SAM-Verluste und Qwen-Fehler.
  `CanCompleteJournal` gibt den Checkpoint nur bei wirklich vollstaendiger Extraktion und
  null Fehlerframes frei — ein abgeschnittener Lauf darf nie als abgeschlossenes
  Wiederaufnahme-Journal enden, sonst setzt der naechste Lauf hinter dem Abbruch auf.
  `Incomplete` heisst seither nicht mehr nur «>10 % Fehlerframes», sondern «nicht
  vollstaendig ausgewertet»; der Grund steht in `Warnings` beziehungsweise `DegradedReason`.
- **Ein als Wert zurueckgegebener Modellfehler ist kein Erfolg.** Qwen/Ollama liefert bei
  HTTP-Fehler oder kaputtem JSON ein Fehlerergebnis statt einer Ausnahme. Der Aufrufer
  wertet jetzt `Error`/`Outcome` VOR der Erfolgsmeldung aus. Ein gueltiges leeres Ergebnis
  bleibt ein Erfolg, und ein Nutzerabbruch wird weitergereicht statt als Modellfehler
  journalisiert.
- **Eine bewusst verworfene SAM-Maske ist kein technischer Fehler.**
  `MultiModelRunCompleteness.RecordSam` trennt beides: Nur was ueber die vom Modell
  gemeldeten `LowScoreBoxes` hinaus fehlt, einen `Error` traegt oder `Degraded` ohne
  fehlende Maske meldet, zaehlt als Fehlerframe. Ohne diese Trennung wuerde jede
  absichtliche Score-Verwerfung einen Frame zum Wiederholungsfall machen.
- **Die Darstellung darf die fachliche Auswahl nicht begrenzen.** `PipelineResultPresenter`
  hatte `Take(250)`; Befund 251 war nicht auswaehlbar und wurde beim Uebernehmen aus
  `Current` UND `Original` entfernt — ein stiller Datenverlust. Die Kappung ist weg.
  Nie wieder eine Anzeigegrenze vor die Uebernahmeliste setzen.
- **Der Rohrdurchmesser gehoert zum Auftrag.** `PipelinePipeDiameterPolicy`
  (Application/Ai) liest den Haltungs-DN, gibt ihm Vorrang vor der globalen Vorgabe und
  benennt die Grenzen: fehlender DN heisst keine Millimeterwerte, eine abweichende globale
  Vorgabe wird genannt statt still verwendet, und die 70-%-Bildbreitenannahme wird als
  Schaetzung ausgewiesen. `PipelineRequest.PipeDiameterMm` und `PipelineConfig.PipeDiameterMm`
  sind additiv. Vorher rechnete die Stapelanalyse jede Haltung mit DN 300 — aus 94 mm
  wurden 47 mm und aus Stufe 3 die Stufe 2.
- **Zwischen zwei Bildern wird raeumlich zugeordnet, nie nach Listenplatz.**
  `TemporalFindingDeduplicator` merkt je aktivem Befund die letzte brauchbare Box
  (`LastBox`) und ordnet gleichcodierte Treffer ueber dieselbe IoU-Schwelle zu, die schon
  innerhalb eines Bildes trennt. Ohne Geometrie wird nur der eindeutige Einzelfall
  fortgesetzt — mehrere aktive Schaeden duerfen NICHT nach Reihenfolge verteilt werden.
  Vorher uebernahm bei vertauschter Modellreihenfolge ein Befund Ausdehnung und Stufe des
  anderen (20 %/Stufe 1 wurde zu 80 %/Stufe 4), ohne dass sich am Bild etwas geaendert hatte.
- **Eine spaete Fortschrittsmeldung darf den Abschluss nicht ueberschreiben.**
  `PipelineProgressMapper` bricht bei `IsDone` ab; `PipelineResultPresenter.ApplyCompletion`
  setzt Warnungen, Phase («Unvollstaendig» / «Fertig mit Hinweisen» / «Fertig») und
  Statustext gemeinsam. Ein neuer, gesunder Lauf raeumt alte Warnungen weg.
- Tests: `MultiModelCompletionFailureTests` (9, mit echtem ffmpeg und echtem Ollama-Client
  hinter kuenstlichem HTTP-Handler), `TemporalFindingDeduplicatorContinuityTests` (7),
  `VideoAnalysisPipeDiameterTests` (3), `PipelineCompletionSafetyTests` (4) und
  `PipelineCompletionWindowTests` (2, echtes WPF-Fenster im Kindprozess).
  Sabotageprobe 18.09.2026: `Take(250)` zurueckgebaut -> 4 Tests rot; Frame-Zuordnung
  abgeschaltet -> 8 Tests rot.
- Grenzen: Die Erkennungsqualitaet der Modelle ist unveraendert und weiterhin nicht
  freigegeben. Der Rohrdurchmesser macht die Maskenmasse nachvollziehbar, nicht kalibriert.
  Abnahme: `docs/audits/2026-09-18-videoauswertung/BEHEBUNG.md`.

## Aufbau der Mehrmodell-Videoanalyse (AP05, 30.09.2026)

`MultiModelAnalysisService.AnalyzeAsync` ist in drei Teile geordnet (reine Umordnung, gleiches Verhalten):

- **Laufzustand:** `MultiModelLaufZustand` haelt alles, was genau einen Lauf lebt (Befunde, Dedup,
  Code-Voting, Zaehler, Ausfallschutz, Vollstaendigkeit, Resume-Stand, Qualifikation, Neustart-Budget).
  Der Dienst hat zwischen zwei Laeufen keinen Laufzustand mehr.
- **Ein Bild = ein Schritt:** `ProcessFrameAsync` liefert nur einen `MultiModelBildErgebnis`
  (uebersprungen, ohne Box, Grundgeruestbefund, Befunde, erneut noetig mit `MultiModelFehlerart`
  Transport/Kapazitaet/Modell). `BookFrameResultAsync` bucht daraus Trace, Dedup, Checkpoint und
  Fehlerzaehlung in der belegten Reihenfolge und entscheidet ueber Fortsetzung oder Abbruch.
  Achtung: Beim DINO-Negativbefund steht der Checkpoint VOR dem Trace, sonst danach.
- **Qwen-Schritt:** `MultiModelQwenSchritt` (eine Instanz je Lauf, haelt den Vorbefund-Kontext).
  Seit AP05b liegen auch YOLO, DINO, SAM und der cls-Vorfilter in eigenen Klassen (siehe unten);
  ihre Regeln sind bewusst nicht vereinheitlicht.
- **Modellschritte (AP05b, 30.09.2026):** `MultiModelClsVorfilter`, `MultiModelYoloSchritt` (Umgehung,
  Qualifikationsentzug: nur ausdrueckliches `qualified=true` filtert, Klassenschwellen, COCO-Warnung),
  `MultiModelDinoSchritt` (degraded = Modellfehler, ohne Box, Grundgeruest-Befund; seit 01.10.2026 haengt
  DINO-degraded seinen Grund ueber `MarkTraceDegraded` an, ein vorheriges `detector_unqualified` bleibt im
  Trace) und `MultiModelSamSchritt`
  (Vollstaendigkeit, `LowScoreBoxes` kein Fehler, Quantifizierung, Befundbau). `MultiModelBildKontext` traegt
  die Werte eines Bildes. `MultiModelSidecarAufruf` ordnet nur die Fehlerart ein (Nutzerabbruch weiterwerfen,
  VRAM = Kapazitaet, sonst Transport); Folgen, Log- und Fortschrittstexte bleiben beim Modell. Der Vorfilter
  faellt bei jeder Fehlerart weich zurueck. Tests je Schritt: `MultiModel*SchrittTests`,
  `MultiModelClsVorfilterTests`, `MultiModelSidecarAufrufTests`.
- Schutz: `MultiModelAnalysisReferenceSnapshotTests` vergleicht fuenf kontrollierte Laeufe zeichengleich
  (Ereignisfolge aus Modellaufrufen, Trace, Checkpoint, Fortschritt, Log sowie Ergebnis und Befunde;
  Schnappschuesse unter `tests/AuswertungPro.Next.Pipeline.Tests/Snapshots/MultiModel/`). Neu schreiben
  nur bewusst mit `SEWERSTUDIO_SNAPSHOT_UPDATE=1`. Dazu `MultiModelFrameOutcomeTests` und
  `MultiModelQwenSchrittTests`.

## Aktueller Pipeline-Ablauf
1. UI/Service startet Analyse ueber `VideoAnalysisPipelineService`, `SingleFrameMultiModelService` oder `VideoFullAnalysisService`.
2. C# ruft den Sidecar ueber `VisionPipelineClient` auf.
3. Sidecar verwaltet Modell-Locks und GPU-Slots in `sidecar/sidecar/gpu_manager.py`.
4. Multi-Model-Pfad: YOLO -> DINO -> SAM -> Quantifizierung -> optional Qwen.
5. C# mappt VSA-Code, dedupliziert framebasiert und laesst `QualityGateService` laufen.

### Bogen-Vorschlags-Subsystem (Bogen-Copilot, seit 2026-08-09 produktiv)

Vorabdurchlauf ueber ein Video im Training Studio (Expander "Bogen-Vorschlaege"),
der verdaechtige Bogen-Stellen als Liste zum Bestaetigen liefert — bewusst kein
Live-Overlay im Player. Aufbau: `BendSuggestionScanWorkflow` →
`IBendSuggestionScanService`/`BendSuggestionScanService` (Verdrahtung) →
`BendSuggestionScanUseCase` (Ablauf: alle Bilder fragen, Meterfolge ueber ALLE
Bilder, erst `MeterSequencePlausibility`, dann `MeterSequenceGapFiller`,
Zusammenfassung im `BendSuggestionAggregator`). Der Meterstand kommt als
`meter_value` in derselben Sidecar-Antwort (`/detect/yolo/bcc-test`), der
Format-Lock (`meter_format`) erzwingt optional das OSD-Zahlenlayout. Ohne
kalibrierten Arbeitspunkt (`workpoint.json` neben dem Kandidaten, gelesen ueber
`BendSuggestionCalibrationFileStore`/`BendSuggestionCalibrationPolicy`) laeuft
gar nichts; Kandidaten-ID und Gewicht-Hash gehen mit jeder Anfrage und werden an
der Antwort erneut geprueft. Zeilen zeigen Ort (gelesen als `Meter 9,42`,
geschaetzt mit Zusatz, fehlend als `Sekunde … (Meterstand nicht lesbar)` —
niemals `0,0`), Stufe (stark/schwach), Konfidenz und Bildzahl; Doppelklick oder
"Gross anzeigen" oeffnet `BendSuggestionPreviewWindow` mit Spitzenbild und Clip
(`IVideoClipExtractor`/`VideoClipExtractionService`, drei Haerten wie der
Bildfolgen-Extraktor). Das Sitzungsgedaechtnis `ICodingSuggestionExposure`
(`CodingSuggestionExposure`, Singleton) merkt je Programmlauf, fuer welche
Haltungen eine Liste angesehen wurde: Der `CodingEventToSampleMapper` meldet
dann auch ohne Ereignis-KI-Kontext `SuggestionShown` statt `Independent`, damit
der Assistent den unbeeinflussten Messbestand fuer `ModelPromotionPolicy` nicht
verbrennt. Abnahme: `BendSuggestionLiveAcceptanceTests` (maschinengebunden)
gegen die Repo-Fixture `tests/Fixtures/BendSuggestions/` — 226 Einzeltreffer
ohne Abweichung zum Prototyp, fuenf Stellen feldgleich. Messgrundlagen:
`docs/quality/BCC-COPILOT-2026-08-08.md` und `BCC-PDF-RECALL-2026-08-09.md`
(77,6 % Recall auf 85 protokollierten Boegen, 60,3 % Precision nach blinder
Clip-Pruefung).

### Rohranfang und Rohrende im Vorabdurchlauf (seit 2026-09-04)

Dieselbe Liste im Training Studio (Expander "Vorschlaege aus dem Video-Durchlauf")
zeigt neben den Boegen je Video hoechstens EINEN Rohranfang (BCD) und EIN Rohrende
(BCE). Quelle sind die zwei freigegebenen Lernstufen des Sidecars
(`GET /classify/lernstufen`, `POST /classify/lernstufe`; Freigaben unter
`C:\KI_BRAIN	raining\lernstufenreigaben`, Regel "staerkste gruppierte Meldung
im GANZEN Video, kein Zeitfenster", Abnahme 2026-08-12 an Clips: Rohranfang
Precision 85 % / Recall 98 %, Rohrende 89 % / 88 %). Aufbau:
`PipeEndSuggestionScanWorkflow` -> `IPipeEndSuggestionScanService`/
`PipeEndSuggestionScanService` (Verdrahtung) -> `PipeEndSuggestionScanUseCase`
(Bilder einmal holen, je Klasse NACHEINANDER ueber alle Bilder, weil beide
Lernstufen den Slot `YOLO_TEST` teilen und ein Wechsel je Bild das Gewicht neu
laden wuerde) -> `PipeEndSuggestionRule` (Boden 0,10, Luecke 3 s, Schwelle 0,50,
Rohrende blendet die ersten 3 s aus, Rohranfang nicht; bei Gleichstand die
fruehere Stelle). `PipeEndLernstufePins` pinnt Klasse und Gewicht-SHA-256; der
Sidecar-Client (`ILernstufeClient`, eigener kleiner Vertrag neben
`IVisionPipelineClient`) prueft beides an jeder Antwort. Die Lernstufen lesen
keinen Meterstand; die Zeile nennt ehrlich die Videosekunde, die Stufe heisst
"Abnahme 85 %" statt stark/schwach. Der Durchlauf laeuft NACH dem Bogen und
unabhaengig von dessen Arbeitspunkt; jeder Durchlauf ersetzt die Liste ganz.

Drei Regeln nie zurueckdrehen:

- **Der Sidecar letterboxt VOR dem predict** (`lernstufe_wrapper.einordnen`,
  `_letterbox_rgb(bild, imgsz)`). Die Gewichte tragen nur Resize+CenterCrop;
  ohne Letterbox schnitt Ultralytics von 720x576 links und rechts je 80 Pixel ab.
  Gegenprobe 2026-09-04 auf `07.6588-6587`: bis 0,79 Abweichung je Bild und ein
  verschobener Spitzenmoment beim Rohranfang; mit Letterbox ist der Sidecar-Weg
  bildgenau der Abnahmeweg (`lernstufe_vorschlagspruefung.py`, `letterbox_pil(640)`).
- **Abtastrate 1 Bild je Sekunde, imgsz 640, Schwelle 0,50** sind Teil der Freigabe,
  keine Stellschrauben. Wer sie aendert, misst die Freigabe neu.
- **Ein anderes Gewicht braucht eine neue Freigabe UND neue Pins** (SHA, Precision,
  Recall) — die Messung gehoert zum Gewicht.

`PipeEndSuggestionLiveAcceptanceTests` (MachineIntegrationFact) laesst den echten
C#-Weg gegen die Repo-Fixture `tests/Fixtures/PipeEndSuggestions/` laufen; die
Soll-Werte stammen aus dem Abnahmeweg (Modell direkt) auf demselben Video.

### KI-Vorschlaege im Codiermodus (seit 2026-09-05)

Beim Eintritt in den Codiermodus laeuft `CodingSuggestionScanUseCase`
(`Application/UseCases/CodingSuggestions`) im Hintergrund: zuerst der Bogen-Durchlauf
mit dem festen Pin `CodingBendCandidatePin` (`bcc_nc15_seed46_20260808`), dann
Rohranfang/Rohrende ueber die gepinnten Lernstufen. Ergebnis ist ein
`CodingSuggestionSet` mit Meterspur (`BendSuggestionScanResult.MeterTrack`). Die Karte
"KI-Vorschlaege" im Seitenpanel und die Marker unter dem Regler zeigen es; Bestaetigen
folgt `CodingSuggestionConfirmPolicy`: Bogen oeffnet das Codierfenster mit `BCC`,
Rohranfang legt BCD bei 0 m an, Rohrende legt BCE mit dem Spurmeter an und schlaegt bei
leerer `Haltungslaenge_m` diesen Wert vor (`FieldSource.Protocol`). Schalter:
`AppSettings.CodingSuggestionsEnabled` (Standard ein). Die Verdrahtung im Player liegt
in `PlayerWindow.Coding.Suggestions.cs`; der Abbruch beim Verlassen haengt am
Teardown neben der Import-Referenz.

Vier Regeln nie zurueckdrehen:

- **Bogen vor Anfang/Ende, nie parallel** — alle drei Gewichte teilen `YOLO_TEST`.
- **Jeder Teil faellt fuer sich aus**; ein technischer Fehler ist `Fehler` mit Text,
  nie eine leere Liste. `OperationCanceledException` geht immer durch.
- **Ein geschaetzter Meter wird nie Vorgabe oder Laenge**; ein fehlender Meter wird nie
  als `0,0` gezeigt.
- **Mindestens ein gezeigter Vorschlag markiert die Haltung im Sitzungsgedaechtnis**
  (`ICodingSuggestionExposure`), damit Goldsamples dieser Haltung `SuggestionShown`
  tragen und den unbeeinflussten Messbestand nicht verfaelschen.

## Codiermodus-Bildvergleich (20.09.2026, erste Messstufe)

- `Application/UseCases/CodingReplay/CodingReplayUseCase` führt Bilder seriell mit
  Hashprüfung, Pflichtkontext, Zeitlimit, Abbruch und Einzelfehlerbelegen aus.
  Sollcodes gelangen nur in `CodingReplayComparer`, nie in `ICodingReplayAnalyzer`.
  Ein Schreibfehler stoppt den Lauf; Modellfehler sind keine Negativbefunde.
- `tools/CodingReplay` ist ein eigener Windows-CLI-Host in der vollständigen Lösung.
  `prepare` bindet eingefrorenes Eval-Set, vorhandene menschliche Review und
  eindeutig zugeordnete Projekt-Stammdaten an ein neues Paket. Originale bleiben
  unverändert. `run` nutzt bestehende öffentliche Player-Workflows und
  `SingleFrameMultiModelService`; keine App-Instanz und kein Projekt-Save.
- Meter kommen aus `CodingOsdMeterService` mit unverändertem 8-Sekunden-Limit.
  Pro Bild beginnen Sitzung/Tracker leer, ohne Importbefunde, Kalibrierung oder
  Verlaufswerte. DINO/SAM, Codezuordnung, räumliche Filter, QualityGate sowie
  Grenz-/Strukturereignisregeln bleiben bestehen. `ReplayClosedTrainingStore`
  sperrt Training. Sidecar-Qualifikation wird respektiert, keine automatische
  Modellaktivierung. Der Host liest Einstellungen ohne `AppSettings.Load`.
- HTML/JSON zeigen tatsächliche `session.Events`, nicht nur den Workflowausgang
  `EventsAdded` (der auch bei null Vorschlägen auftreten kann). Pro Bild getrennt:
  genauer Code, gleiche Hauptgruppe, fehlender Referenzvorschlag, Schadensvorschlag
  bei negativer Referenz, manuelle Prüfung oder technisch nicht gemessen.
- Historische Bilddiagnose, keine Video-/Release-Messung: Vorabdurchlauf, Qwen-only,
  Bildauswahl, Live-Takt, Oberfläche, Verlauf und zeitliche Zusammenführung fehlen.
  Gewichtsbindung des Sidecars, Jobwarteschlange und Wiederanlauf sind noch offen.
  Keine neue DI-Registrierung, NuGet-Abhängigkeit oder Änderung am Produktionsweg.
- Anleitung: `tools/CodingReplay/README.md`. Tests: `CodingReplayUseCaseTests`,
  `CodingReplayComparisonTests`, `CodingReplayAnalyzerTests`.

