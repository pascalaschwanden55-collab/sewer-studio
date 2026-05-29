#!/usr/bin/env python3
"""Probe: globaler vs. lokaler pxToMm aus Ground-Truth-Frames.

REINE ANALYSE — kein Produktionscode, keine KI-Pipeline, kein Aspect-Fix.
Vergleicht den heutigen GLOBALEN pxToMm (DN / (image_width * 0.70), so wie
MaskQuantificationService rechnet) gegen den LOKALEN pxToMm
(DN / (pipe_right_x - pipe_left_x), aus vorab gemessenen Rohrkanten).

So sieht man schwarz auf weiss, ob der lokale Faktor wirklich naeher an der
Realitaet liegt — BEVOR irgendetwas am Messmodell geaendert wird.

Aufruf:
    python probe.py [pfad/zu/manifest.json]
Default: ../GroundTruthFrames/pipe-scale/manifest.json
Format des Manifests: siehe sample-manifest.json
"""
from __future__ import annotations

import json
import sys
from pathlib import Path

# Aktuelle globale Annahme im MaskQuantificationService (PipeImageWidthRatio).
GLOBAL_WIDTH_RATIO = 0.70


def compute(frame: dict) -> dict:
    """Berechnet globalen vs. lokalen pxToMm fuer einen Frame."""
    dn = float(frame["dn_mm"])
    width = float(frame["image_width"])
    left = float(frame["pipe_left_x"])
    right = float(frame["pipe_right_x"])

    global_px_d = width * GLOBAL_WIDTH_RATIO
    local_px_d = right - left
    if global_px_d <= 0 or local_px_d <= 0:
        raise ValueError(f"ungueltiger Pixel-Durchmesser in {frame.get('file')}")

    global_pxmm = dn / global_px_d
    local_pxmm = dn / local_px_d
    diff_pct = (local_pxmm - global_pxmm) / global_pxmm * 100.0

    row = {
        "file": frame.get("file", "?"),
        "image_width": int(width),
        "dn_mm": int(dn),
        "global_px_d": round(global_px_d, 1),
        "local_px_d": round(local_px_d, 1),
        "global_pxmm": round(global_pxmm, 4),
        "local_pxmm": round(local_pxmm, 4),
        "diff_pct": round(diff_pct, 1),
    }

    hpx = frame.get("damage_height_px")
    if hpx:
        g_mm = hpx * global_pxmm
        l_mm = hpx * local_pxmm
        row["damage_global_mm"] = round(g_mm, 1)
        row["damage_local_mm"] = round(l_mm, 1)
        known = frame.get("damage_known_mm")
        if known:
            row["known_mm"] = float(known)
            row["global_err_mm"] = round(g_mm - known, 1)
            row["local_err_mm"] = round(l_mm - known, 1)
    return row


def to_markdown(rows: list[dict]) -> str:
    head = (
        "| Frame | DN | imgW | global px-d | local px-d | global px->mm | local px->mm | diff% "
        "| Schaden global mm | Schaden local mm | bekannt mm | Fehler global | Fehler local |"
    )
    sep = "|" + "---|" * 13
    lines = [head, sep]
    for r in rows:
        lines.append(
            f"| {r['file']} | {r['dn_mm']} | {r['image_width']} | {r['global_px_d']} "
            f"| {r['local_px_d']} | {r['global_pxmm']} | {r['local_pxmm']} | {r['diff_pct']} "
            f"| {r.get('damage_global_mm', '')} | {r.get('damage_local_mm', '')} "
            f"| {r.get('known_mm', '')} | {r.get('global_err_mm', '')} | {r.get('local_err_mm', '')} |"
        )
    return "\n".join(lines)


def main(argv: list[str]) -> int:
    default = Path(__file__).parent.parent / "GroundTruthFrames" / "pipe-scale" / "manifest.json"
    path = Path(argv[1]) if len(argv) > 1 else default
    if not path.exists():
        print(f"manifest nicht gefunden: {path}", file=sys.stderr)
        print("Format siehe sample-manifest.json", file=sys.stderr)
        return 2

    data = json.loads(path.read_text(encoding="utf-8"))
    rows = [compute(f) for f in data.get("frames", [])]
    if not rows:
        print("keine frames im manifest", file=sys.stderr)
        return 1

    print(to_markdown(rows))
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv))
