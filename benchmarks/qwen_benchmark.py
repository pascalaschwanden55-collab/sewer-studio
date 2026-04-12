"""Qwen 3B vs 8B Benchmark fuer SewerStudio.

Sendet identische Frames + Prompts an beide Modelle via Ollama API.
Vergleicht: VSA-Codes, JSON-Stabilitaet, Latenz, Severity.

Nutzung:
    python benchmarks/qwen_benchmark.py
    python benchmarks/qwen_benchmark.py --candidate qwen2.5-vl:3b
"""

from __future__ import annotations

import argparse
import base64
import csv
import json
import logging
import os
import sys
import time
from pathlib import Path

import requests

logging.basicConfig(level=logging.INFO, format="%(asctime)s [%(levelname)s] %(message)s")
logger = logging.getLogger(__name__)

OLLAMA_URL = os.environ.get("SEWERSTUDIO_OLLAMA_URL", "http://localhost:11434")

# Exakter Prompt aus EnhancedVisionAnalysisService.cs (gekuerzt fuer Benchmark)
SYSTEM_PROMPT = """Du analysierst ein Kanalinspektion-Bild (TV-Kamera in einem Abwasserkanal).
Erkenne ALLE sichtbaren Schaeden, Anomalien und Bestandsmerkmale.
Antworte AUSSCHLIESSLICH auf Deutsch.
Gib AUSSCHLIESSLICH gueltiges JSON zurueck (keine Erklaerung, kein Markdown).

JSON-Format:
{
  "findings": [
    {
      "label": "BABBA",
      "vsa_code_hint": "BABBA",
      "description": "Laengsriss in Scheitel",
      "severity": 3,
      "position_clock": "12",
      "extent_percent": 15,
      "cross_section_reduction_percent": null
    }
  ],
  "meter": null,
  "image_quality": "gut",
  "is_empty_frame": false
}

Regeln:
- label MUSS ein VSA/EN 13508-2 Code sein (BAB, BCAAA, BCD, BBFA etc.)
- severity: 1=Beobachtung, 2=leicht, 3=mittel, 4=schwer, 5=kritisch
- BC-Codes (BCA, BCC, BCD, BCE) sind severity=1
- Wenn NICHTS sichtbar: findings=[], is_empty_frame=true
- Verwende den SPEZIFISCHSTEN Code (BABBA statt BAB)
"""

USER_PROMPT = "Analysiere dieses Kanalbild. Melde alle sichtbaren Schaeden und Bestandsmerkmale."


def ollama_chat(model: str, image_b64: str, timeout: int = 120) -> tuple[str, float]:
    """Sendet Bild an Ollama und gibt (response_text, latency_ms) zurueck."""
    t0 = time.perf_counter()
    resp = requests.post(
        f"{OLLAMA_URL}/api/chat",
        json={
            "model": model,
            "messages": [
                {"role": "system", "content": SYSTEM_PROMPT},
                {
                    "role": "user",
                    "content": USER_PROMPT,
                    "images": [image_b64],
                },
            ],
            "stream": False,
            "options": {"num_ctx": 4096, "temperature": 0},
            "format": "json",
        },
        timeout=timeout,
    )
    latency_ms = (time.perf_counter() - t0) * 1000
    resp.raise_for_status()
    content = resp.json().get("message", {}).get("content", "")
    return content, latency_ms


def parse_response(raw: str) -> dict:
    """Parst JSON-Response, gibt strukturiertes Dict zurueck."""
    try:
        # Versuche JSON direkt
        data = json.loads(raw)
        return {
            "json_valid": True,
            "findings_count": len(data.get("findings", [])),
            "vsa_codes": [f.get("label", "") for f in data.get("findings", [])],
            "severities": [f.get("severity", 0) for f in data.get("findings", [])],
            "is_empty": data.get("is_empty_frame", False),
            "image_quality": data.get("image_quality", ""),
            "raw": data,
        }
    except json.JSONDecodeError:
        # Versuche JSON aus Text zu extrahieren
        import re
        m = re.search(r"\{[\s\S]*\}", raw)
        if m:
            try:
                data = json.loads(m.group())
                return {
                    "json_valid": True,
                    "findings_count": len(data.get("findings", [])),
                    "vsa_codes": [f.get("label", "") for f in data.get("findings", [])],
                    "severities": [f.get("severity", 0) for f in data.get("findings", [])],
                    "is_empty": data.get("is_empty_frame", False),
                    "image_quality": data.get("image_quality", ""),
                    "raw": data,
                }
            except json.JSONDecodeError:
                pass
        return {
            "json_valid": False,
            "findings_count": 0,
            "vsa_codes": [],
            "severities": [],
            "is_empty": None,
            "image_quality": "",
            "raw": raw,
        }


def code_matches_class(vsa_codes: list[str], expected_class: str) -> bool:
    """Prueft ob mindestens ein VSA-Code zur erwarteten Klasse passt."""
    expected_class_upper = expected_class.upper()
    for code in vsa_codes:
        code_upper = code.upper().strip()
        # Exakter Prefix-Match (BAB matcht BAB, BABA, BABBA etc.)
        if code_upper.startswith(expected_class_upper):
            return True
        # Spezialfall: OTHER-Klasse → kein Code erwartet
        if expected_class_upper == "OTHER" and not code_upper:
            return True
    return False


def run_benchmark(baseline: str, candidate: str, frames_dir: str, output_csv: str):
    """Fuehrt den A/B-Benchmark aus."""
    frames_path = Path(frames_dir)
    images = sorted(frames_path.glob("*.jpg")) + sorted(frames_path.glob("*.png"))

    if not images:
        logger.error("Keine Bilder gefunden in %s", frames_dir)
        return

    logger.info("=== Qwen Benchmark: %s vs %s ===", baseline, candidate)
    logger.info("Frames: %d", len(images))

    results = []
    baseline_codes_match = 0
    candidate_codes_match = 0
    baseline_json_ok = 0
    candidate_json_ok = 0

    for i, img_path in enumerate(images):
        # Klasse aus Dateiname extrahieren (z.B. "BAB_frame123.jpg" → "BAB")
        expected_class = img_path.stem.split("_")[0]

        with open(img_path, "rb") as f:
            b64 = base64.b64encode(f.read()).decode()

        logger.info("[%d/%d] %s (erwartet: %s)", i + 1, len(images), img_path.name, expected_class)

        # Baseline (8B)
        try:
            raw_8b, lat_8b = ollama_chat(baseline, b64)
            res_8b = parse_response(raw_8b)
        except Exception as exc:
            logger.warning("  8B FEHLER: %s", exc)
            res_8b = {"json_valid": False, "findings_count": 0, "vsa_codes": [], "severities": [], "is_empty": None, "image_quality": ""}
            lat_8b = 0

        # Candidate (3B)
        try:
            raw_3b, lat_3b = ollama_chat(candidate, b64)
            res_3b = parse_response(raw_3b)
        except Exception as exc:
            logger.warning("  3B FEHLER: %s", exc)
            res_3b = {"json_valid": False, "findings_count": 0, "vsa_codes": [], "severities": [], "is_empty": None, "image_quality": ""}
            lat_3b = 0

        # Vergleich
        match_8b = code_matches_class(res_8b["vsa_codes"], expected_class)
        match_3b = code_matches_class(res_3b["vsa_codes"], expected_class)
        if match_8b:
            baseline_codes_match += 1
        if match_3b:
            candidate_codes_match += 1
        if res_8b["json_valid"]:
            baseline_json_ok += 1
        if res_3b["json_valid"]:
            candidate_json_ok += 1

        sev_8b = sum(res_8b["severities"]) / max(len(res_8b["severities"]), 1)
        sev_3b = sum(res_3b["severities"]) / max(len(res_3b["severities"]), 1)

        row = {
            "frame": img_path.name,
            "expected_class": expected_class,
            "8b_latency_ms": round(lat_8b, 0),
            "8b_json_valid": res_8b["json_valid"],
            "8b_findings": res_8b["findings_count"],
            "8b_codes": "|".join(res_8b["vsa_codes"]),
            "8b_code_match": match_8b,
            "8b_severity_avg": round(sev_8b, 1),
            "3b_latency_ms": round(lat_3b, 0),
            "3b_json_valid": res_3b["json_valid"],
            "3b_findings": res_3b["findings_count"],
            "3b_codes": "|".join(res_3b["vsa_codes"]),
            "3b_code_match": match_3b,
            "3b_severity_avg": round(sev_3b, 1),
        }
        results.append(row)

        logger.info("  8B: %dms, %d findings [%s] match=%s | 3B: %dms, %d findings [%s] match=%s",
                     lat_8b, res_8b["findings_count"], ",".join(res_8b["vsa_codes"][:3]), match_8b,
                     lat_3b, res_3b["findings_count"], ",".join(res_3b["vsa_codes"][:3]), match_3b)

    # CSV speichern
    csv_path = Path(output_csv)
    with open(csv_path, "w", newline="", encoding="utf-8") as f:
        writer = csv.DictWriter(f, fieldnames=results[0].keys())
        writer.writeheader()
        writer.writerows(results)
    logger.info("CSV gespeichert: %s", csv_path)

    # Zusammenfassung
    n = len(results)
    avg_lat_8b = sum(r["8b_latency_ms"] for r in results) / n
    avg_lat_3b = sum(r["3b_latency_ms"] for r in results) / n

    logger.info("\n" + "=" * 60)
    logger.info("ERGEBNIS: %s vs %s (%d Frames)", baseline, candidate, n)
    logger.info("=" * 60)
    logger.info("                        %-20s %-20s", baseline, candidate)
    logger.info("  Code-Match:           %d/%d (%.0f%%)           %d/%d (%.0f%%)",
                baseline_codes_match, n, baseline_codes_match / n * 100,
                candidate_codes_match, n, candidate_codes_match / n * 100)
    logger.info("  JSON-Valid:           %d/%d (%.0f%%)           %d/%d (%.0f%%)",
                baseline_json_ok, n, baseline_json_ok / n * 100,
                candidate_json_ok, n, candidate_json_ok / n * 100)
    logger.info("  Avg Latenz:           %.0fms                  %.0fms", avg_lat_8b, avg_lat_3b)
    logger.info("  Speedup:              1.0x                    %.1fx", avg_lat_8b / max(avg_lat_3b, 1))

    # Go/No-Go
    code_match_rate = candidate_codes_match / max(n, 1) * 100
    json_error_rate = (1 - candidate_json_ok / max(n, 1)) * 100
    baseline_match_rate = baseline_codes_match / max(n, 1) * 100

    logger.info("\n>>> GO/NO-GO KRITERIEN <<<")
    logger.info("  Code-Match >= 80%%: %.0f%% → %s", code_match_rate,
                "GO" if code_match_rate >= 80 else "NO-GO")
    logger.info("  JSON-Fehler < 5%%:  %.0f%% → %s", json_error_rate,
                "GO" if json_error_rate < 5 else "NO-GO")
    logger.info("  vs Baseline:       %.0f%% vs %.0f%% → %s",
                code_match_rate, baseline_match_rate,
                "GO" if code_match_rate >= baseline_match_rate * 0.85 else "NO-GO")

    overall = (code_match_rate >= 80 and json_error_rate < 5
               and code_match_rate >= baseline_match_rate * 0.85)
    logger.info("\n>>> GESAMT: %s <<<", "GO — Kandidat ist geeignet" if overall else "NO-GO — Kandidat nicht ausreichend")

    # Report speichern
    report_path = Path(output_csv).with_suffix(".md")
    with open(report_path, "w", encoding="utf-8") as f:
        f.write(f"# Qwen Benchmark: {baseline} vs {candidate}\n\n")
        f.write(f"**Datum:** {time.strftime('%Y-%m-%d %H:%M')}\n")
        f.write(f"**Frames:** {n}\n\n")
        f.write(f"| Metrik | {baseline} | {candidate} |\n")
        f.write(f"|--------|-----------|------------|\n")
        f.write(f"| Code-Match | {baseline_codes_match}/{n} ({baseline_match_rate:.0f}%) | {candidate_codes_match}/{n} ({code_match_rate:.0f}%) |\n")
        f.write(f"| JSON-Valid | {baseline_json_ok}/{n} | {candidate_json_ok}/{n} |\n")
        f.write(f"| Avg Latenz | {avg_lat_8b:.0f}ms | {avg_lat_3b:.0f}ms |\n")
        f.write(f"| Speedup | 1.0x | {avg_lat_8b / max(avg_lat_3b, 1):.1f}x |\n\n")
        f.write(f"## Ergebnis: {'GO' if overall else 'NO-GO'}\n")
    logger.info("Report: %s", report_path)


def main():
    parser = argparse.ArgumentParser(description="Qwen 3B vs 8B Benchmark")
    parser.add_argument("--baseline", default="qwen3-vl:8b")
    parser.add_argument("--candidate", default="qwen3-vl:2b")
    parser.add_argument("--frames", default="benchmarks/frames")
    parser.add_argument("--output", default="benchmarks/benchmark_results.csv")
    args = parser.parse_args()

    run_benchmark(args.baseline, args.candidate, args.frames, args.output)


if __name__ == "__main__":
    main()
