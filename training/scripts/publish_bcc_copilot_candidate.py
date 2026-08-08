"""Veroeffentlicht einen trainierten BCC-Kandidaten fuer den Sidecar-Testweg.

Der Kandidat wird ausschliesslich als `not_deployed` abgelegt. Er ersetzt keine
produktiven Gewichte und wird nicht aktiviert; der Sidecar nimmt ihn nur ueber
den gepinnten Testpfad `/detect/yolo/bcc-test` an.

Das Werkzeug prueft vor dem Schreiben alles, was der Wächter spaeter prueft, und
zusaetzlich die Klassenkarte des Modells selbst. Standard ist ein schreibfreier
Prueflauf; `--execute` schreibt atomar und ueberschreibt niemals einen
bestehenden Kandidaten.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import shutil
import sys
from datetime import datetime, timezone
from pathlib import Path
from typing import Sequence

KANDIDATEN_WURZEL = Path(r"C:\KI_BRAIN\training\models\candidates")
ID_MUSTER = re.compile(r"^[A-Za-z0-9][A-Za-z0-9_-]{0,127}$")
MIN_BILDER = 30

ERWARTETE_KLASSEN = {
    0: "BCA_anschluss",
    1: "BAB_riss",
    2: "BAC_bruch",
    3: "BAA_verformung",
    4: "BAF_oberflaeche",
    5: "BAH_schadanschluss",
    6: "BAI_dichtung",
    7: "BAJ_verbindung",
    8: "BBA_wurzeln",
    9: "BBB_anhaftung",
    10: "BBC_ablagerung",
    11: "BBD_boden",
    12: "BBF_infiltration",
    13: "SONST_schaden",
    14: "BCC_bogen",
}


def sha256(pfad: Path) -> str:
    hasher = hashlib.sha256()
    with pfad.open("rb") as datei:
        for block in iter(lambda: datei.read(1 << 20), b""):
            hasher.update(block)
    return hasher.hexdigest()


def ist_verknuepfung(pfad: Path) -> bool:
    try:
        return pfad.is_symlink() or bool(pfad.lstat().st_reparse_tag)  # type: ignore[attr-defined]
    except (OSError, AttributeError):
        return pfad.is_symlink()


def klassenkarte_pruefen(gewicht: Path) -> dict[int, str]:
    """Laedt das Modell und vergleicht seine Klassenkarte mit der freigegebenen."""
    from ultralytics import YOLO

    modell = YOLO(str(gewicht))
    namen = {int(schluessel): str(wert) for schluessel, wert in dict(modell.names).items()}
    if namen != ERWARTETE_KLASSEN:
        fehlend = {k: v for k, v in ERWARTETE_KLASSEN.items() if namen.get(k) != v}
        raise SystemExit(
            "Das Modell traegt nicht die freigegebene 15er-Klassenkarte. "
            f"Abweichungen: {fehlend}"
        )
    return namen


def zaehle_bilder(datensatz: Path) -> tuple[int, int, int, int]:
    train = len(list((datensatz / "images" / "train").glob("*"))) if datensatz.is_dir() else 0
    val = len(list((datensatz / "images" / "val").glob("*"))) if datensatz.is_dir() else 0
    instanzen = 0
    for split in ("train", "val"):
        ordner = datensatz / "labels" / split
        if not ordner.is_dir():
            continue
        for label in ordner.glob("*.txt"):
            instanzen += sum(1 for zeile in label.read_text(encoding="utf-8").splitlines() if zeile.strip())
    return train + val, train, val, instanzen


def ergebnisse_lesen(lauf: Path) -> dict[str, float]:
    bericht = lauf.parent / "kandidat.json"
    if not bericht.is_file():
        raise SystemExit(f"Kennzahlen fehlen: {bericht}")
    werte = json.loads(bericht.read_text(encoding="utf-8-sig")).get("interne_validation") or {}
    map50 = werte.get("mAP50")
    if not isinstance(map50, (int, float)) or not 0.0 <= float(map50) <= 1.0:
        raise SystemExit(f"Unbrauchbarer mAP50-Wert: {map50!r}")
    return {
        "metrics/precision(B)": float(werte.get("precision", 0.0)),
        "metrics/recall(B)": float(werte.get("recall", 0.0)),
        "metrics/mAP50(B)": float(map50),
        "metrics/mAP50-95(B)": float(werte.get("mAP50_95", 0.0)),
    }


def epochen_lesen(lauf: Path) -> int:
    csv = lauf / "results.csv"
    if not csv.is_file():
        raise SystemExit(f"results.csv fehlt: {csv}")
    zeilen = [z for z in csv.read_text(encoding="utf-8").splitlines() if z.strip()]
    if len(zeilen) < 2:
        raise SystemExit("results.csv enthaelt keine Epochen.")
    return len(zeilen) - 1


def main(argv: Sequence[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="BCC-Kandidat fuer den Sidecar-Testweg ablegen")
    parser.add_argument(
        "--lauf",
        type=Path,
        default=Path(r"C:\KI_BRAIN\training\diagnostics\bcc_nc15_20260807\runs\seed44\run"),
        help="Ultralytics-Laufordner mit weights/best.pt und results.csv",
    )
    parser.add_argument(
        "--datensatz",
        type=Path,
        default=Path(r"C:\KI_BRAIN\training\diagnostics\bcc_nc15_20260807\dataset"),
    )
    parser.add_argument("--id", default="bcc_nc15_seed44_20260808")
    parser.add_argument("--execute", action="store_true", help="Wirklich schreiben.")
    args = parser.parse_args(argv)

    if ID_MUSTER.fullmatch(args.id) is None:
        raise SystemExit(f"Die Kandidaten-ID passt nicht zum Muster des Sidecars: {args.id!r}")

    gewicht = args.lauf / "weights" / "best.pt"
    if not gewicht.is_file() or ist_verknuepfung(gewicht):
        raise SystemExit(f"Gewicht fehlt oder ist verknuepft: {gewicht}")

    ziel = KANDIDATEN_WURZEL / args.id
    if ziel.exists():
        raise SystemExit(f"Kandidat existiert bereits und wird nie ueberschrieben: {ziel}")

    bilder, train, val, instanzen = zaehle_bilder(args.datensatz)
    if bilder < MIN_BILDER:
        raise SystemExit(f"Der Sidecar verlangt mindestens {MIN_BILDER} Bilder, gefunden: {bilder}")

    ergebnisse = ergebnisse_lesen(args.lauf)
    epochen = epochen_lesen(args.lauf)
    gewicht_sha = sha256(gewicht)
    data_yaml = args.datensatz / "data.yaml"
    classes_txt = args.datensatz / "classes.txt"

    print("Kandidat vorbereiten")
    print(f"  ID            {args.id}")
    print(f"  Gewicht       {gewicht}")
    print(f"  SHA-256       {gewicht_sha}")
    print(f"  Bilder        {bilder} ({train} Train, {val} Validation), {instanzen} Boxen")
    print(f"  Epochen       {epochen}")
    print(f"  mAP50         {ergebnisse['metrics/mAP50(B)']:.4f}")

    print("  Klassenkarte  wird am Modell geprueft ...")
    klassenkarte_pruefen(gewicht)
    print("  Klassenkarte  entspricht der freigegebenen 15er-Karte")

    manifest = {
        "schema_version": "1.0",
        "candidate_status": "not_deployed",
        "pilot": "BCC_bogen",
        "created_utc": datetime.now(timezone.utc).isoformat(),
        "zweck": "Vorschlags-Assistent Bogen; keine Aktivierung, kein Standardmodell",
        "dataset": {
            "path": str(args.datensatz),
            "data_yaml_sha256": sha256(data_yaml) if data_yaml.is_file() else None,
            "classes_sha256": sha256(classes_txt) if classes_txt.is_file() else None,
            "images": bilder,
            "train_images": train,
            "validation_images": val,
            "instances": instanzen,
        },
        "training": {
            "epochs_completed": epochen,
            "image_size": 1280,
            "seed": 44,
            "results": ergebnisse,
        },
        "weights": {
            "candidate_path": str(ziel / "best.pt"),
            "candidate_sha256": gewicht_sha,
        },
    }

    if not args.execute:
        print("\nPrueflauf — nichts geschrieben. Mit --execute veroeffentlichen.")
        return 0

    staging = KANDIDATEN_WURZEL / f".{args.id}.staging"
    if staging.exists():
        shutil.rmtree(staging)
    staging.mkdir(parents=True)
    try:
        shutil.copy2(gewicht, staging / "best.pt")
        if sha256(staging / "best.pt") != gewicht_sha:
            raise SystemExit("Die Kopie des Gewichts stimmt nicht mit dem Original ueberein.")
        (staging / "candidate_manifest.json").write_text(
            json.dumps(manifest, indent=2, ensure_ascii=False), encoding="utf-8"
        )
        staging.rename(ziel)
    except Exception:
        shutil.rmtree(staging, ignore_errors=True)
        raise

    print(f"\nVeroeffentlicht: {ziel}")
    print("Status not_deployed — der Sidecar bietet ihn nur als gepinnten Testkandidaten an.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
