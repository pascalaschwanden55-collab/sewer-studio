# D3: QualityGate ehrlich machen (statisch + pfadkalibriert, halber Lernkreis offengelegt) — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans (oder subagent-driven-development) zur Umsetzung. Steps mit Checkbox (`- [ ]`).

**Goal:** Den tatsächlichen Zustand des QualityGate ehrlich festhalten — statische Gewichte, pfadabhängige Kalibrierung, halb-offener Lernkreis — damit niemand (Mensch oder KI-Analyse) wieder fälschlich „adaptives, selbstlernendes Gate" annimmt.

**Architecture:** Reine **Dokumentation + Code-Kommentare**. KEINE Verhaltensänderung, KEIN neuer/entfernter Pfad, KEINE geänderten Gewichte/Schwellen. Entspricht dem D8-Prinzip (entphantomisieren statt löschen).

**Tech Stack:** Markdown (ADR), C#-Kommentare. Kein Test nötig (kein Verhalten geändert); nur Build-Check.

---

## Verifizierte Faktenlage (Stand HEAD b021b0ce)

- **Lernen läuft (eine Hälfte):** Bestätigen/Verwerfen im Codiermodus → `CodingSessionViewModel.cs:569` `RecordDecisionAsync` → `CodingFeedbackRecorder` (verdrahtet an 3 Stellen: `CodingModeWindow.xaml.cs:86`, `PlayerWindow.Coding.cs:143`, `PlayerWindow.LiveDetection.cs:1054`) → `FeedbackIngestionService.ProcessFeedbackAsync` schreibt ValidationLog, alle 25 → `WeightLearningService.ReLearnAsync` schreibt gelernte Gewichte in die SQLite-Tabelle `CategoryWeights`.
- **Anwenden fehlt (andere Hälfte):** `WeightLearningService.LoadWeights` (`:51`) hat **0 Leser**, `QualityGateService.SetWeights` hat **0 Aufrufer**. Beide Gate-Instanzen sind `new QualityGateService()` ohne Gewichte → immer `CategoryWeights.Default()`:
  - `PlayerWindow.Coding.cs:2446` (Live-Codierung)
  - `FullProtocolGenerationService.cs:53` (Offline-Protokoll)
- **Pfad-Asymmetrie:** verschiedene Pfade füllen verschiedene Signale (Qwen-Live: QwenVisionConf+Plausibilität; Multi-Model-Live nach D2: DinoConf+SamMaskStability+Plausibilität) → das renormalisierende Gate rechnet über verschiedene Grundlagen → dieselbe Realität kann je nach Pfad eine andere Ampel ergeben. Teils **intrinsisch** (Qwen-only-Pfad hat kein DINO/SAM).
- **Reframe seit D1:** Die Ampel gated im Live-Pfad **keinen Protokolleintrag** mehr (alles `Ignored` bis Bestätigung). Sie steuert nur noch (a) ob ein Befund zur Bestätigung **pausiert** und (b) die angezeigte/gespeicherte **Confidence-Zahl**.

---

## File Structure

| Datei | Verantwortung | Änderung |
|---|---|---|
| `docs/ADR-008-qualitygate-statisch-und-pfadkalibriert.md` | Entscheidung dokumentieren | **Create** |
| `src/AuswertungPro.Next.Infrastructure/Ai/QualityGate/WeightLearningService.cs` | Klar kennzeichnen: lernt+speichert, wird aktuell NICHT geladen | **Modify** (Kommentar) |
| `src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.Coding.cs:2446` | Kennzeichnen: bewusst Default-Gewichte | **Modify** (Kommentar) |
| `src/AuswertungPro.Next.Infrastructure/Ai/FullProtocolGenerationService.cs:53` | Kennzeichnen: bewusst Default-Gewichte | **Modify** (Kommentar) |

> Ausdrücklich NICHT: gelernte Gewichte laden (= Option 1, eigenes eval-getriebenes Projekt). Nie-gefüllte Signale aus `CategoryWeights.Default()` entfernen wäre wirkungslos (Gate renormalisiert ohnehin nur über vorhandene Signale) → weggelassen.

---

## Task 1: ADR-008 schreiben

**Files:**
- Create: `docs/ADR-008-qualitygate-statisch-und-pfadkalibriert.md`

- [ ] **Step 1: ADR mit diesem Inhalt anlegen**

```markdown
# ADR-008: QualityGate ist statisch + pfadkalibriert; Lernkreis bewusst halb-offen

**Status:** Akzeptiert · **Datum:** 2026-05-31 · **Kontext:** D3 aus Pipeline-Root-Cause-Audit (docs/audits/2026-05-31-pipeline-rootcause-workflow.md)

## Kontext
Das QualityGate fusioniert bis zu 8 Signale (YOLO/DINO/SAM/Qwen/LLM/KB/KB-Agreement/Plausibilität)
gewichtet zu einem Composite (Green >= 0.75, Yellow >= 0.45). Befund aus dem Audit:
- Die Gewichte sind **statisch** (`CategoryWeights.Default()`); beide Gate-Instanzen werden ohne
  Gewichte erzeugt (`PlayerWindow.Coding.cs:2446`, `FullProtocolGenerationService.cs:53`).
- Es existiert eine **Lern-Hälfte**, die läuft: Bestätigen/Verwerfen im Codiermodus schreibt
  ValidationLog und lernt alle 25 Entscheidungen Gewichte (`WeightLearningService.ReLearnAsync`).
- Die **Anwende-Hälfte fehlt**: `LoadWeights`/`SetWeights` haben keine Aufrufer — gelernte Gewichte
  werden nie geladen. Der Lernkreis ist also nur zur Hälfte geschlossen.
- Verschiedene Analyse-Pfade füllen verschiedene Signale → dieselbe Realität kann je nach Pfad eine
  andere Ampel ergeben (teils intrinsisch, da der Qwen-only-Pfad kein DINO/SAM hat).
- Seit der Auto-Accept-Abschaltung (Commit 8fa4ca58) gated die Ampel **keinen Protokolleintrag** mehr;
  sie steuert nur die Bestätigungs-Pause und die angezeigte Confidence.

## Entscheidung
1. Das Gate bleibt **bewusst statisch** mit `CategoryWeights.Default()`.
2. Die pfadabhängige Kalibrierung wird als **beabsichtigt** akzeptiert und dokumentiert (verschiedene
   Modell-Stacks → verschiedene Signal-Sets → nicht 1:1 vergleichbare Composites).
3. Die Lern-/Selbstverbesserungs-Maschinerie (WeightLearningService / FeedbackIngestionService) gilt
   als **experimentell und absichtlich nicht ins Gate verdrahtet**. Sie wird im Code als solche
   gekennzeichnet, damit keine Analyse wieder „adaptives Gate" annimmt.

## Begründung
Ein echtes adaptives Gate (gelernte Gewichte laden) ist nur sinnvoll mit einer Messung, die belegt,
dass gelernte Gewichte die Übereinstimmung mit der Ground-Truth **erhöhen** — sonst optimiert man
blind. Das ist ein eigenes, eval-getriebenes Projekt (passt zur Eval-Set-Warden-Disziplin) und kein
Quick-Fix. Nach der Auto-Accept-Abschaltung ist die Dringlichkeit ohnehin niedrig.

## Konsequenzen
- Verlässlich, vorhersehbar, kein blindes Auto-Tuning.
- Gelernte Gewichte sammeln sich in der DB an, ohne Wirkung (akzeptiert; Grundlage für ein späteres,
  gemessenes „Gate adaptiv machen").
- Wenn der Lernkreis später geschlossen wird: separater ADR + Eval-Validierung erforderlich.
```

- [ ] **Step 2: Commit**

```bash
git add docs/ADR-008-qualitygate-statisch-und-pfadkalibriert.md
git commit -m "docs(adr): ADR-008 QualityGate statisch + pfadkalibriert, Lernkreis halb-offen (D3)"
```

---

## Task 2: Code-Kommentare (Ehrlichkeit am Ort)

**Files:**
- Modify: `src/AuswertungPro.Next.Infrastructure/Ai/QualityGate/WeightLearningService.cs` (Klassen-Header über `:18`)
- Modify: `src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.Coding.cs:2446`
- Modify: `src/AuswertungPro.Next.Infrastructure/Ai/FullProtocolGenerationService.cs:53`

- [ ] **Step 1: WeightLearningService-Header kennzeichnen**

Über der Klassendeklaration (`public sealed class WeightLearningService`) einfügen:
```csharp
// EXPERIMENTELL / HALB-OFFENER LERNKREIS (siehe ADR-008): Diese Klasse lernt und speichert
// Gewichte (via FeedbackIngestionService, ausgeloest beim Bestaetigen/Verwerfen im Codiermodus),
// ABER kein QualityGate laedt sie aktuell (LoadWeights/SetWeights ohne Aufrufer). Das Gate laeuft
// bewusst mit CategoryWeights.Default(). Nicht als "aktives adaptives Gate" annehmen.
```

- [ ] **Step 2: Beide Gate-Instanzen kennzeichnen**

`PlayerWindow.Coding.cs:2446` — über `_codingQualityGate = new QualityGateService();`:
```csharp
// Bewusst Default-Gewichte (statisch). Gelernte Gewichte werden NICHT geladen (siehe ADR-008).
```
`FullProtocolGenerationService.cs:53` — an `_qualityGate = qualityGate ?? new QualityGateService();`:
```csharp
// Fallback laeuft bewusst mit statischen Default-Gewichten (siehe ADR-008).
```

- [ ] **Step 3: Build (nur Kommentare — muss fehlerfrei bleiben)**

Run: `dotnet build AuswertungPro.sln -c Debug -v minimal`
Expected: 0 Fehler, 0 Warnungen.

- [ ] **Step 4: Commit**

```bash
git add src/AuswertungPro.Next.Infrastructure/Ai/QualityGate/WeightLearningService.cs \
        src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.Coding.cs \
        src/AuswertungPro.Next.Infrastructure/Ai/FullProtocolGenerationService.cs
git commit -m "docs(ai): QualityGate-Lernkreis als halb-offen/experimentell gekennzeichnet (D3, ADR-008)"
```

---

## Self-Review
1. **Spec-Abdeckung:** D3-Entscheidung „Option 3 (ehrlich-statisch)" → ADR (Task 1) + Kennzeichnung (Task 2). ✓
2. **Kein Verhalten geändert:** nur Markdown + Kommentare; keine Logik, keine Gewichte, keine Schwellen. ✓ (deshalb kein Test nötig, nur Build-Check)
3. **Platzhalter-Scan:** ADR-Text + Kommentar-Text vollständig ausformuliert; exakte Datei:Zeilen. ✓
4. **Konsistenz:** ADR-Nummer 008 folgt auf vorhandene ADR-006/007. ✓

## Was D3 ausdrücklich NICHT tut
- Gelernte Gewichte laden (= „adaptives Gate", Option 1) — eigenes eval-getriebenes Projekt, separater ADR.
- Pfad-Evidenz vereinheitlichen (Option 2) — teils intrinsisch unmöglich (Qwen-only ohne DINO/SAM).
- Gewichte/Schwellen ändern — keine Kalibrierungsänderung in D3.
