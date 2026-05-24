# KI-Diagnose 2026-05-24

> Stand: 2026-05-24 (Samstagvormittag)
> Anlass: Frage des Anwenders "training aus? qualitaet der kb pipeline"
> Ergebnis: KI-Metrik-System ist aktuell **nicht funktional** (siehe Befund D unten).

## Executive Summary

Die KI-Pipeline-Infrastruktur (Sidecar, Ollama, KnowledgeBase) ist **lauffaehig und
funktional**. Die KB enthaelt 21'856 indexierte Samples (100% Embeddings, konsistentes
Modell). Aber die Metrik-Schicht, die ueberpruefen soll wie gut die KI tatsaechlich ist,
ist **nicht aktiv gepflegt**: das Feedback-Logging hat keinen Aufrufer mehr, der letzte
Eval-Set-Lauf ist 20 Tage alt und war ein Modell-Failure-Lauf, und der bisher
kommunizierte 52%-Wert ist ein Snapshot eines abgeklemmten Code-Pfades.

**Konsequenz:** Aussagen ueber die KI-Genauigkeit sind aktuell nicht belastbar.
Bevor wieder Inhalt-Arbeit (Active Learning, Training, externe Tests) priorisiert
wird, muss das Metrik-Geruest repariert werden.

## Methodik

Geprueft an diesem Datum:

| Prueffeld | Werkzeug |
|---|---|
| Sidecar-Health (Port 8100) | `/health`-Endpoint |
| Ollama-Modelle und VRAM | `api/tags`, `api/ps`, `nvidia-smi` |
| KB-Inhalte und Verteilung | SQLite-Direktabfrage `KnowledgeBase.db` |
| Eval-Set-Integritaet | `_manifest.json` + letzter CSV-Lauf |
| ValidationLog-Verlauf | SQLite-Direktabfrage |
| Aufrufpfade im Code | `grep` ueber `src/` |

## Befunde

### A — Infrastruktur laeuft (gruen)

| Komponente | Status |
|---|---|
| Sidecar Port 8100 | LAEUFT, YOLO+SAM auf CUDA (~1 GB VRAM) |
| Ollama | LAEUFT, `qwen3-vl:8b-q8` im VRAM (11.7 GB), Keep-Alive bis 22:19 |
| GPU/VRAM | RTX 5090, 14.8 GB / 32 GB belegt (46%), 4% Auslastung (idle) |
| KnowledgeBase.db | 187 MB, lesbar, 21'856 Samples |

`active_inference=0`, keine laufenden Jobs. **Training ist aus, Infrastruktur ist warm.**

### B — KB-Inhalt unbalanciert (gelb)

| Hauptgruppe | Samples | Anteil |
|---|---|---|
| BC (Bauinformationen) | 9'695 | 44% |
| BA (Bauliche Schaeden) | 5'323 | 24% |
| BD (Anschluesse) | 3'469 | 16% |
| **BB (Betriebliche Stoerungen)** | **1'759** | **8%** |
| AE (Allgemein/Erfassung) | 1'610 | 7% |

**Top-3-Codes machen 28% aller Samples aus**: BCE (Rohrende, 2'660), BCD (Rohranfang,
2'241), BDA (1'329). Davon sind BCE+BCD = 4'901 Standard-Marker, die jede Inspektion
automatisch erzeugt — also kein echter Schadenscode-Lernstoff.

**BB ist untervertreten** — das ist genau die Gruppe mit Wurzeln, Ablagerungen,
Inkrustationen, die fuer Sanierungsentscheidungen wichtig waere.

### C — KB-Indexierung sauber (gruen)

| Metrik | Wert |
|---|---|
| Samples mit Embedding | 21'856 / 21'856 (100%) |
| Embedding-Modell | `nomic-embed-text`, konsistent ueber alle |
| Versions-Eintraege | 359 |

Allerdings: 3 leere Versions am 2026-05-20 12:14:04-06 (`samples=0`, keine Notes,
in 2 Sekunden Folge erstellt) sehen nach Bug aus. Aufrufpfad konnte im aktuellen
Code nicht identifiziert werden.

### D — Metrik-Schicht nicht funktional (rot)

Das ist der kritische Befund:

**D1. ValidationLog ist tot.**
- 355 historische Eintraege, 210 als `WasCorrect=1` markiert → Snapshot-Accuracy = 59%
- Letzter Eintrag am 2026-05-20, neueste 32 Eintraege haben alle `EvidenceJson="{}"`
- Schreibstelle: `ValidationLogger.Log()` in
  `src/AuswertungPro.Next.UI/Ai/QualityGate/ValidationLogger.cs`
- Aufrufer: `FeedbackIngestionService.ProcessFeedbackAsync()` in
  `src/AuswertungPro.Next.UI/Ai/SelfImproving/FeedbackIngestionService.cs`
- **`ProcessFeedbackAsync` wird nirgends im aktuellen Code aufgerufen** — UI-Anbindung
  fehlt oder wurde entfernt

Damit ist der gesamte Self-Improvement-Pfad (Accept/Reject → ValidationLog →
Weight-Learning) abgeschnitten.

**D2. Letzter Eval-Set-Lauf ist Modell-Failure.**
- Datei: `c:/KI_BRAIN/eval_set/metrics_qwen3_vl_8b_q8.csv`
- Datum: 2026-05-04 (vor 20 Tagen)
- 120 Frames getestet
- Exact-Code-Match: **0%** (0/120)
- Main-Group-Match: 0% (0/120)
- Sub-Group-Match: 6.7% (8/120)
- Null-Response: **65.8%** (79/120) — Modell antwortet leer
- `pred=BCD` Fallback: 34% (41/120) — Modell sagt "Rohranfang" fuer alles wo es nicht
  null antwortet

Das ist nicht der echte aktuelle Modell-Stand. Vermutlich war zu dieser Zeit der
Sidecar oder Prompt kaputt konfiguriert. Aber es ist der einzige Eval-Set-Lauf
den wir haben.

**D3. Keine systematische Benchmark-Versionsfuehrung.**
- `c:/KI_BRAIN/benchmark_metrics.json` (Zeitreihen-Pfad laut Skill): **fehlt**
- `c:/KI_BRAIN/baselines/` (Baseline-Archiv): **fehlt**
- `eval_set/_manifest.json`: `frozen=true`, aber **kein Hash-Block** (`hashes_count=0`)

Es gibt also keine Moeglichkeit, ueber Zeit zu verfolgen ob die KI besser oder
schlechter wird, und auch keine Garantie dass das Eval-Set nicht verfaelscht wurde
(Hash-Pruefung nicht moeglich).

**D4. 52%-Wert darf nicht als aktuelle Messung kommuniziert werden.**
- Der aktuelle `README.md`-Stand dieses Branches enthielt den alten 52%-Satz nicht
  mehr. Falls der Wert in README, Uebergabeunterlagen oder externen Notizen
  verwendet wird, muss er als historischer Snapshot gekennzeichnet werden.
- Realitaet: ValidationLog wird nicht mehr aktiv geschrieben (D1). Der Wert ist
  ein historischer Snapshot, keine aktuelle Messung.
- Externe Tester wuerden bei einem unmarkierten 52%-Wert einen falschen Eindruck
  bekommen.

## Konsolidierte Bewertung

| Aspekt | Status |
|---|---|
| KI-Pipeline laeuft technisch | gruen |
| KI-Pipeline lernt aus User-Feedback | **rot** — Feedback-Pfad tot |
| KI-Pipeline-Qualitaet ist messbar | **rot** — kein aktueller Benchmark |
| KI-Pipeline-Qualitaet ist trackbar | **rot** — keine Zeitreihe, keine Baselines |
| Eval-Set ist sicher (Hash-Schutz) | gelb — Hash-Block fehlt |
| KB-Daten sind sauber indexiert | gruen |
| KB-Inhalt-Balance | gelb — BB schwach |

## Aktionsplan

Vorgeschlagene Reihenfolge fuer die naechsten Sessions:

### Ticket 1 — README-Hinweis "Messung steht aus" (klein, ~10 Min)

`README.md` um einen klaren KI-Messstands-Hinweis ergaenzen. Keine aktuelle
Accuracy behaupten. Wenn der alte Wert genannt wird, nur so:

> "Letzter dokumentierter ValidationLog-Wert: ca. 52 % (Stand Mai 2026, historischer Snapshot).
> Aktuelle Messung steht aus — Metrik-Pflegekette wird in Q2 2026 reaktiviert."

### Ticket 2 — Eval-Set Hash-Block einfuehren (klein, ~30 Min)

`EvalSetGenerator.ComputeAndStoreHashes()`-Methode bauen (falls noch nicht vorhanden),
einmalig aufrufen, Hash-Block ins `_manifest.json` schreiben. Danach kann der
`eval-set-warden`-Skill Pre-Run und Pre-Import sauber pruefen.

### Ticket 3 — Feedback-Pfad reaktivieren (mittel, ~2-4 Std)

UI-Code suchen wo Codierende Accept/Reject-Aktionen triggern (CodingModeWindow,
ProtocolService) und dort `FeedbackIngestionService.ProcessFeedbackAsync` einhaengen.
Ohne diesen Schritt lernt die KI gar nichts aus User-Korrekturen, und der
Self-Improvement-Loop bleibt offen.

### Ticket 4 — Standalone-Benchmark-Runner als CLI-Tool (mittel, ~4-6 Std)

`tools/EvalSetBenchmark/` als Standalone-CLI bauen, das:
- Die 120 Eval-Frames durch Sidecar+Ollama jagt
- CSV (kompatibel zum heutigen `metrics_qwen3_vl_8b_q8.csv`-Format) schreibt
- JSON-Eintrag in `c:/KI_BRAIN/benchmark_metrics.json` anhaengt
- Vollstaendigen Snapshot in `c:/KI_BRAIN/baselines/yolo_v<X>_<timestamp>.json` ablegt

Damit ist der Benchmark headless reproduzierbar — kein UI-Klick mehr noetig, auch fuer
CI/Nachtlauf nutzbar.

### Ticket 5 (optional) — 3 leere Versions vom 20.05. aufraeumen (klein, ~15 Min)

`DELETE FROM Versions WHERE SampleCount = 0 AND Notes = ''` (nach Sicht-Pruefung).
Wenn ein Aufrufpfad gefunden wird der leere Versions schreibt: dort guarden.

## Anhang A — KB Top-15 Codes

| Code | Anzahl |
|---|---|
| BCE | 2'660 |
| BCD | 2'241 |
| BDA | 1'329 |
| BCAAA | 929 |
| BCAEA | 711 |
| BAJC | 537 |
| BAJB | 529 |
| BCCBY | 528 |
| BDDC | 522 |
| BCCAY | 513 |
| BAFCE | 452 |
| BAAA | 444 |
| BAHC | 439 |
| BDBA | 439 |
| BCC | 412 |

## Anhang B — KB Datenquellen (Source-Type)

| Quelle | Samples | Anteil |
|---|---|---|
| BatchImport | 16'053 | 73% |
| VideoTimestamp | 2'849 | 13% |
| DB3Profile | 2'229 | 10% |
| PdfPhoto | 482 | 2% |
| FeedbackReview | 164 | 0.75% |
| TeacherAnnotation | 79 | 0.36% |

Nur 1.1% (243 von 21'856) sind explizit menschlich validierte Samples
(FeedbackReview + TeacherAnnotation).

## Anhang C — Eval-Set-Manifest-Zustand

```
frozen:        True
approved:      120
exported:      120
total_cands:   120
frozen_at:     None
hashes_count:  0
hash_algorithm: -
```

`frozen_at` ist null und es gibt keinen Hash-Block — Migration faellig.

## Anhang D — ValidationLog Tagesverteilung

| Datum | Eintraege | Davon korrekt |
|---|---|---|
| 2026-05-20 | 32 | 32 (100%) — alle mit leerem EvidenceJson |
| 2026-05-18 | 5 | 5 (100%) |
| 2026-05-15 | 5 | 4 (80%) |
| 2026-05-14 | 15 | 11 (73%) |
| 2026-05-11 | 8 | 8 (100%) |

Die "73-80%"-Tage sehen realistisch aus, die "100%"-Tage sind verdaechtig
(moeglicherweise nur Accept-Events ohne Reject-Korrekturen).
