# ADR-008: QualityGate ist statisch + pfadkalibriert; Lernkreis bewusst halb-offen

**Status:** Akzeptiert · **Datum:** 2026-05-31 · **Kontext:** D3 aus Pipeline-Root-Cause-Audit (`docs/audits/2026-05-31-pipeline-rootcause-workflow.md`)

## Kontext
Das QualityGate fusioniert bis zu 8 Signale (YOLO/DINO/SAM/Qwen/LLM/KB/KB-Agreement/Plausibilität) gewichtet zu einem Composite (Green ≥ 0.75, Yellow ≥ 0.45). Befund aus dem Audit:

- Die Gewichte sind **statisch** (`CategoryWeights.Default()`); beide Gate-Instanzen werden ohne Gewichte erzeugt (`PlayerWindow.Coding.cs:2446`, `FullProtocolGenerationService.cs:53`).
- Es existiert eine **Lern-Hälfte, die läuft**: Bestätigen/Verwerfen im Codiermodus (`CodingSessionViewModel.cs:569` → `CodingFeedbackRecorder.RecordDecisionAsync`) schreibt ValidationLog und lernt alle 25 Entscheidungen Gewichte (`WeightLearningService.ReLearnAsync`).
- Die **Anwende-Hälfte fehlt**: `WeightLearningService.LoadWeights` und `QualityGateService.SetWeights` haben keine Aufrufer — gelernte Gewichte werden nie geladen. Der Lernkreis ist also nur zur Hälfte geschlossen.
- Verschiedene Analyse-Pfade füllen verschiedene Signale → dieselbe Realität kann je nach Pfad eine andere Ampel ergeben. Teils **intrinsisch**, da der Qwen-only-Pfad kein DINO/SAM hat.
- Seit der Auto-Accept-Abschaltung (Commit `8fa4ca58`) gated die Ampel im Live-Pfad **keinen Protokolleintrag** mehr; sie steuert nur die Bestätigungs-Pause und die angezeigte/gespeicherte Confidence.

## Entscheidung
1. Das Gate bleibt **bewusst statisch** mit `CategoryWeights.Default()`.
2. Die pfadabhängige Kalibrierung wird als **beabsichtigt** akzeptiert und dokumentiert (verschiedene Modell-Stacks → verschiedene Signal-Sets → nicht 1:1 vergleichbare Composites).
3. Die Lern-/Selbstverbesserungs-Maschinerie (`WeightLearningService` / `FeedbackIngestionService`) gilt als **experimentell und absichtlich nicht ins Gate verdrahtet**. Sie wird im Code als solche gekennzeichnet, damit keine Analyse wieder ein „adaptives Gate" annimmt.

## Begründung
Ein echtes adaptives Gate (gelernte Gewichte laden) ist nur sinnvoll mit einer Messung, die belegt, dass gelernte Gewichte die Übereinstimmung mit der Ground-Truth **erhöhen** — sonst optimiert man blind. Das ist ein eigenes, eval-getriebenes Projekt (passt zur Eval-Set-Warden-Disziplin) und kein Quick-Fix. Nach der Auto-Accept-Abschaltung ist die Dringlichkeit ohnehin niedrig.

## Konsequenzen
- Verlässlich, vorhersehbar, kein blindes Auto-Tuning.
- Gelernte Gewichte sammeln sich in der DB an, ohne Wirkung (akzeptiert; Grundlage für ein späteres, gemessenes „Gate adaptiv machen").
- Wenn der Lernkreis später geschlossen wird: separater ADR + Eval-Validierung erforderlich.

## Nicht Teil dieser Entscheidung
- Gelernte Gewichte laden (= adaptives Gate, Option 1) — eigenes eval-getriebenes Projekt.
- Pfad-Evidenz vereinheitlichen (Option 2) — teils intrinsisch unmöglich (Qwen-only ohne DINO/SAM).
- Gewichte/Schwellen ändern — keine Kalibrierungsänderung.
