# ADR-006: VSA-KEK-Manifest als alleinige Code-Wahrheit

**Status:** Accepted / in Umsetzung
**Datum:** 2026-05-26
**Aktualisiert:** 2026-05-27
**Ersetzt:** Implizite Mehrfach-Definitionen in C#-Klassen (siehe Befundtabelle)

---

## Kontext

Im Repo gibt es mindestens **sechs unabhaengige Stellen**, die VSA-Schadenscodes
und ihre Bedeutungen hartkodiert haben. Die Definitionen widersprechen
sich teilweise. Selbst `CLAUDE.md` (das angebliche Projekt-Gedaechtnis) und
die Memory-Notiz `project_vsa_2019_catalog.md` enthalten **falsche** Code-
Bedeutungen.

### Konkretes Symptom

Stichprobe `BBA`:

| Quelle | Bedeutung |
|---|---|
| **VSA-KEK-Manifest (Katalog-Wahrheit)** | **Wurzeln** |
| `CLAUDE.md` vor Korrektur | Inkrustation/Kalk (falsch) |
| `project_vsa_2019_catalog.md` Memory | implizit Inkrustation (falsch) |
| `VsaCodeResolver.cs` vor Paket 6 | inkrustation/anhaftung/sinter → `BBA` (falsch) |
| `GuidedVerificationService.cs:232` | Wurzeleinwuchs (korrekt) |
| `FewShotExampleBuilder.cs:79` | Wurzeleinwuchs (korrekt) |
| `ObservationCatalogViewModel.cs:561` | Wurzeln (korrekt) |

Das gleiche Muster fuer `BBB`, `BBC`, `BBD`, `BAB/BAC` (vertauscht in
ObservationCatalogViewModel) und Sonderfaelle `BAG`/`BAGA`/`BDB*`.

### Konsequenz heute

- KI-Pipeline lernt potenziell falsche Klassen-Zuordnungen.
- Plausibilitaets-Checks koennten korrekte Codes ablehnen oder falsche
  durchwinken.
- Reports/Symbole im PDF-Export verwenden teilweise verdrehte Bedeutungen.
- 23'326 Stage-A-trainings-ready Samples (per Memory) sind potenziell mit
  falschen Labels versehen, sofern sie aus Pfaden stammen, die ueber die
  verdrehten Quellen liefen.

### Was bisher schon richtig ist

- Commit `ccc1790e` hat WinCan VSA-2019 SEC/NOD XML-Kataloge als ersten
  Single-Source-Schritt eingefuehrt.
- Commit `8d1d56c8` hat ein generiertes VSA-KEK-Manifest und einen
  `ManifestCodeCatalogProvider` etabliert.
- Commit `bc4ebea9` schliesst Trainingsdaten vor 2022 vom Export aus
  (`TrainingSampleEligibility`).
- `VsaCodeTreeCatalogAdapter` baut den UI-Codierbaum aus dem Manifest.

**Stand 2026-05-27:** Die kritischen Konsumenten sind bereinigt oder
gegen Tests verriegelt: Training-Filter, UI-Katalog, Guided Verification,
Few-Shot-Kommentare, zentrale Text-zu-Code-Heuristik, Prompt-Rendering,
PDF-Symbolmapping und Eingabemarker. Uebrig bleiben nur bewusste
Grobklassen/Modellkompatibilitaets-Stellen wie YOLO-Klassen-IDs.

---

## Bewiesene Konfliktstellen (Inventur)

| Datei | Stellen-Typ | Befund / Status |
|---|---|---|
| [GuidedVerificationService.cs](../src/AuswertungPro.Next.Infrastructure/Ai/Training/GuidedVerificationService.cs) | Code→Label | erledigt: Label kommen ueber `ICodeCatalogProvider`; kein eigener Switch mehr. |
| [FewShotExampleBuilder.cs](../src/AuswertungPro.Next.Infrastructure/Ai/Training/FewShotExampleBuilder.cs) | Trainings-Priority-Liste | erledigt: Liste enthaelt nur Prefixe; Bedeutungen werden nicht dort gepflegt. |
| [ObservationCatalogViewModel.cs](../src/AuswertungPro.Next.UI/ViewModels/Protocol/ObservationCatalogViewModel.cs) | UI-Codebaum | erledigt: Unterkategorie-Labels kommen aus dem Katalog. |
| [ProtocolPdfExporter.cs](../src/AuswertungPro.Next.Application/Reports/ProtocolPdfExporter.cs) | Symbol-Klassifizierung | Paket 9 korrigiert: `BBA -> roots`, `BBB -> incrustation`, `BAJ -> default`. Symbolmapping bleibt bewusst Darstellungslogik. |
| [EnhancedVisionAnalysisService.cs](../src/AuswertungPro.Next.Infrastructure/Ai/EnhancedVisionAnalysisService.cs) | Vision-Prompt | erledigt: Prompt wird aus dem Katalog gerendert, Fallback ist manifestkonform. |
| [VsaCodeResolver.cs](../src/AuswertungPro.Next.Infrastructure/Ai/VsaCodeResolver.cs), [LiveDetectionMapper.cs](../src/AuswertungPro.Next.Infrastructure/Ai/LiveDetectionMapper.cs), [VideoFullAnalysisService.cs](../src/AuswertungPro.Next.Infrastructure/Ai/VideoFullAnalysisService.cs), [MultiModelAnalysisService.cs](../src/AuswertungPro.Next.Infrastructure/Ai/Pipeline/MultiModelAnalysisService.cs) | Text→Code-Heuristik | erledigt: zentrale Heuristik ueber `VsaCodeResolver`; Konsumenten duplizieren die Regeln nicht mehr. |

---

## Entscheidung

1. **VSA-KEK-Manifest ist alleinige Code-Wahrheit.**
   Datei: `src/AuswertungPro.Next.UI/Data/vsa_kek_2020_catalog_manifest.json`
   (kuenftig umzubenennen — separater Schritt). Generiert aus dem
   WinCan-VSA-2019-XML-Katalog via `VsaKekCatalogBuilder`.
   Zugriffspunkt: `ICodeCatalogProvider` aus DI.

2. **Keine automatische Umlabelung bestehender Trainings-Samples.**
   Alte Samples bleiben gespeichert. Sie werden nicht umcodiert, weil
   das ohne manuelle Kontrolle Daten korrupt machen kann. Die Stichtags-
   Regel aus ADR-005 (`bc4ebea9`) und die zusaetzliche Katalog-Validitaets-
   Regel (siehe Punkt 4) sperren sie effektiv vom Training aus.

3. **Codierbaum (UI), KI-Prompts, PDF-Symbole und Resolver greifen ueber
   zentrale Services zu.** Stand 2026-05-27:
   - `ObservationCatalogViewModel.subLabels` ist entfernt.
   - `EnhancedVisionAnalysisService` rendert den Prompt aus dem Katalog.
   - `VsaCodeResolver` ist die zentrale Free-Text-Heuristik.
   - `LiveDetectionMapper`, `VideoFullAnalysisService` und
     `MultiModelAnalysisService` nutzen die zentrale Heuristik.
   - `GuidedVerificationService` liest Labels aus dem Katalog.
   - `FewShotExampleBuilder` pflegt keine Bedeutungs-Kommentare mehr.
   - `ProtocolPdfExporter` nutzt manifestkonforme Symbol-Prefixe; das
     Symbolmapping bleibt bewusst Darstellungslogik, nicht Code-Wahrheit.

4. **Training-Haerter:**
   `StageAExporter` und `YoloDatasetExportService` validieren jeden
   `sample.Code` gegen den aktiven Katalog. Trifft der Code nicht:
   - **`SkippedInvalidCatalogCode`** (neue Result-Zaehl-Spalte).
   - **`TrainingEligibilityReason = "code-not-in-catalog"`** im Sample.

   Wirkt zusaetzlich zur Stichtags-Regel.

5. **Tests verriegeln die Wahrheit.**
   Pro Konflikt-Code mindestens ein Test, der den Katalog-Eintrag und
   die UI/Prompt-/Resolver-Ausgabe gegenprueft. Schwerpunkt:
   `BBA`, `BBB`, `BBC`, `BBD`, `BAB`, `BAC`, `BAH`, `BAG`, `BAGA`, `BDB*`.

---

## Konsequenzen

### Positiv

- Eine Wahrheit, mehrere lesende Konsumenten. Kataloge des
  VSA-KEK-Updates schlagen automatisch durch.
- Falsche KI-Prompts werden mit dem Katalog-Update korrigiert.
- Reports und UI zeigen konsistente Bedeutungen.
- Stichtagsregel + Katalog-Validitaet zusammen schliessen den Trainings-
  Daten-Pfad gegen alte Fehl-Labels ab.

### Negativ / Aufwand

- Vier bis sechs Refactor-Pakete in Reihenfolge umzusetzen (siehe unten).
- KI-Prompt-Aenderung erfordert Pipeline-Smoke-Test (Qwen liefert
  moeglicherweise andere Codes als vorher, weil der Prompt explizit
  andere Bedeutungen suggeriert hat).
- `ProtocolPdfExporter`-Aenderung kann visuelle Symbol-Aenderungen in
  bereits generierten Reports erzeugen, falls jemand neu generiert.
- 23'326 Stage-A-Samples bleiben mit moeglicherweise alten Labels im KB
  — bewusst nicht angefasst (siehe Entscheidung 2).

### Was NICHT passiert (Nicht-Ziele)

- Keine automatische Code-Konversion `BBA → BBB` o. ae. fuer existierende
  Samples.
- Keine Loeschung alter Trainings-Samples.
- Keine Aenderung am VSA-KEK-Manifest selbst (das ist Single Source —
  Aenderungen daran sind eigene Entscheidung).
- Kein UI-Kosmetik-Refactor (Theme, Codierdialog-Hardening).
- Kein Performance-Refactor (TensorRT, FrameCapture) bevor Code-Wahrheit
  steht.

---

## Migrations-Reihenfolge

Streng inkrementell, jedes Paket einzeln committed und testbar.

| # | Paket | Risiko | Aufwand | Reihenfolge-Begruendung |
|---|---|---|---|---|
| 1 | **Stichtags-Regel um Katalog-Validitaet erweitern** (Entscheidung 4) | niedrig | 1h | Erste Verteidigungslinie — verhindert dass weitere falsche Samples ins Training kommen, waehrend wir refaktorieren. |
| 2 | **`ObservationCatalogViewModel.subLabels` entfernen** | niedrig | 1h | UI-Code, kein Pipeline-Risiko, klare Nachweisbarkeit (Label-Aenderungen sichtbar). |
| 3 | **`GuidedVerificationService` switch ersetzen** | niedrig | 30min | Reine Lookup-Aenderung, hat Tests dahinter. |
| 4 | **`FewShotExampleBuilder` Kommentare aus Katalog ziehen** | niedrig | 1h | Beeinflusst Trainings-Beispiele — nach Tests pruefen. |
| 5 | **`ProtocolPdfExporter` Symbol-Klassifizierung an Katalog binden** | mittel | 2h | Visueller Impact, manueller Smoke-Test eines PDF-Exports noetig. |
| 6 | **`VsaCodeResolver` Text→Code + Katalog-Validierung** | mittel | 2h | Aenderung kann Free-Text-Klassifikation veraendern — Pipeline-Smoke-Test noetig. |
| 7 | **Vier Heuristik-Konsumenten** (`LiveDetectionMapper`, `VideoFullAnalysisService`, `MultiModelAnalysisService`) konsolidieren | mittel | 2-3h | Vorher Tests fuer Verhalten dokumentieren. |
| 8 | **`EnhancedVisionAnalysisService` Prompt dynamisch aus Katalog** | hoch | 2h | KI-Prompt-Aenderung beeinflusst Qwen-Output direkt. Mit Live-Testlauf vergleichen. |
| 9 | **CLAUDE.md korrigieren** | keiner | 15min | Erledigt am 2026-05-27 mit `BBA=Wurzeln/Bewuchs`, `BBB=Anhaftende Stoffe/Inkrustation`. |
| 10 | **Memory `project_vsa_2019_catalog.md` korrigieren** | keiner | 15min | Ausserhalb des Repos separat nachziehen/halten. |

**Gesamt-Aufwand:** ~13-15h, verteilt auf 4-6 Sitzungen.

---

## Offene Fragen

1. **Wie genau soll `BDB*` behandelt werden?** Laut User-Inventur sind das
   "TV-Untersuchungs-Anmerkungen, intern `BDB`". Sollen `BDBA/BDBD/BDBF/BDBK`
   im Trainingsdatensatz **gar nicht** vorkommen (weil reine Anmerkungen)
   oder als Klasse `BDB` zusammengefasst werden?

2. **`BCCYY` und andere Observed-Extensions:** Sollen sie im
   Trainings-Filter als `SkippedInvalidCatalogCode` zaehlen, obwohl der
   Katalog sie als `IsObservedExtension=true` kennt? Vorschlag: separate
   Zaehler `SkippedObservedExtension`, damit unterscheidbar.

3. **`EnhancedVisionAnalysisService` Prompt-Stabilitaet:** Wenn Qwen
   vorher mit "BAB=Riss" trainiert wurde und der Prompt jetzt aus dem
   Katalog "BAB=Riss" rendert — kein Problem. Aber wenn der Katalog
   irgendwann "BAB=Riss laengs" detaillierter wird, koennte Qwen weniger
   gut antworten. → braucht laufenden Eval-Set-Lauf nach Aenderung.

4. **Mehrsprachige Labels:** Katalog enthaelt deutsche Bedeutungen. Wenn
   der Prompt englisch wird (z. B. fuer ein non-German Qwen-Modell), wer
   uebersetzt? → Aktuelles Setup ist DE-only, vorerst ignorieren.

---

## Glossar — VSA-KEK-Code-Wahrheit (Stand 2026-05-26)

Basierend auf User-Inventur, abgeglichen mit `VsaCodeTreeCatalogAdapter`-
Output:

| Code | Bedeutung |
|---|---|
| `BAB` | Riss |
| `BAC` | Bruch |
| `BAF` | Deformation |
| `BAH` | Versatz |
| `BAI` | Einragung Stutzen/Anschluss |
| `BBA*` | Wurzeln |
| `BBB*` | Anhaftende Stoffe / Inkrustation / Fett |
| `BBC*` | Ablagerungen |
| `BBD*` | Eindringendes Bodenmaterial (kein Basiscode `BBD`) |
| `BAG` | Regelcode, **nicht klickbar** |
| `BAGA` | Anschluss einragend, intern `BAG` |
| `BDB*` | TV-Untersuchungs-Anmerkungen, intern `BDB` |
| `BCD` | Rohranfang |
| `BCE` | Rohrende |
| `BCA` | Seitlicher Anschluss |
| `BCC` | Bogen |
| `BCCYY` | Observed-Extension, nicht klickbar |

`CLAUDE.md` ist mit diesem Glossar abgeglichen. Externe Agenten-Memorys
muessen weiterhin gegen das Manifest geprueft werden, weil sie ausserhalb
des Repos liegen.

---

## Bezug zu anderen ADRs

- **ADR-005 (implizit, Commit `bc4ebea9`):** Stichtags-Regel
  `TrainingSampleEligibility` — diese ADR erweitert die Sperrlogik um
  Katalog-Validitaet.
- **Audit-Bericht `AUDIT_2026-05-25.md` Fund F-D-20:** Diese ADR ist die
  konkrete Antwort darauf. F-D-20 ist mit Umsetzung dieser ADR
  geschlossen.

---

## Naechster konkreter Schritt nach Approval dieser ADR

Codex-Auftrag fuer **Paket 1 (Katalog-Validitaet als Trainings-Filter):**

> In `src/AuswertungPro.Next.Application/Ai/Training/TrainingSampleModels.cs` einen
> zusaetzlichen Check ergaenzen: `Evaluate(sample, ICodeCatalogProvider catalog)`
> als neuer Overload, der pruefte: Datum >= 2022 UND
> `catalog.TryGet(sample.Code, out var def) && def.IsSelectable`.
> `StageAExporter` und `YoloDatasetExportService` nutzen diesen Overload
> via DI. Neuer Reason: `"code-not-in-catalog"`. Neuer Result-Zaehler:
> `SkippedInvalidCatalogCode`. Tests fuer 3 Faelle: gueltiger Code,
> ungueltiger Code, Observed-Extension. Solution-Build und alle 408 Tests
> muessen gruen bleiben.

Kein anderer Konfliktcode wird in diesem Paket angefasst.
