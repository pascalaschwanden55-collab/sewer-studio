# Phase 6.3 — KI-Schicht komplett aus UI (Inventar + Empfehlung)

**Datum:** 2026-05-04
**Auftrag:** "KI-Schicht komplett aus UI ausziehen (144 Dateien)" — Audit A1 (Konsens 3/3, "langfristig").
**Resultat:** Inventar + Migrationsplan-Verweis. KEIN Code-Eingriff.

---

## A. Beziehung zu Phase 5.3

Phase 5.3 (Inventar `PHASE_5.3_KI_AUS_UI_INVENTAR.md`) deckt den **ersten Schritt** ab:
- Interfaces in Application-Schicht
- Implementierungen in Infrastructure-Schicht
- UI ruft via Interfaces

Phase 6.3 ist die **vollstaendige Bereinigung** — der Endzustand:
- `src/AuswertungPro.Next.UI/Ai/` ist **leer oder enthaelt nur UI-Adapter** (Dispatcher.Invoke-Wrapper, LiveDetection-UI-Pipeline, PhotoAssistant-Tools mit Canvas-Interaktion).
- 90% der KI-Logik lebt in Application + Infrastructure.
- Tests fuer KI-Code laufen ohne WPF-Referenz.

---

## B. Aktueller Stand vs. Endzustand

| Bereich | Heute | Phase 5.3 (Mid) | Phase 6.3 (End) |
|---|---|---|---|
| `UI/Ai/KnowledgeBase/` (9 files) | hier | Application+Infra | nur Adapter (1-2 files) |
| `UI/Ai/Pipeline/` (12) | hier | Application+Infra | nur LiveDetection-Adapter (1-2) |
| `UI/Ai/Ollama/` (3) | hier | Infrastructure | leer |
| `UI/Ai/Sanierung/` (6) | hier | Application+Infra | leer |
| `UI/Ai/Training/` (47) | hier | Application+Infra | leer |
| `UI/Ai/QualityGate/` (11) | hier | Application | leer |
| `UI/Ai/Teacher/` (4) | hier | Application | leer |
| `UI/Ai/SelfImproving/` (7) | hier | Application+Infra | leer |
| `UI/Ai/PhotoAssistant/` (5) | hier | hier (UI-spezifisch) | hier (UI-spezifisch) |
| `UI/Ai/Monitoring/` (3) | hier | hier (UI-spezifisch) | hier (UI-spezifisch) |
| `UI/Ai/Shared/` (4) | hier | Application | leer |
| Top-Level Files (~30) | hier | gemischt | <5 verbleibend |

**Endzustand:** ~10 UI-Adapter-Files in `UI/Ai/`, 137 Files migriert.

---

## C. Was Phase 6.3 ueber Phase 5.3 hinaus macht

1. **Komplette Bereinigung** der Top-Level-Files (PythonSidecarService, OllamaClient, etc.).
2. **Tests umziehen** — alle Pipeline-Tests in eigenes `tests/Application.Ai.Tests/`-Projekt (kein WPF mehr).
3. **Aufrufer-Migration komplett** — UI ruft NUR Interfaces, kein einziger `using AuswertungPro.Next.UI.Ai.*` in PageVM/Window/CodeBehind.
4. **Build-Reihenfolge optimiert** — UI haengt nur an Application, nicht mehr an Konkrete-Implementierungen.

---

## D. Warum "langfristig" im Audit

Phase 6.3 ist nicht eine eigene Session, sondern ein **Wartungs-Endzustand**:
- Erreicht durch Phase 5.3 + Phase 6.1 + Phase 6.2 + ggf. weitere Bereinigungen
- Pro Code-Aenderung wird darauf geachtet, dass kein neuer KI-Code im UI landet
- "Definition of Done" fuer den langfristigen Refactor

**Konkret:** Phase 6.3 wird nicht "gemacht", sondern **erreicht**, sobald 5.3 + 6.1 + 6.2 durch sind und die UI-Aufrufer alle ueber Interfaces laufen.

---

## E. Empfehlung

1. **Erst Phase 5.3** durchziehen (KI-Schicht raus, Interfaces).
2. **Dann Phase 6.1 + 6.2** (PlayerWindow + TrainingCenterVM zerlegen, dabei keine direkten KI-Aufrufe mehr).
3. **Phase 6.3 ist die Verifikation** — am Ende: `find src/AuswertungPro.Next.UI/Ai -name "*.cs" | wc -l` < 15.

---

## F. Akzeptanz-Kriterium fuer Phase 6.3

Wenn alle drei Punkte erfuellt sind, ist Phase 6.3 abgeschlossen:
1. `src/AuswertungPro.Next.UI/Ai/` enthaelt < 15 Files (heute 147).
2. Alle Pipeline-Tests laufen ohne `<UseWPF>true</UseWPF>`-Projekt-Referenz.
3. `grep -rl "AuswertungPro.Next.UI.Ai" src/AuswertungPro.Next.{Domain,Application,Infrastructure}/` ist leer (keine Schichten-Verletzungen).

---

## G. Akzeptanz dieser Inventar-Phase

- Endzustand klar definiert (3 Akzeptanz-Kriterien).
- Beziehung zu Phase 5.3 / 6.1 / 6.2 dokumentiert.
- "Langfristig" ist konkret als Wartungs-Endzustand spezifiziert.
- ⏸️ Erreicht nach Migration der vorgelagerten Phasen.
- KEIN Code-Eingriff in dieser Iteration.
