"""Training-Ops-Agent fuer SewerStudio — lauffaehiges Grundgeruest.

Der Agent ORCHESTRIERT nur. Er ruft deterministische Skripte (Schicht 1) als Tools auf,
liest Ergebnisse und schreibt eine Empfehlung. Er deployt NICHT, gibt KEIN Release frei
und fasst das versiegelte Abnahme-Set NICHT an — solche Aktionen existieren als Tool gar nicht.

Start:
    # Claude-Backend (Standard):
    set ANTHROPIC_API_KEY=...          &&  python training_ops_agent.py "Werte Run yolo_v1 aus."
    # Lokales Ollama-Backend:
    set TRAINING_OPS_BACKEND=ollama    &&  set TRAINING_OPS_MODEL=qwen3-vl:8b-q8  &&  python training_ops_agent.py "..."

Ohne Argument laeuft ein sicherer Standard-Prompt (nur Umgebungs-Report).
"""
from __future__ import annotations

import asyncio
import sys

import config
import schicht1
from guardrails import (
    GuardrailViolation,
    assert_eval_split_allowed,
    ensure_gpu_free_for_training,
    gpu_free_vram_mb,
    sidecar_running,
)

try:
    from claude_agent_sdk import ClaudeAgentOptions, create_sdk_mcp_server, query, tool
except ImportError:
    print(
        "Fehlt: claude-agent-sdk. Installieren mit:\n"
        "    pip install claude-agent-sdk\n"
        "(Python >= 3.10; laeuft nativ unter Windows.)",
        file=sys.stderr,
    )
    sys.exit(1)


def _text(msg: str) -> dict:
    """Einheitliche Text-Rueckgabe im vom SDK erwarteten Format."""
    return {"content": [{"type": "text", "text": msg}]}


# ── Tools (duenne, guardrail-geschuetzte Wrapper um Schicht 1) ────────────────
@tool("report_environment", "Zeigt Sidecar-Status und freien VRAM (read-only, immer erlaubt).", {})
async def report_environment(args):
    running = sidecar_running()
    free = gpu_free_vram_mb()
    free_txt = f"{free} MB frei" if free is not None else "nvidia-smi nicht verfuegbar"
    return _text(
        f"Sidecar: {'LAEUFT (Training gesperrt)' if running else 'aus (Training moeglich)'} | VRAM: {free_txt}"
    )


@tool("export_dataset", "Exportiert ein Dataset ueber den ExportPlanner (haltungs-sauberer Split).",
      {"dataset_version": str})
async def export_dataset(args):
    res = schicht1.export_dataset(args["dataset_version"])
    return _text(f"[export_dataset] ok={res['ok']}\n{res['message']}")


@tool("train_detect", "Startet YOLO-Detect-Training (imgsz 1280). Guardrail: nur wenn Sidecar aus & VRAM frei.",
      {"dataset_version": str})
async def train_detect(args):
    try:
        ensure_gpu_free_for_training()
    except GuardrailViolation as gv:
        return _text(f"VERWEIGERT (Guardrail): {gv}")
    res = schicht1.train_detect(args["dataset_version"])
    return _text(f"[train_detect] ok={res['ok']}\n{res['message']}")


@tool("run_eval", "Wertet ein Modellpaket auf Dev-Val aus. Guardrail: versiegeltes Abnahme-Set hart gesperrt.",
      {"model_paket": str, "split": str})
async def run_eval(args):
    split = args.get("split") or "devval"
    try:
        assert_eval_split_allowed(split)
    except GuardrailViolation as gv:
        return _text(f"VERWEIGERT (Guardrail): {gv}")
    res = schicht1.run_eval(args["model_paket"], split=split)
    return _text(f"[run_eval:{split}] ok={res['ok']}\n{res['message']}")


SYSTEM_PROMPT_DE = """\
Du bist der Training-Ops-Agent fuer SewerStudio. Du orchestrierst die Trainings-Pipeline,
liest Ergebnisse und schreibst am Ende eine klare Empfehlung. Regeln (nicht verhandelbar):

1. Du DEPLOYST NICHT, gibst KEIN Release frei und wertest das versiegelte Abnahme-Set NICHT aus.
   Solche Aktionen sind bewusst kein Tool. Deine Ausgabe endet immer mit 'Empfehlung' fuer den Menschen.
2. Sicherheit vor Effizienz: Ein uebersehener Schaden (v.a. Schweregrad 4-5) wiegt schwerer als
   eine schlechtere Skip-Quote. Melde jede moegliche Regression bei schweren Schaeden ausdruecklich.
3. Trainiere nie, wenn der Sidecar laeuft (VRAM-Budget). Pruefe im Zweifel zuerst report_environment.
4. Arbeite in kleinen Schritten: ein Tool, Ergebnis lesen, naechster Schritt. Bei Anomalien oder
   fehlenden Skripten: abbrechen und melden, nicht raten.
5. Antworte auf Deutsch, knapp und faktenbasiert.
"""


def build_options() -> "ClaudeAgentOptions":
    backend_desc = config.apply_backend_env()
    print(f"[backend] {backend_desc}", file=sys.stderr)

    server = create_sdk_mcp_server(
        name="training_ops",
        version="1.0.0",
        tools=[report_environment, export_dataset, train_detect, run_eval],
    )
    kwargs = dict(
        mcp_servers={"training_ops": server},
        # Whitelist: nur unsere Tools + read-only. BEWUSST kein Bash/Write/Edit.
        allowed_tools=[
            "mcp__training_ops__report_environment",
            "mcp__training_ops__export_dataset",
            "mcp__training_ops__train_detect",
            "mcp__training_ops__run_eval",
        ],
        system_prompt=SYSTEM_PROMPT_DE,
        max_turns=config.MAX_TURNS,
    )
    if config.MODEL:
        kwargs["model"] = config.MODEL
    return ClaudeAgentOptions(**kwargs)


def _print_message(msg) -> None:
    """Robuste Textausgabe unabhaengig von exakten SDK-Klassennamen."""
    content = getattr(msg, "content", None)
    if isinstance(content, list):
        for block in content:
            text = getattr(block, "text", None)
            if text:
                print(text)
    elif isinstance(content, str):
        print(content)


async def run(prompt: str) -> None:
    options = build_options()
    async for msg in query(prompt=prompt, options=options):
        _print_message(msg)


def main() -> None:
    prompt = " ".join(sys.argv[1:]).strip() or (
        "Berichte den aktuellen Umgebungsstatus (report_environment) und erklaere in einem Satz, "
        "ob ein Training jetzt starten duerfte."
    )
    asyncio.run(run(prompt))


if __name__ == "__main__":
    main()
