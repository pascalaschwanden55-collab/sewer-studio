# Phase 0 — Self-Training Gate-Härtung (Design / Spec)

**Datum:** 2026-06-04
**Branch:** feature/gis-karte
**Status:** Entwurf zur User-Review
**Vorgeschichte:** Self-Training-Audit (2026-06-03) + Grundlagen-Verifikation gegen den echten Code (Multi-Agent-Workflow, adversarisch gegengeprüft). Siehe Memory `self-training-audit-2026-06`.

---

## Ziel

Die Sicherheits-Gates des Self-Trainings so härten, dass ein **unbeaufsichtigter Batch** über die ~3000 Videos keine falschen Labels automatisch ins Training übernimmt — bevor dieser Batch zum ersten Mal läuft.

## Sicherheitsrahmen (wichtig zum Verständnis)

Solange `TrainingCenterSettings.RequireHumanReview == true` (heutiger Default, [TrainingCenterSettings.cs:22](../../../src/AuswertungPro.Next.Application/Ai/Training/TrainingCenterSettings.cs#L22)) wird **nichts** automatisch als Gold/KB übernommen — jedes Sample geht in die ReviewQueue. Die hier gebauten Gates werden erst scharf, wenn Pascal diesen Schalter für den unbeaufsichtigten Nachtlauf ausschaltet. **Genau dafür sind sie das Sicherheitsnetz.** Phase 0 ändert den Default-Schalter NICHT.

## Was die Code-Verifikation am ursprünglichen Audit korrigiert hat

- **Punkt C ist halb ein Fehlalarm:** Die VSA-Code-Strings im `StreckenschadenCodes`-Set ([VsaCodeTree.cs:500-532](../../../src/AuswertungPro.Next.Domain/VsaCatalog/VsaCodeTree.cs#L500-L532)) sind korrekt; nur die **Kommentare** lügen (z.B. `"BBA", // Ablagerung` — laut CLAUDE.md ist BBA = Wurzeln). Die behauptete KB-Lücke (Phantasiecode „BBZ" rutscht durch) existiert nicht, weil der Katalog keine 2-Zeichen-Codes hat und unbekannte Codes bei StageA bereits über `InvalidCatalogCode` abgelehnt werden ([StageAExporter.cs:245-250](../../../src/AuswertungPro.Next.Application/Ai/Training/StageAExporter.cs#L245-L250)).
- **Gate B liegt anders als gedacht:** Ein Hard-Block im StageA-Export würde **menschlich freigegebene** Samples überstimmen (StageA exportiert nur `Status==Approved`; im strengen Modus ist alles dort von Hand freigegeben). Der korrekte, nicht-überstimmende Ort ist die **Auto-Freigabe-Entscheidung selbst** — dort, wo im Batch-Modus überhaupt automatisch Gold entsteht. Siehe Gate B unten.

---

## Die vier Gates

### Gate C-Teil1 — Falsche VSA-Kommentare korrigieren *(trivial, kein Verhaltensrisiko)*

**Problem:** Inline-Kommentare und Docstring in [VsaCodeTree.cs:495-531](../../../src/AuswertungPro.Next.Domain/VsaCatalog/VsaCodeTree.cs#L495-L531) widersprechen CLAUDE.md und der autoritativen `Groups["BB"]`-Definition. Ein künftiger Editor könnte „korrigieren" und damit funktionierende Logik zerstören.

**Korrekte BB-Bedeutung** (CLAUDE.md / `Groups["BB"]`):
- `BBA` = Wurzeln/Bewuchs *(Kommentar sagt fälschlich „Ablagerung")*
- `BBB` = Anhaftende Stoffe / Inkrustation / Fett *(Kommentar sagt fälschlich „Infiltration")*
- `BBC` = Ablagerung (A=Sand, B=Kies, C=verfestigt) *(Kommentar sagt fälschlich „Wurzeleinwuchs")*
- `BBD*` = Eindringender Boden, nur Untercodes *(Kommentar sagt fälschlich „Anhaftung/Inkrustation")*

**Änderung:** NUR die Kommentar-Texte (Zeilen 497-498 Docstring, 520/523/525/529 inline) an die Wahrheit angleichen. **Die HashSet-Werte NICHT anfassen** — sie sind korrekt. Im aktiv genutzten Set ([VsaCodeResolver.cs](../../../src/AuswertungPro.Next.Infrastructure/Ai/VsaCodeResolver.cs)) einen kurzen Verweis-Kommentar auf `Groups["BB"]` als Wahrheitsquelle setzen.

**Vorbedingung im Plan:** `Groups["BB"]` (VsaCodeTree.cs:124-167) gegen CLAUDE.md prüfen, bevor die Kommentare daran ausgerichtet werden.

**Test:** Keine Verhaltensänderung → kein neuer Pflicht-Test. Optional ein Theory-Test, der `BBA→"Wurzeln"`, `BBB→"Anhaftende"`, `BBC→"Ablagerung"` gegen die Katalog-Labels assertet, damit die Vertauschung nicht zurückkehrt.

---

### Gate C-Teil2 — VsaCodeValidator als Vorsorge *(User-Entscheidung; bewusst über Empfehlung hinaus)*

**Kontext der Entscheidung:** Die den Validator motivierende Lücke existiert im echten Katalog nicht. Pascal hat den Validator dennoch als Vorsorge gewählt. Er wird gebaut — aber **risikoarm verdrahtet**.

**Wo er Nutzen hat:** Beim **Eintritt** von Codes als Trainings-Label. `ExtractEntriesFromChunkText` ([TrainingCenterImportService.cs:401-425](../../../src/AuswertungPro.Next.Infrastructure/Ai/Training/TrainingCenterImportService.cs#L401-L425)) zieht Codes per Regex `[A-Z]{2,6}(?:\.[A-Z]{1,2})*` aus freiem PDF-Text — dort kann Müll wie „ABC" entstehen. Ein Validator, der Codes gegen den Katalog prüft, bevor sie zu `ProtocolEntry`/Sample werden, fängt das **früh** ab (weniger Review-Rauschen, sauberere Daten).

**Änderung:**
- Neue reine Klasse `VsaCodeValidator` (in `Domain/VsaCatalog`), Methode `IsKnownCode(string code)` → true nur wenn:
  - der Code normalisiert werden kann (`.` entfernen, trimmen, uppercase, nur A-Z),
  - die Länge mindestens 3 Zeichen ist,
  - der 3-Zeichen-Hauptcode in `VsaCodeTree.Groups` vorhanden ist.
- 2-Zeichen-Gruppen (`BA`, `BB`, ...) sind kein Trainingslabel und bleiben `false`.
- Untercodes werden in Phase 0 pragmatisch akzeptiert, sobald der Hauptcode bekannt ist. Feingranulare Char1/Char2-Validierung bleibt ein separates späteres Thema, damit echte, aber im lokalen Baum unvollständig abgebildete VSA-Varianten nicht versehentlich blockiert werden.
- `VsaCodeResolver.LookupLabel` / `NormalizeFindingCode` werden NICHT als Validator missbraucht: Beide sind für UI-/KI-Pfade bewusst fallback-tolerant und nicht als strenger PDF-Eintrittsfilter gedacht.
- Verdrahten am Parse-Eintritt (z.B. `ExtractEntriesFromChunkText`): unbekannte Codes verwerfen bzw. als unsicher markieren, statt sie als Label durchzulassen.

**Bewusst NICHT:** Den Validator in `KnowledgeBaseManager.IsIndexWorthy` einziehen. Begründung: (a) StageA prüft Katalog-Codes bereits als Backstop (`InvalidCatalogCode`); (b) `IsIndexWorthy` ist `static` mit 3 Aufrufern ohne Katalog-Zugriff → Signaturänderung zieht die Aufruferkette nach (mittleres Refactor-Risiko); (c) das KB-Gate ist laut [EvalContaminationGuard.cs:19-21](../../../src/AuswertungPro.Next.Application/Ai/Training/EvalContaminationGuard.cs#L19-L21) bewusst als separate Betriebsentscheidung dokumentiert. → Falls später gewünscht, getrennt diskutieren.

**Test:** `VsaCodeValidator` ist reine, deterministische Logik → Tests erlaubt (CLAUDE.md). Fälle: bekannter Code (BABBB) → true; bekannter Hauptcode mit Untercode (BBAA) → true; unbekannter/Müll-Code (ABC, XY, BBZ) → false; leerer/2-Zeichen-Code → false. Parse-Test: PDF-Chunk mit gemischt gültigen/Müll-Codes → nur gültige werden zu Entries.

---

### Gate D — Inhaltsbasierte Video↔PDF-Paarung beim Import *(gezielt, nur bei Mehrdeutigkeit)*

**Problem:** `ScanAsync` ([TrainingCenterImportService.cs:18-63](../../../src/AuswertungPro.Next.Infrastructure/Ai/Training/TrainingCenterImportService.cs#L18-L63)) wählt Video und PDF **unabhängig**: `PickBestVideo` nimmt das größte Video ([Zeile 134-136](../../../src/AuswertungPro.Next.Infrastructure/Ai/Training/TrainingCenterImportService.cs#L134-L136)), `PickBestProtocol` das beste PDF — ohne Abgleich, ob beide dieselbe Haltung betreffen. Bei mehreren Videos/PDFs im selben Ordner kann das größte Video mit dem **falschen** PDF gepaart werden → falsche Ground Truth → genau das Gift, das Phase 0 verhindern soll.

**Änderung:**
- Neue `internal static`-Hilfsmethode `ResolvePair(videos, protos, caseId)` → `(string bestVideo, string bestProto)`.
- **Eindeutige 1:1-Fälle** (genau 1 Video UND ≤ 1 PDF): unverändert wie heute (`PickBestVideo`/`PickBestProtocol`).
- **Mehrdeutig** (> 1 Video ODER > 1 PDF): Haltungs-ID aus Video- und PDF-Dateinamen extrahieren und das **ID-passende** Element bevorzugen. Bei extrahierbarer, aber widersprüchlicher ID nicht kreuzpaaren:
  - Wenn genau eine Seite zum `caseId` passt, diese Seite behalten und die widersprechende Seite leeren.
  - Wenn keine Seite belastbar zum `caseId` passt, im normalen `ScanAsync` das Protokoll leeren (lieber „kein Protokoll" als falsche Ground Truth).
- **ID-Extraktion über `EvalContaminationGuard.NormalizeHaltungKey`** ([EvalContaminationGuard.cs:127](../../../src/AuswertungPro.Next.Application/Ai/Training/EvalContaminationGuard.cs#L127)) — NICHT über den in-file `NormalizeId` ([Zeile 395](../../../src/AuswertungPro.Next.Infrastructure/Ai/Training/TrainingCenterImportService.cs#L395)). Grund: `NormalizeId` strippt keine Bereichs-Präfixe und würde „H_06.24379-…" fälschlich von „protokoll_24379-…" entkoppeln. `NormalizeHaltungKey` löst das (`StripAreaPrefix`) und ist im Eval-Guard bereits produktiv. Erreichbar ohne neue Abhängigkeit (Infrastructure referenziert Application schon, `using` in Zeile 8).
- `ScanAsync` ruft `ResolvePair` statt der zwei getrennten Picker.
- `ScanProtocolOnlyAsync` ([Zeile 69-107](../../../src/AuswertungPro.Next.Infrastructure/Ai/Training/TrainingCenterImportService.cs#L69-L107)) wird nur für das **optionale Video** mitgezogen: Ein vorhandenes Protokoll darf dort nie wegen Video-Widerspruch geleert werden, weil der Zweck dieses Pfads gerade der Protokoll-Import ist. Bei Widerspruch: Protokoll behalten, Video leeren oder ID-passend wählen.

**Test:** `ResolvePair` `internal` → über InternalsVisibleTo testbar (im Plan verifizieren, dass `Infrastructure.Tests` das hat). Fälle:
1. 1 Video + 1 PDF → unverändert.
2. 2 Videos (groß/klein) + 1 ID-passendes PDF → ID-passendes Video gewinnt, **nicht** das größte (Regressionsschutz gegen Largest-Wins).
3. 1 Video + 2 PDFs → ID-passendes PDF gewinnt.
4. Widersprüchliche IDs → schwächeres Element geleert.
5. PDF ohne extrahierbare ID → Fallback auf heutige Heuristik (kein Regress).
6. `NormalizeHaltungKey` matcht „H_06.24379-41412" und „protokoll_24379-41412" als gleich.
7. `ScanProtocolOnlyAsync`: widersprechendes Video leert nur `VideoPath`, nie das vorhandene `ProtocolPath`.
Nur Dateisystem-Fixtures (Temp-Ordner, Größe via `FileInfo.Length`), kein Sidecar/GPU.

---

### Gate A — Confidence/KB-Gate für Auto-Freigabe *(zuletzt; ändert bewusst Verhalten)*

**Problem:** `SelfTrainingAutoAcceptPolicy.Decide` ([SelfTrainingAutoAcceptPolicy.cs:23-54](../../../src/AuswertungPro.Next.Application/Ai/Training/SelfTrainingAutoAcceptPolicy.cs#L23-L54)) nutzt nur `MatchLevel` + `RequireHumanReview` + KB-**Veto**. Der berechnete `ConfidenceScore` ([SelfTrainingComparisonService.cs:67-99](../../../src/AuswertungPro.Next.Infrastructure/Ai/Training/SelfTrainingComparisonService.cs#L67-L99)) wird nie als Tor gelesen, und `KbAgreement` bestätigt nichts positiv (`KbAgreement` und `KbNoSignal` fallen in denselben Auto-Gold-Zweig, [Zeile 44-48](../../../src/AuswertungPro.Next.Application/Ai/Training/SelfTrainingAutoAcceptPolicy.cs#L44-L48)). Bei abgeschaltetem `RequireHumanReview` kann also ein Sample **ohne jede KB-Bestätigung** automatisch Gold werden.

**User-Entscheidung:** Auto-Gold nur noch mit **aktiver KB-Zustimmung** (`KbAgreement`); ohne KB-Signal → Review. Als konfigurierbarer Schalter, Default streng.

**Änderung:**
- `TrainingCenterSettings`: zwei neue Felder
  - `bool RequireKbAgreementForAutoGold { get; set; } = true;` (Pascals Entscheidung, Default streng)
  - `double AutoAcceptConfidenceThreshold { get; set; } = 1.0;` (Vorsorge; greift erst, wenn ExactMatch je gelockert wird — heute ist ExactMatch immer Score 1.0)
- `Decide` um **optionale** Parameter erweitern (Defaults halten alle Aufrufer/Tests rückwärts-kompatibel):
  `Decide(level, requireHumanReview, kbCheck = KbNoSignal, bool requireKbAgreement = false, double confidenceScore = 1.0, double confidenceThreshold = 1.0, bool framePositionReliable = true)`
- Auto-Gold-Zweig (ersetzt Zeile 44-48): Approved + Pending **nur wenn** `cleanExact && confidenceScore >= confidenceThreshold && (!requireKbAgreement || kbCheck == KbAgreement) && framePositionReliable`. Sonst `New` + `RouteToReview` mit spezifischem Reason (neue Konstanten, z.B. `KbAgreementRequiredReason`, `ConfidenceInsufficientReason`, `FramePositionUnverifiedReason`; den erstfehlenden melden).
- `framePositionReliable` gehört zu **Gate B** (siehe unten) — wird hier im selben Zweig mitgeprüft.
- Orchestrator ([SelfTrainingOrchestrator.cs:315-317](../../../src/AuswertungPro.Next.Infrastructure/Ai/Training/SelfTrainingOrchestrator.cs#L315-L317)) reicht `_settings.RequireKbAgreementForAutoGold`, `comparison.ConfidenceScore`, `_settings.AutoAcceptConfidenceThreshold` durch.

**Bewusste Verhaltensänderung:** Der bestehende grüne Test `NonDisagreement_WithFlagOff_CleanExact_StillApproves` ändert sich für den `KbNoSignal`-Fall (künftig Review statt Auto-Gold), **wenn** `requireKbAgreement=true`. Das ist Pascals Entscheidung. Nebeneffekt: bei heute kleiner/leerer KB landet anfangs fast alles in Review, bis die KB Substanz hat — gewollt.

**Test:** Datei `SelfTrainingAutoAcceptPolicyTests.cs` (aktuell **9 Testfälle in 6 Methoden**, grün verifiziert). Ergänzen:
1. cleanExact + Flag aus + KbAgreement + Score≥Threshold + frameReliable → Approved + Pending.
2. cleanExact + Flag aus + KbNoSignal + requireKbAgreement=true → New + Review (dokumentierte Änderung des Alt-Tests, mit Begründung).
3. cleanExact + KbAgreement + Score < Threshold → Review.
4. KbDisagreement bleibt Veto (unverändert).
5. `Decide(ExactMatch, false)` ohne neue Argumente → weiterhin Approved (Rückwärts-Kompatibilität der Defaults).
Zusätzlich `tools/SelfTrainingHarness` laufen lassen: `approved==0 && indexed==0` unter `RequireHumanReview=true` muss weiter gelten.

---

### Gate B — Auto-Gold nur für verlässlich positionierte Frames *(Review-Queue für unsichere Frames)*

**User-Entscheidung:** Video-Bilder mit unsicherer Meter-/Positionszuordnung → Review-Queue (Mensch entscheidet), kein stiller Hard-Drop.

**Warum am Auto-Freigabe-Gate statt im StageA-Export:** Im strengen Modus (`RequireHumanReview=true`) ist alles, was StageA erreicht, von Hand freigegeben — ein StageA-Block würde den Menschen überstimmen. Im Batch-Modus (`RequireHumanReview=false`) entsteht Auto-Gold an **einer** Stelle: `Decide`. Genau dort gehört der Riegel hin.

**Der verlässliche Unterschied ist die Frame-Herkunft, nicht der Meter-Vergleich.** `OsdDelta` ist deckungsgleich mit der ExactMatch-Meter-Achse (beides `|KI-Meter − Protokoll|`) und daher redundant. Aussagekräftig ist `SourceType` ([SelfTrainingOrchestrator.cs:341-343](../../../src/AuswertungPro.Next.Infrastructure/Ai/Training/SelfTrainingOrchestrator.cs#L341-L343)):
- `PdfPhoto` → Frame ist das Protokoll-Foto selbst → **korrekt verankert, sicher**.
- `VideoTimestamp` → Protokoll-Eintrag hatte einen Zeitstempel (`entry.Zeit`) → am exakten Videozeitpunkt → **ausreichend verankert**.
- `VideoLinear` → kein Zeitstempel, Frame per linearer Meter→Zeit-Schätzung gewählt → **unzuverlässig verankert, riskant**.

**Änderung:**
- `framePositionReliable`-Parameter in `Decide` (siehe Gate A); Auto-Gold nur wenn `true`.
- Orchestrator setzt `framePositionReliable = !usedVideoFallback || entry.Zeit.HasValue` (entspricht `SourceType != VideoLinear`). Konservativ: `VideoLinear` → immer Review.
- Optionaler Schalter `bool RequireReliableFramePositionForAutoGold { get; set; } = true;` in `TrainingCenterSettings`, falls Pascal den Riegel später lockern will.
- **Sichtbarkeit (optional, nicht zwingend Phase 0):** ein Zähler im StageA-Manifest, der `VideoLinear`-Samples ausweist, rein informativ — ohne sie zu blockieren (kein Override).

**Test:** In `SelfTrainingAutoAcceptPolicyTests.cs`: cleanExact + Flag aus + KbAgreement + `framePositionReliable=false` → New + Review (Reason FramePositionUnverified); dasselbe mit `framePositionReliable=true` → Approved. Orchestrator-Mapping (`VideoLinear→false`, `PdfPhoto/VideoTimestamp→true`) in einem schmalen Test absichern, falls eine testbare Naht existiert (sonst im Plan als Integrationsnotiz).

---

## Bewusst NICHT in Phase 0 (separat diskutieren)

- **KiCode in die globale Dedup-Signatur** aufnehmen: `BuildCanonicalSignature` hat 6 Aufrufer; eine generelle 5-teilige Signatur bräche den signatur-gekeyten Dedup für alle Pfade und erzeugte stille Duplikate. Der reale Datenverlust-Pfad (zwei Läufe desselben Truth-Eintrags mit unterschiedlichem KI-Code kollabieren) ist klein; `RequireHumanReview=true` schützt heute ohnehin. → aufgeschoben.
- **`maxJumpPerSecond=5.0`** ([OsdMeterDetectionService.cs](../../../src/AuswertungPro.Next.Infrastructure/Ai/OsdMeterDetectionService.cs)) verwirft legitime Kamerasprünge still — eigenes Tuning-Thema.
- **Validator in `IsIndexWorthy`** verdrahten — Architektur-Entscheidung, siehe Gate C-Teil2.
- **DINO-Schwellen** (`SingleFrameMultiModelService` 0.30/0.25, Memory `dino-schwellen-zu-streng`) — separates Erkennungs-Tuning, nicht Self-Training.

## Reihenfolge

1. **Gate C-Teil1** (Kommentar-Fix) — trivial, null Risiko, beseitigt sofort die Verwechslungsfalle.
2. **Gate C-Teil2** (VsaCodeValidator) — reine, getestete Logik am Parse-Eintritt.
3. **Gate D** (Import-Paarung) — verhindert falsche Ground Truth bereits beim Import.
4. **Gate A + B** (Confidence/KB- + Frame-Position-Gate, gemeinsam in `Decide`) — zuletzt, weil sie bewusst Verhalten ändern.

## Test-Strategie & CLAUDE.md-Konformität

- Tests betreffen ausschließlich deterministische Gate-/Validator-/Pairing-Logik → durch CLAUDE.md („Tests NUR für Recommendation- und QualityGate-Logik") gedeckt.
- Keine neuen NuGet-Pakete.
- Alle Code-Kommentare auf Deutsch.
- `dotnet test AuswertungPro.sln` muss grün bleiben; `SelfTrainingHarness` bestätigt die `RequireHumanReview`-Garantie.
- TDD pro Gate: erst Test (rot), dann minimale Implementierung (grün), dann Commit.

## Risiken & offene Punkte

- **Gate A** erhöht bei kleiner KB die Review-Last (gewollt, dokumentiert).
- **Gate D**: `InternalsVisibleTo` für `Infrastructure.Tests` im Plan verifizieren, sonst `ResolvePair` testbar anders schneiden.
- **Gate C-Teil1**: vor dem Kommentar-Fix `Groups["BB"]` gegen CLAUDE.md gegenprüfen (Wahrheitsquelle bestätigen).
- **Gate B** ist konservativ: `VideoLinear`-Frames kommen im Batch nie automatisch ins Gold. Falls das zu viel Ausbeute kostet, ist der Schalter `RequireReliableFramePositionForAutoGold` die Stellschraube — nicht das stille Aufweichen des Gates.

## Definition of Done

- Alle vier Gates implementiert, je mit Tests (rot→grün).
- `dotnet test AuswertungPro.sln` grün; `SelfTrainingHarness`-Garantie hält.
- Kein Default-Verhalten ohne ausdrückliche Entscheidung geändert (`RequireHumanReview` bleibt `true`).
- Commit je Gate; Branch `feature/gis-karte`, nichts gepusht/gemerged ohne Freigabe.
- Danach: Pilot 10-20 Haltungen mit voller manueller Review (Phase 1) — separater Schritt.
