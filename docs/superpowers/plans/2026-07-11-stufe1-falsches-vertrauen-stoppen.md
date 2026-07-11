# Stufe 1: Falsches Vertrauen stoppen — Umsetzungsplan (11.07.2026)

Basis: Fehlerpruefung 11.07. (3 kritische Befunde, verifiziert), Stand 9f490b6dc.
Reihenfolge A -> C -> B. Details/Zeilennummern vom Plan-Agenten verifiziert.

## A) Severity nicht mehr als Confidence
- A1 LiveDetectionModels.cs: LiveFrameFinding + `double? ModelConfidence = null, string? ConfidenceSource = null` (ans Ende).
- A2 CodingLiveFindingQualityGatePolicy: QwenVisionConf = ModelConfidence (null statt Severity/5); Gate-null-Fallback = Red/0.0 statt Yellow/Severity.
- A3 AiFindingDisplayItem: ConfidencePercent int? aus ModelConfidence; Text "Sicherheit: n/v" + graue Farbe bei null; Severity bleibt getrennt (SeverityText).
- A4 VideoPipelineContracts.cs:115: Kommentar "abgeleitet, nur Anzeige/Overlay, KEIN Freigabe-Signal".
- Tests: n/v-Anzeige; QwenVisionConf null; Gate-null -> Rot. Bestehende CodingLiveFindingQualityGatePolicyTests bewusst anpassen.

## C) KB-Validierung blind
- C1 Neu: Application IKbBlindValidationService + Infrastructure KbBlindValidationService.
  KbValidationHit(CaseId, VsaCode, Score, HumanConfirmed?); KbValidationResult(Agrees, BestHit, Reason).
  Blinde Query NUR Label/Meter/Uhrlage/Quantifizierung (kein HaltungId/VsaCodeHint/suggestedCode).
  Pure Kernfunktion EvaluateHits: Same-Case-Ausschluss (CaseId==HaltungId), HumanConfirmed==true Pflicht,
  MinValidationScore=0.75 (benannt, unkalibriert), Agrees nur bei gleichem Hauptcode des besten Treffers.
  SampleRecord traegt CaseId+HumanConfirmed und kommt im Retrieval an (RetrievalService.cs:274-281) - verifiziert.
- C2 FullProtocolGenerationService: kbExamples fuer Prompt unveraendert; NACH LLM ValidateAsync; KbCodeAgreement = validation.Agrees; alte kbAgrees-Zeilen (~244-246) raus.
- Tests (pur, EvaluateHits): SameCase kein Beleg; Score<0.75 kein Agreement; unbestaetigt kein Agreement; bestaetigter fremder gleicher Code -> Agreement; Query ohne HaltungId/Code.

## B) Strukturiertes Urteil + Auswahl vor Uebernahme
- B1 MappedProtocolEntry + `AiDecision? Freigabe = null` + `Guid EntryId` (in MapDetectionAsync vergeben; ProtocolEntryFactory.BuildProtocolEntry:108 uebernimmt statt Guid.NewGuid()).
- B2 AutoApprovalService: neue `AiDecision Decide(MappedProtocolEntry)` (3 Outcomes); Evaluate = bool-Adapter darueber; AlsHinweis(AiDecision)-Ueberladung (verlaesslich/pruefen/ablehnen). MitZentralerFreigabe setzt Freigabe strukturiert.
- B3 Neu Application: AiProtocolAcceptancePolicy.Apply(ProtocolDocument, ISet<Guid> keepEntryIds) -> gefiltertes Dokument, setzt Ai.Accepted=true fuer uebernommene (Matching EntryId).
- B4 DetectionItem: + EntryId, Outcome, OutcomeLabel, Reason, IsSelected (Default = AutoAccept; INotifyPropertyChanged falls noetig); FromMapped fuellt.
- B5 VideoAnalysisPipelineWindow: MappedEntries via FromMapped anzeigen (Fallback Detections); CheckBox+Badge+Grund im Template; Accept_Click: Zusammenfassungs-Confirm (n/m/k, uebernommen x); Property SelectedEntryIds.
- B6 DataPageVideoAnalysisController.Open (89-107): gefiltertes Dokument via B3 statt result.Document; Delegat-Signatur erweitern (DataPageViewModel.cs:287/690).
- Tests: Decide 3 Outcomes; FromMapped Vorauswahl (AutoAccept ja / Review nein); AcceptancePolicy nur Auswahl + Accepted-Flag; leere Auswahl leeres Protokoll; Integrationstest Fake-Retrieval Same-Case -> Freigabe Review.

## Risiken
- CodingLiveFindingQualityGatePolicyTests (3) brechen bewusst; AiFindingDisplayItem-/ControlsTests pruefen; AutoApprovalTests bleiben (Adapter).
- VideoAnalysisPipelineWindow-XAML neue Bindings -> xaml-binding-check.
- Verhaltensaenderung: Uebernahme default nur AutoAccept-Zeilen; KbAgreement deutlich strenger -> weniger AutoAccept (gewollt); 0.75 unkalibriert.
