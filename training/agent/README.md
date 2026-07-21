# Training-Ops-Agent — Grundgerüst

Orchestriert die Trainings-Pipeline von SewerStudio. **Der Agent entscheidet nichts Sicherheitskritisches** — er ruft deterministische Skripte auf, liest Ergebnisse und schreibt eine Empfehlung. Deployen, Release freigeben und das versiegelte Abnahme-Set anfassen kann er nicht, weil es dafür kein Tool gibt.

Bezug: `../../Agent-Konzept_Training-Ops.md` und `../../Trainingsplan_Detail_KI-Pipeline.md` (v1.2).

## Dateien

| Datei | Zweck |
|---|---|
| `training_ops_agent.py` | Agent + Tools + CLI-Einstieg |
| `guardrails.py` | Sicherheitsprüfungen im Code (VRAM/Sidecar, versiegelte Splits, Pfad-Sandbox) |
| `schicht1.py` | dünne Wrapper um die Skripte in `../scripts/` (melden ehrlich, wenn ein Skript noch fehlt) |
| `config.py` | Pfade, Sidecar-URL, Backend-Wahl — alles per ENV überschreibbar |
| `test_guardrails.py` | fokussierte Tests der Kernlogik (ohne SDK lauffähig) |

## Installation

```bat
py -m venv .venv && .venv\Scripts\activate
pip install -r requirements.txt
```

## Start

**Claude-Backend (empfohlen für Orchestrierung):**
```bat
set ANTHROPIC_API_KEY=sk-...
python training_ops_agent.py "Berichte den Umgebungsstatus."
```

**Lokal auf der RTX 5090 (Ollama, privat/gratis, schwächer bei langen Tool-Ketten):**
```bat
set TRAINING_OPS_BACKEND=ollama
set TRAINING_OPS_MODEL=qwen3-vl:8b-q8
python training_ops_agent.py "Berichte den Umgebungsstatus."
```

Ohne Argument läuft ein sicherer Standard-Prompt (nur Umgebungs-Report). Der Agent ist **sofort lauffähig**: Die Trainings-Tools melden „TODO: Skript fehlt", solange die Schicht-1-Skripte aus Trainingsplan Phase 0/2 noch nicht existieren — nichts wird gefaked.

## Tests

```bat
python test_guardrails.py
```

## Eingebaute Guardrails

- **VRAM-Schutz:** `train_detect` verweigert, solange der Sidecar erreichbar ist (`127.0.0.1:8100/health`) oder zu wenig VRAM frei ist — schützt das 29-GB-Laufzeitbudget.
- **Versiegeltes Abnahme-Set:** `run_eval` läuft nur auf Dev-Val; jeder Split mit `abnahme/gold/sealed/...` wird hart abgelehnt.
- **Tool-Whitelist:** Der Agent hat nur die vier `training_ops`-Tools — **kein** generisches Bash/Write/Edit.
- **Schleifen-Limit:** `max_turns` (Standard 24) verhindert Kosten-/Endlosausreißer.

## Nächste Schritte

1. Schicht-1-Skripte unter `../scripts/` bauen (`export_dataset.py`, `train_detect.py`, `run_eval.py`, `doppellauf.py`) — dann werden die Tools „scharf".
2. Weitere Tools ergänzen (`doppellauf`, `write_report`) analog zum bestehenden Muster.
3. Später: geplanter Nachtlauf per Windows-Taskplaner (`python training_ops_agent.py "..."`).

## Konfiguration (ENV-Auszug)

| Variable | Default | Zweck |
|---|---|---|
| `TRAINING_OPS_BACKEND` | `claude` | `claude` oder `ollama` |
| `TRAINING_OPS_MODEL` | — | Modellname je Backend |
| `KI_BRAIN_ROOT` | `C:\KI_BRAIN\training` | Datenwurzel (außerhalb Repo) |
| `SEWER_SIDECAR_HEALTH_URL` | `http://127.0.0.1:8100/health` | Sidecar-Präsenzcheck |
| `TRAINING_OPS_MIN_FREE_VRAM_MB` | `28000` | VRAM-Schwelle fürs Training |
| `TRAINING_OPS_MAX_TURNS` | `24` | Obergrenze Agentenschleife |
