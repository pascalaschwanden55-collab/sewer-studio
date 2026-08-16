"""Trainiert den OSD-Zeichen-Detektor. Schreibt NUR einen Kandidaten.

Sperren wie bei train_bcc_pilot.py: kein Lauf bei erreichbarem Sidecar (er haelt
GPU-Speicher), kein Lauf unter 8000 MB freiem VRAM. Produktive Gewichte oder
Modellzeiger werden nie angefasst. Der Kandidat startet als
diagnostic_not_deployed und laeuft erst nach ausdruecklicher Freigabe mit.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import shutil
import subprocess
import sys
import urllib.request
from pathlib import Path

WURZEL = Path(__file__).resolve().parents[2]
if str(WURZEL / "sidecar") not in sys.path:
    sys.path.insert(0, str(WURZEL / "sidecar"))

from sidecar import osd_meter

KANDIDATEN = Path(r"C:\KI_BRAIN\training\models\candidates")
MIN_FREIER_VRAM_MB = 8000


def sha256(pfad: Path) -> str:
    h = hashlib.sha256()
    with pfad.open("rb") as f:
        for block in iter(lambda: f.read(1 << 20), b""):
            h.update(block)
    return h.hexdigest()


def schreibe_laufzeit_yaml(datensatz: Path) -> Path:
    """Laufzeit-YAML mit ABSOLUTEM Pfad neben der data.yaml des Datensatzes.

    Die data.yaml selbst traegt "path: ." und bleibt unveraendert - sie ist im
    Datensatzbeleg gehasht. Ultralytics wuerde ein relatives "path:" gegen das
    Arbeitsverzeichnis aufloesen und die Bilder nicht finden.
    """
    quelle = datensatz / "data.yaml"
    zeilen = []
    for zeile in quelle.read_text(encoding="utf-8").splitlines():
        if zeile.startswith("path:"):
            zeilen.append(f"path: {datensatz.resolve().as_posix()}")
        else:
            zeilen.append(zeile)
    ziel = datensatz / "data.runtime.yaml"
    ziel.write_text("\n".join(zeilen) + "\n", encoding="utf-8")
    return ziel


def entferne_label_caches(datensatz: Path) -> None:
    """Von Ultralytics erzeugte Cachedateien wieder wegraeumen.

    Der Datensatz ist per Beleg gehasht und soll nach dem Lauf so dastehen wie
    davor. Unsichere Pfade und Verknuepfungen werden nicht angefasst.
    """
    labels = (datensatz / "labels").resolve()
    for name in ("train.cache", "val.cache"):
        pfad = (labels / name).resolve()
        if pfad.parent != labels or pfad.is_symlink():
            continue
        if pfad.is_file():
            pfad.unlink()


def sidecar_laeuft() -> bool:
    """True, wenn unter localhost:8100 ein Sidecar antwortet.

    Bewusst eine reine, modulweite Funktion (Ruling zu Aufgabe 5): main()
    ruft sie einfach auf, ein Test kann sie per monkeypatch ersetzen, ohne
    einen echten Sidecar zu starten oder eine GPU zu brauchen.
    """
    try:
        with urllib.request.urlopen("http://127.0.0.1:8100/health", timeout=2):
            return True
    except Exception:
        return False


def freier_vram_mb() -> int | None:
    """Freier GPU-Speicher in MB, oder None wenn er nicht messbar ist.

    None bedeutet "unbekannt", NICHT "zu wenig": Fehlt nvidia-smi (z.B. keine
    NVIDIA-GPU/Treiber) oder schlaegt der Aufruf fehl, darf das den Lauf hier
    nicht blockieren - main() prueft deshalb ausdruecklich nur den Fall
    "gemessen UND zu niedrig", nie den Fall "nicht gemessen".
    """
    try:
        ergebnis = subprocess.run(
            ["nvidia-smi", "--query-gpu=memory.free", "--format=csv,noheader,nounits"],
            capture_output=True, text=True, timeout=10, check=True)
        return int(ergebnis.stdout.strip().splitlines()[0])
    except Exception:
        return None


def baue_manifest(
    kandidat_id: str,
    gewicht_pfad: Path,
    basis_pfad: Path,
    imgsz: int,
    datensatz: Path,
    datensatz_beleg_pfad: Path,
) -> dict:
    """Baut den Kandidaten-Manifest-Inhalt - reine Funktion, kein Training.

    Ruling zu Aufgabe 5 + Fix-Runde 1: main() und ein Test rufen exakt diese
    eine Funktion auf, damit die Manifestform nur an einer Stelle entsteht.
    Gewicht-, Basis- UND Beleg-Hash werden HIER aus den tatsaechlichen Bytes
    der jeweiligen Datei berechnet (nie vom Aufrufer als fertiger Wert
    uebernommen) - Manifest und Dateien koennen dadurch nie auseinanderlaufen.

    Fix-Runde 1: datensatz_receipt_sha256 bindet datensatz.json statt
    data.yaml. schreibe_data_yaml() (osd_datensatz.py) erzeugt data.yaml
    ausschliesslich aus der festen Konstante osd_meter.ZEICHEN und zwei
    festen relativen Pfaden - die Datei ist fuer JEDEN je erzeugten
    Datensatz bytegleich und ein Hash darauf waere ein Scheinnachweis.
    datensatz.json dagegen traegt echte Quellinformation (SHA-256 je
    eintraege.json-Quelle, Split-Zahlen). Ebenso basis_sha256: Ein blosser
    Dateiname wuerde Ultralytics erlauben, notfalls aus dem Netz
    nachzuladen; hier wird ausschliesslich die tatsaechlich aufgeloeste,
    lokal vorhandene Datei gebunden.

    status bleibt immer diagnostic_not_deployed und schwelle immer None:
    Das setzt erst osd_schwelle_kalibrieren.py (Aufgabe 7); die Goldmessung
    (Aufgabe 8) verweigert den Lauf, solange schwelle None ist.
    """
    return {
        "schema": "osd_zeichen_kandidat_v1",
        "kandidat_id": kandidat_id,
        "status": "diagnostic_not_deployed",
        "gewicht_datei": "weights/best.pt",
        "gewicht_sha256": sha256(gewicht_pfad),
        "basis_pfad": str(basis_pfad),
        "basis_sha256": sha256(basis_pfad),
        "klassen": list(osd_meter.ZEICHEN),
        "imgsz": imgsz,
        "datensatz": str(datensatz),
        "datensatz_receipt_sha256": sha256(datensatz_beleg_pfad),
        # Wird erst von osd_schwelle_kalibrieren.py gesetzt. Solange None,
        # verweigert die Goldmessung den Lauf.
        "schwelle": None,
    }


def main(argv=None) -> int:
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("--datensatz", type=Path, required=True)
    p.add_argument("--epochen", type=int, default=60)
    p.add_argument("--imgsz", type=int, default=320)
    p.add_argument("--batch", type=int, default=16)
    # Vorgabe ist der absolute Pfad im Repo-Wurzelverzeichnis (WURZEL), nicht
    # ein blosser Dateiname und nicht relativ zum Arbeitsverzeichnis - siehe
    # Basisgewicht-Aufloesung weiter unten.
    p.add_argument("--basis", type=Path, default=WURZEL / "yolo26n.pt")
    args = p.parse_args(argv)

    if sidecar_laeuft():
        print("ABBRUCH: Der Sidecar laeuft und haelt GPU-Speicher. Erst beenden.",
              file=sys.stderr)
        return 2

    # Unbekannter VRAM (nvidia-smi fehlt oder schlaegt fehl) ist NICHT
    # dasselbe wie zu wenig VRAM: freier_vram_mb() liefert dann None, und ein
    # unbekannter Wert blockiert den Lauf hier bewusst nicht.
    frei = freier_vram_mb()
    if frei is not None and frei < MIN_FREIER_VRAM_MB:
        print(f"ABBRUCH: Nur {frei} MB VRAM frei, noetig sind {MIN_FREIER_VRAM_MB}.",
              file=sys.stderr)
        return 2

    yaml_pfad = args.datensatz / "data.yaml"
    if not yaml_pfad.is_file():
        print(f"ABBRUCH: data.yaml fehlt unter {args.datensatz}", file=sys.stderr)
        return 2

    # data.yaml allein bindet nichts (siehe baue_manifest-Docstring): Der
    # einzige Beleg, der die tatsaechlichen Quellen bindet, ist
    # datensatz.json. Ohne ihn laesst sich der Datensatz nicht nachweisen.
    beleg_pfad = args.datensatz / "datensatz.json"
    if not beleg_pfad.is_file():
        print(f"ABBRUCH: datensatz.json fehlt unter {args.datensatz}", file=sys.stderr)
        return 2

    # Ultralytics wuerde einen blossen Dateinamen notfalls aus dem Netz
    # nachladen - das ist hier kein zulaessiger Weg. Erst auf einen
    # absoluten Pfad aufloesen und die Existenz pruefen; erst danach binden
    # wir die tatsaechlichen Bytes ins Manifest (baue_manifest).
    basis_pfad = args.basis.resolve()
    if not basis_pfad.is_file():
        print(f"ABBRUCH: Basisgewicht fehlt: {basis_pfad}", file=sys.stderr)
        return 2

    # Ultralytics loest ein relatives "path:" gegen das ARBEITSVERZEICHNIS auf,
    # nicht gegen den Ort der data.yaml. Die data.yaml des Datensatzes traegt
    # bewusst "path: ." (Hausform, siehe osd_datensatz.schreibe_data_yaml) und
    # wuerde deshalb im Repo-Ordner nach images/val suchen. Darum bekommt der
    # Trainingslauf eine eigene Laufzeit-YAML mit absolutem Pfad, genau wie
    # train_bcc_pilot.py:349-361 (_write_runtime_yaml). Die data.yaml des
    # Datensatzes bleibt unangetastet - sie ist im Beleg gehasht.
    laufzeit_yaml = schreibe_laufzeit_yaml(args.datensatz)

    from ultralytics import YOLO

    modell = YOLO(str(basis_pfad))
    ergebnis = modell.train(
        data=str(laufzeit_yaml),
        epochs=args.epochen,
        imgsz=args.imgsz,
        batch=args.batch,
        # Uhrlage und Leserichtung sind fest: Ein gespiegeltes "9" waere eine "P".
        flipud=0.0,
        fliplr=0.0,
        degrees=0.0,
        # Die Anzeige variiert in Helligkeit und Farbe, nicht in der Form.
        hsv_h=0.02, hsv_s=0.4, hsv_v=0.5,
        patience=15,
        # Reproduzierbarkeit (Fix-Runde 1, wie train_bcc_pilot.py:445-446):
        # Aufgabe 7/8 bauen ihre Messung auf genau diesem Kandidaten auf.
        seed=42,
        deterministic=True,
        project=str(KANDIDATEN),
        name="osd_zeichen_lauf",
        exist_ok=False,
    )

    # Ultralytics legt neben den Labels train.cache/val.cache an. Der Datensatz
    # ist per Beleg gehasht und soll nach dem Lauf unveraendert dastehen -
    # dieselbe Aufraeumung wie train_bcc_pilot.py:364-373.
    entferne_label_caches(args.datensatz)

    quelle = Path(ergebnis.save_dir) / "weights" / "best.pt"
    if not quelle.is_file():
        print("ABBRUCH: Kein best.pt erzeugt.", file=sys.stderr)
        return 1

    gewicht_hash = sha256(quelle)
    kandidat_id = f"osd_zeichen_{gewicht_hash[:12]}"
    ziel = KANDIDATEN / kandidat_id
    if ziel.exists():
        print(f"ABBRUCH: Kandidat besteht bereits: {ziel}", file=sys.stderr)
        return 1
    shutil.copytree(Path(ergebnis.save_dir), ziel)

    manifest = baue_manifest(
        kandidat_id=kandidat_id,
        gewicht_pfad=ziel / "weights" / "best.pt",
        basis_pfad=basis_pfad,
        imgsz=args.imgsz,
        datensatz=args.datensatz,
        datensatz_beleg_pfad=beleg_pfad,
    )
    (ziel / "manifest.json").write_text(
        json.dumps(manifest, indent=2, ensure_ascii=False), encoding="utf-8")

    print(f"Kandidat: {ziel}")
    print(f"Gewicht-SHA-256: {manifest['gewicht_sha256']}")
    print("Status: diagnostic_not_deployed - Schwelle fehlt noch.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
