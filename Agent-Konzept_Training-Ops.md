# Agent-Konzept — „Training-Ops-Agent" für SewerStudio

**Version:** 1.0 · **Datum:** 2026-07-16 · **Bezug:** `Trainingsplan_Detail_KI-Pipeline.md` (v1.2), `CLAUDE.md`
**Ziel:** Ein Agent, der die Trainings-Workflows der KI-Pipeline automatisiert — Export, Training, Eval, Doppellauf-Vergleich, Reports — ohne die Sicherheits-Gates des Trainingsplans zu unterlaufen.

---

## 1. Grundprinzip: Was ein Agent ist — und was er hier NICHT sein darf

Ein Agent = LLM in einer Schleife: Aufgabe → Werkzeug aufrufen → Ergebnis lesen → nächster Schritt → … → fertig melden.

**Thin-AI konsequent weitergedacht:** Alles, was ein Skript deterministisch kann (Export, Training starten, Metriken berechnen, Diffs erzeugen), macht ein **Skript**. Der Agent übernimmt nur, was Urteilsvermögen und Sprache braucht:

| Deterministisch (Skripte, kein LLM) | Agent (Urteil + Sprache) | Mensch (nie delegieren) |
|---|---|---|
| ExportPlan erzeugen, Dataset exportieren | Zyklus orchestrieren, Reihenfolge/Abbruch entscheiden | Label-Review-Entscheidungen |
| YOLO-Training, Eval-Metriken, Engine-Build | Metriken/Diffs interpretieren, Anomalien melden | **Release-Gate abhaken** |
| Doppellauf A/B, Diff-Berechnung | Reports schreiben (`experiments.md`, Zyklus-Report) | Abnahme-Set-Freigabe |
| Frame-Extraktion, Active-Learning-Ranking | Label-Kandidaten begründet vorpriorisieren | Quarantäne-Freigaben, class_map-Änderungen |

**Eiserne Regel:** Der Agent endet jeden Lauf mit einem **Report + Vorschlag**. Deployen, Release freigeben, versiegelte Daten anfassen — macht er nie.

---

## 2. Architektur (3 Schichten)

```
┌─ Schicht 3: MENSCH ─────────────────────────────────────────┐
│  Review in Label Studio · Release-Gate · Abnahme            │
├─ Schicht 2: AGENT (Claude Agent SDK, Python) ───────────────┤
│  Orchestriert Tools, liest Logs/Metriken, schreibt Reports, │
│  schlägt nächsten Schritt vor                               │
├─ Schicht 1: SKRIPTE (training/, deterministisch) ───────────┤
│  export_dataset · train_detect · train_cls · run_eval ·     │
│  doppellauf · diff_events · build_engine(dry) · extract     │
└──────────────────────────────────────────────────────────────┘
```

Die Skripte der Schicht 1 sind **genau die Arbeitspakete aus Trainingsplan v1.2** (ExportPlanner, Eval-Harness, Doppellauf-Diff). Der Agent kommt also **nach** Phase 0 — er automatisiert, was als Skript schon existiert und einzeln funktioniert.

---

## 3. Basis-Entscheidung: Claude Agent SDK, wahlweise Cloud oder lokal

**Empfehlung: Claude Agent SDK (Python)** — `pip install claude-agent-sdk` (Python ≥3.10, läuft nativ auf Windows). Gründe:
- Fertige Agent-Schleife, Built-in-Tools (Read, Bash, Grep …), Custom Tools per `@tool`-Decorator.
- Feingranulare **Permissions** (`allowedTools`-Whitelist, `disallowed_tools`) — passt exakt zu den Guardrails unten.
- **Ein Code, zwei Backends:** Standard = Claude API (stärkstes Urteilsvermögen, Kosten pro Token). Alternativ **lokal via Ollama** (ab v0.14 native Anthropic-Messages-API, 3 Env-Vars) — dann läuft der Agent auf deinem Qwen: privat, kostenlos, aber schwächer bei langen Tool-Ketten (Kontext ≥64K konfigurieren).

**Praktische Aufteilung:**
- **Orchestrierung/Reports → Claude API** (wenige Läufe pro Woche, überschaubare Kosten, deutlich zuverlässiger bei Multi-Step).
- **Einfache Routine-Urteile (z. B. Log-Zusammenfassung) → lokal/Qwen**, wenn gewünscht.
- Nicht als Agent-Backend geeignet: das produktive `qwen3-vl` im Sidecar während Trainingsläufen (VRAM!).

**Low-Code-Alternative (ohne eigenes Programm):** Claude Code im Repo nutzen — Workflows als Skills/Slash-Commands beschreiben, headless per `claude -p "Führe Trainingszyklus aus"` starten (auch per Windows-Taskplaner). Guter Einstieg; der SDK-Agent ist die sauberere Dauerlösung, weil Tools/Guardrails im Code fixiert sind statt im Prompt.

---

## 4. Tool-Design (Schicht 2 → Schicht 1)

Jedes Tool ist ein dünner Wrapper um ein Skript/Kommando — mit eingebauten Schutzprüfungen:

| Tool | Wrappt | Schutzprüfung im Tool (nicht dem LLM überlassen!) |
|---|---|---|
| `export_dataset(version)` | ExportPlanner + Export | verweigert, wenn Abnahme-/Dev-Val-Haltung im Train-Split landen würde; Quarantäne nie exportieren |
| `train_detect(dataset, cfg)` / `train_cls(...)` | Ultralytics-Lauf | verweigert, wenn Sidecar/Inferenz aktiv (VRAM-Kollision); imgsz-Default 1280/1024 |
| `run_eval(model, split)` | Eval-Harness | `split="abnahme"` **hart gesperrt** — nur Dev-Val erlaubt |
| `doppellauf(candidate, videolist)` | A/B-Läufe + Diff | nur auf definierte Videoliste |
| `read_metrics(run)` / `read_logs(...)` | Dateien lesen | read-only |
| `select_label_candidates(n)` | Active-Learning-Ranking | schreibt nur Vorschlagsliste, importiert nichts |
| `write_report(cycle)` | Report → `KI_BRAIN\training\reports\` | schreibt nur in reports/ und experiments.md |

**Bewusst KEINE Tools:** `deploy_engine`, `promote_cls`, `edit_class_map`, `unlock_quarantine`, `eval_abnahme` — diese Aktionen existieren für den Agenten nicht. Was kein Tool ist, kann er nicht tun; das ist die stärkste Leitplanke.

---

## 5. Code-Skelett (Minimalgerüst)

```python
# training/agent/training_ops_agent.py
from claude_agent_sdk import tool, create_sdk_mcp_server, ClaudeAgentOptions, query

@tool("run_eval", "Eval-Harness auf Dev-Val ausführen", {"model_paket": str})
async def run_eval(args):
    if "abnahme" in args["model_paket"].lower():          # Hartes Verbot
        return {"content": [{"type": "text", "text": "VERWEIGERT: Abnahme-Set ist versiegelt."}]}
    result = subprocess_run_eval(args["model_paket"], split="devval")   # Schicht-1-Skript
    return {"content": [{"type": "text", "text": result.summary_json}]}

@tool("train_detect", "YOLO-Detect-Training starten (imgsz 1280)", {"dataset_version": str})
async def train_detect(args):
    if sidecar_running():                                  # VRAM-Schutz
        return {"content": [{"type": "text", "text": "VERWEIGERT: Sidecar aktiv (VRAM-Budget)."}]}
    run_id = start_ultralytics(args["dataset_version"], imgsz=1280)
    return {"content": [{"type": "text", "text": f"Training gestartet: {run_id}"}]}

server = create_sdk_mcp_server(name="training_ops", version="1.0.0",
                               tools=[run_eval, train_detect])  # + export, doppellauf, report ...

options = ClaudeAgentOptions(
    mcp_servers={"training_ops": server},
    allowed_tools=["mcp__training_ops__*", "Read", "Grep"],   # Whitelist — kein Write/Bash!
    system_prompt=SYSTEM_PROMPT_DE,   # Regeln aus Abschnitt 1+4, Trainingsplan-v1.2-Kontext
)

async for msg in query(prompt="Führe Flywheel-Zyklus für Kandidat v003 aus und schreibe den Zyklus-Report.",
                       options=options):
    print(msg)
```

**System-Prompt-Kern (deutsch, ~20 Zeilen):** Rolle (Training-Ops für SewerStudio), Ablauf eines Zyklus laut Plan v1.2, Vorrangregel (Sev 4/5 vor Skip-Quote), Endzustand immer „Report + Empfehlung, keine Freigabe", bei Anomalien abbrechen und melden.

---

## 6. Ein typischer Agent-Lauf (Flywheel-Zyklus, Phase 4 des Trainingsplans)

1. `export_dataset("v003")` → prüft Sperrlisten, exportiert.
2. `train_detect("v003")` → wartet auf Abschluss, liest Trainings-Log.
3. `run_eval(...)` auf Dev-Val → vergleicht mit Vorversion (liest `experiments.md`).
4. Bei Verbesserung: `doppellauf(...)` auf der festen Videoliste → Diff auf Ereignis-Ebene.
5. `select_label_candidates(500)` → priorisierte Liste für den nächsten Review.
6. `write_report(...)` → Zyklus-Report inkl. **Empfehlung**: „Kandidat v003 bereit fürs Release-Gate — bitte prüfen" oder „Regression bei BAC — nicht releasen, Vorschlag: …".
7. **Ende.** Mensch reviewt, entscheidet, deployt (oder nicht).

Später (Ausbaustufe 3): nächtlicher Start per Windows-Taskplaner (`python training_ops_agent.py --cycle nightly`), Ergebnis-Report morgens lesen.

---

## 7. Ausbaustufen

| Stufe | Was | Voraussetzung |
|---|---|---|
| **0** | Schicht-1-Skripte einzeln manuell nutzbar | Trainingsplan Phase 0 abgeschlossen |
| **1** | Einzel-Aufgaben an Agent geben („werte Run X aus, schreib Report") — interaktiv | Stufe 0 + SDK-Setup |
| **2** | Voller Zyklus-Lauf (Abschnitt 6) auf Zuruf | Tools + Guardrails getestet |
| **3** | Geplante Läufe (nachts) + Report-Ablage | Stufe 2 stabil, VRAM-Check zuverlässig |

Einstiegsaufwand: Stufe 1 in **1–2 Tagen** (SDK-Setup + 2–3 Tools), Stufe 2 in ~1 Woche — vorausgesetzt, die Skripte aus Phase 0 existieren.

---

## 8. Risiken & Gegenmaßnahmen

| Risiko | Gegenmaßnahme |
|---|---|
| Agent umgeht Gates „kreativ" | Gates leben **in den Tools/Skripten** (Code), nicht im Prompt; kritische Aktionen haben kein Tool |
| VRAM-Kollision Training ↔ Betrieb | `sidecar_running()`-Check in jedem Trainings-Tool |
| Kosten laufen davon (Claude API) | Zyklus-Läufe sind selten (1–2/Woche); `max_turns`-Limit; Kosten pro Lauf im Report ausweisen |
| Lokales Qwen halluziniert Tool-Ketten | Lokal nur für Einzelaufgaben (Zusammenfassen), Orchestrierung bei Claude API belassen |
| Agent-Schreibzugriffe streuen | `allowed_tools`-Whitelist ohne generisches Write/Bash; Reports nur über `write_report` |

---

## Quellen

- Claude Agent SDK (Python, PyPI) — https://pypi.org/project/claude-agent-sdk/
- Custom Tools (`@tool`, `create_sdk_mcp_server`) — https://code.claude.com/docs/en/agent-sdk/custom-tools
- Permissions (`allowedTools`, Modi) — https://platform.claude.com/docs/en/agent-sdk/permissions
- Agent SDK Überblick — https://code.claude.com/docs/en/agent-sdk/overview
- Claude Code Skills / Hooks / Headless (`claude -p`) — https://code.claude.com/docs/en/skills · https://code.claude.com/docs/en/hooks
- Ollama: native Anthropic-Messages-API (lokales Backend) — https://github.com/ollama/ollama
