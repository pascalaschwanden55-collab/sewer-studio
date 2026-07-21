"""Konfiguration des Training-Ops-Agenten.

Trennt bewusst:
- CODE/Skripte  -> im Repo (dieser Ordner + ../scripts)
- DATEN/Modelle -> ausserhalb des Repos unter KI_BRAIN (siehe Trainingsplan v1.2, Abschnitt B)

Alle Werte sind per Umgebungsvariable ueberschreibbar, damit nichts hartkodiert im Repo klebt.
"""
from __future__ import annotations

import os
from pathlib import Path

# ── Pfade ────────────────────────────────────────────────────────────────────
# Repo-Wurzel des Trainings-Toolings (…\Sewer-Studio_KI_4.5\training)
REPO_TRAINING_ROOT = Path(__file__).resolve().parents[1]
# Deterministische Schicht-1-Skripte (werden in Trainingsplan Phase 0/2 gebaut)
SCRIPTS_DIR = Path(os.getenv("TRAINING_OPS_SCRIPTS_DIR", REPO_TRAINING_ROOT / "scripts"))

# Datenwurzel ausserhalb des Repos (Kundenbilder, Datasets, Modelle, Reports)
KI_BRAIN_ROOT = Path(os.getenv("KI_BRAIN_ROOT", r"C:\KI_BRAIN\training"))
DATASETS_DIR = KI_BRAIN_ROOT / "datasets"
CANDIDATES_DIR = KI_BRAIN_ROOT / "models" / "candidates"
REPORTS_DIR = Path(os.getenv("TRAINING_OPS_REPORTS_DIR", KI_BRAIN_ROOT / "reports"))
EXPERIMENTS_LOG = REPO_TRAINING_ROOT / "experiments.md"  # nur Text -> darf ins Repo

# ── Sidecar (fuer VRAM-Guardrail) ────────────────────────────────────────────
# Verifiziert: sidecar/sidecar/config.py -> host 127.0.0.1, port 8100, GET /health.
# /health bleibt waehrend der Analyse bewusst erreichbar -> erreichbar == Prozess laeuft
# == Modelle koennen VRAM halten == kein Training starten.
SIDECAR_HEALTH_URL = os.getenv("SEWER_SIDECAR_HEALTH_URL", "http://127.0.0.1:8100/health")

# Mindestens freier VRAM (MB), bevor ein Training starten darf (zweiter, unabhaengiger Guard).
# 29 GB Laufzeitbudget -> fuer Training wollen wir die Karte praktisch leer.
MIN_FREE_VRAM_MB = int(os.getenv("TRAINING_OPS_MIN_FREE_VRAM_MB", "28000"))

# ── Versiegelte / gesperrte Splits (duerfen vom Agenten NIE ausgewertet werden) ──
# Abnahme/Gold ist versiegelt (Trainingsplan v1.2, Abschnitt F). run_eval nur auf Dev-Val.
SEALED_SPLIT_TOKENS = ("abnahme", "gold", "sealed", "versiegelt", "testset_gold", "holdout")

# ── Modell-Defaults (Trainingsplan v1.2) ─────────────────────────────────────
DETECT_IMGSZ = int(os.getenv("TRAINING_OPS_DETECT_IMGSZ", "1280"))  # Engine/Laufzeit = 1280
CLS_IMGSZ = int(os.getenv("TRAINING_OPS_CLS_IMGSZ", "1024"))

# ── LLM-Backend ──────────────────────────────────────────────────────────────
# "claude" = Claude API (stark, Kosten pro Token) | "ollama" = lokal auf der RTX 5090 (privat, gratis)
BACKEND = os.getenv("TRAINING_OPS_BACKEND", "claude").strip().lower()
# Modellname je Backend. Fuer Claude z.B. ein Sonnet/Opus-Alias; fuer Ollama ein lokales Modell.
MODEL = os.getenv("TRAINING_OPS_MODEL", "").strip()
# Ollama spricht ab v0.14 die native Anthropic-Messages-API.
OLLAMA_BASE_URL = os.getenv("OLLAMA_ANTHROPIC_BASE_URL", "http://localhost:11434")

# Sicherheitsobergrenze fuer die Agentenschleife (verhindert Endlos-/Kostenausreisser).
MAX_TURNS = int(os.getenv("TRAINING_OPS_MAX_TURNS", "24"))


def apply_backend_env() -> str:
    """Setzt Backend-spezifische Umgebungsvariablen und liefert eine Kurzbeschreibung.

    Claude:  erwartet ANTHROPIC_API_KEY in der Umgebung.
    Ollama:  leitet die SDK-Aufrufe auf den lokalen Anthropic-kompatiblen Endpunkt um.
             Hinweis: lokales Tool-Use ist weniger zuverlaessig -> nur fuer einfache Laeufe.
    """
    if BACKEND == "ollama":
        os.environ["ANTHROPIC_BASE_URL"] = OLLAMA_BASE_URL
        # Dummy-Token: der lokale Endpunkt prueft nicht, das SDK erwartet aber einen Wert.
        os.environ.setdefault("ANTHROPIC_AUTH_TOKEN", "ollama-local")
        os.environ.setdefault("ANTHROPIC_API_KEY", "ollama-local")
        return f"ollama @ {OLLAMA_BASE_URL} (Modell: {MODEL or 'ENV TRAINING_OPS_MODEL setzen'})"
    return "claude-api (ANTHROPIC_API_KEY erforderlich)"
