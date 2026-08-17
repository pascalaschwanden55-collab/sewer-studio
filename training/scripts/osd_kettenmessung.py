"""Misst Vorlagenleser und diagnostische Kette auf denselben OSD-Goldsaetzen.

Die Kette ist fest: bestehender Leser zuerst, OSD-Modell nur bei ``None``.
Kandidaten-ID, Gewichts-SHA-256 und Schwelle kommen aus dem Sidecar-Wrapper
und sind dort nicht frei konfigurierbar. Diese Messung aktiviert nichts im
laufenden SewerStudio.
"""

from __future__ import annotations

import argparse
import base64
import hashlib
import json
import math
import sys
import time
from datetime import datetime, timezone
from pathlib import Path

from PIL import Image

import osd_goldmessung

_WURZEL = Path(__file__).resolve().parents[2]
_SIDECAR = _WURZEL / "sidecar"
if str(_SIDECAR) not in sys.path:
    sys.path.insert(0, str(_SIDECAR))

from sidecar import osd_meter, osd_modell  # noqa: E402
from sidecar.config import settings  # noqa: E402
from sidecar.models import bcc_test_wrapper, osd_model_wrapper, yolo_wrapper  # noqa: E402

GOLD_WURZEL = Path(r"C:\KI_BRAIN\eval_set\osd")
BERICHT_ORDNER = Path(r"C:\KI_BRAIN\training\reports")
SAETZE = osd_goldmessung.SAETZE + ("osd_mix_v1",)
# Diese vier Saetze haben Kandidatenwahl und Kettenentscheidung mitbestimmt und
# sind damit verbraucht. Eine Messung auf ihnen kann nie eine Produktfreigabe
# tragen, eine Messung auf ausschliesslich frischem Material dagegen schon.
VERBRAUCHTE_SAETZE = SAETZE


def freigabe_ableitbar(saetze_namen: tuple[str, ...]) -> bool:
    """Darf aus DIESEN Saetzen eine Produktfreigabe abgeleitet werden?

    Nur wenn kein einziger verbrauchter Satz dabei ist. Der Wert stand hier
    fest auf False und trug einen Hinweistext, der von "den vier Saetzen"
    sprach - bei einem Lauf ueber einen frischen Bestand war beides schlicht
    falsch, und der Beleg sagte die Unwahrheit ueber seine eigene Grundlage.
    """
    namen = tuple(saetze_namen)
    return bool(namen) and not set(namen) & set(VERBRAUCHTE_SAETZE)
BCC_KANDIDAT_ID = "bcc_nc15_seed46_20260808"
BCC_GEWICHT_SHA256 = "8ad82c1b0186ec02126a18f095d551d7a083faa90855350b22a6e893ac860114"
BCC_SCHWELLE = 0.5


def _sha256(pfad: Path) -> str:
    digest = hashlib.sha256()
    with pfad.open("rb") as stream:
        for block in iter(lambda: stream.read(1 << 20), b""):
            digest.update(block)
    return digest.hexdigest()


def _perzentil(werte: list[float], anteil: float) -> float | None:
    if not werte:
        return None
    sortiert = sorted(werte)
    position = (len(sortiert) - 1) * anteil
    unten = math.floor(position)
    oben = math.ceil(position)
    if unten == oben:
        return sortiert[unten]
    return sortiert[unten] + (sortiert[oben] - sortiert[unten]) * (position - unten)


def laufzeit_statistik(werte: list[float]) -> dict:
    """Millisekundenwerte kompakt und deterministisch zusammenfassen."""
    return {
        "anzahl": len(werte),
        "mittel_ms": round(sum(werte) / len(werte), 2) if werte else None,
        "median_ms": round(_perzentil(werte, 0.5), 2) if werte else None,
        "p95_ms": round(_perzentil(werte, 0.95), 2) if werte else None,
        "maximum_ms": round(max(werte), 2) if werte else None,
    }


def vergleiche_saetze(basis: dict, kette: dict) -> dict:
    """Zaehlt, was das Modell gegenueber demselben Basislauf beigetragen hat."""
    basis_faelle = {fall["datei"]: fall for fall in basis["faelle"]}
    neue_richtige = 0
    neue_falsche = 0
    for fall in kette["faelle"]:
        vorher = basis_faelle[fall["datei"]]
        if vorher["zustand"] != "nicht_gelesen":
            continue
        if fall["zustand"] == "richtig":
            neue_richtige += 1
        elif fall["zustand"] == "falsch":
            neue_falsche += 1
    return {
        "satz": basis["satz"],
        "basis": {key: basis[key] for key in ("bilder", "richtig", "falsch", "nicht_gelesen")},
        "kette": {key: kette[key] for key in ("bilder", "richtig", "falsch", "nicht_gelesen")},
        "neue_richtige": neue_richtige,
        "neue_falsche": neue_falsche,
    }


def _gpu_snapshot() -> dict | None:
    try:
        import torch
        if not torch.cuda.is_available():
            return None
        torch.cuda.synchronize()
        eigenschaften = torch.cuda.get_device_properties(0)
        return {
            "geraet": str(eigenschaften.name),
            "gesamt_mb": round(eigenschaften.total_memory / (1024**2), 1),
            "belegt_mb": round(torch.cuda.memory_allocated(0) / (1024**2), 1),
            "reserviert_mb": round(torch.cuda.memory_reserved(0) / (1024**2), 1),
            "spitze_belegt_mb": round(torch.cuda.max_memory_allocated(0) / (1024**2), 1),
            "spitze_reserviert_mb": round(torch.cuda.max_memory_reserved(0) / (1024**2), 1),
        }
    except Exception:
        return None


def _erstes_goldbild(gold_wurzel: Path, saetze: tuple[str, ...]) -> Path:
    for name in saetze:
        manifest = json.loads(
            (gold_wurzel / name / "manifest.json").read_text(encoding="utf-8-sig"))
        for eintrag in manifest.get("eintraege") or []:
            bild = gold_wurzel / name / "frames" / eintrag["datei"]
            if bild.is_file():
                return bild
    raise SystemExit("Kein Goldbild fuer die BCC-/VRAM-Messung gefunden.")


def _lade_bcc_fuer_vram(gold_wurzel: Path, saetze: tuple[str, ...]) -> dict:
    bild = _erstes_goldbild(gold_wurzel, saetze)
    antwort = bcc_test_wrapper.detect(
        base64.b64encode(bild.read_bytes()).decode("ascii"),
        BCC_SCHWELLE,
        candidate_id=BCC_KANDIDAT_ID,
        candidate_sha256=BCC_GEWICHT_SHA256,
    )
    if not antwort.available or not antwort.frame_usable:
        raise SystemExit("BCC-Kandidat konnte fuer die gemeinsame VRAM-Messung nicht laufen.")
    return {
        "kandidat_id": antwort.candidate_id,
        "gewicht_sha256": antwort.candidate_sha256,
        "inferenz_ms": antwort.inference_time_ms,
        "bild": bild.name,
    }


def _summiere(saetze: list[dict]) -> dict:
    return {
        key: sum(s[key] for s in saetze)
        for key in ("bilder", "richtig", "falsch", "nicht_gelesen")
    }


def main(argv=None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--gold-wurzel", type=Path, default=GOLD_WURZEL)
    parser.add_argument("--bericht-ordner", type=Path, default=BERICHT_ORDNER)
    parser.add_argument("--satz", action="append")
    parser.add_argument(
        "--mit-bcc-vram",
        action="store_true",
        help="Laedt vorher den angehefteten Bogen-Copilot-Kandidaten und misst beide Modelle gemeinsam.",
    )
    args = parser.parse_args(argv)
    saetze_namen = tuple(args.satz or SAETZE)

    kandidat = osd_model_wrapper.lade_kandidat()
    if (kandidat.candidate_id != osd_model_wrapper.KANDIDAT_ID
            or kandidat.weights_sha256 != osd_model_wrapper.GEWICHT_SHA256):
        raise SystemExit("Der geladene OSD-Kandidat weicht vom festen Pin ab.")

    templates = osd_meter.get_templates()
    basis_zeiten: list[float] = []
    ketten_zeiten: list[float] = []
    modell_zeiten: list[float] = []

    def basis_lesen(bild_pfad: Path) -> dict:
        start = time.perf_counter()
        with Image.open(bild_pfad) as roh:
            roh.load()
            ergebnis = osd_meter.lese_meter(roh.convert("RGB"), templates)
        basis_zeiten.append((time.perf_counter() - start) * 1000)
        return ergebnis

    def modell_lesen(bild: Image.Image, format: str | None) -> dict:
        start = time.perf_counter()
        ergebnis = osd_model_wrapper.lese(bild, format)
        modell_zeiten.append((time.perf_counter() - start) * 1000)
        return ergebnis

    def kette_lesen(bild_pfad: Path) -> dict:
        start = time.perf_counter()
        with Image.open(bild_pfad) as roh:
            roh.load()
            ergebnis = osd_meter.lese_meter(
                roh.convert("RGB"), templates, modell_leser=modell_lesen)
        ketten_zeiten.append((time.perf_counter() - start) * 1000)
        return ergebnis

    gpu_vorher = _gpu_snapshot()
    bcc = None
    if args.mit_bcc_vram:
        bcc = _lade_bcc_fuer_vram(args.gold_wurzel, saetze_namen)
    gpu_nach_bcc = _gpu_snapshot()
    try:
        import torch
        if torch.cuda.is_available():
            torch.cuda.reset_peak_memory_stats()
    except Exception:
        pass

    basis_saetze = []
    ketten_saetze = []
    vergleiche = []
    for name in saetze_namen:
        satz = args.gold_wurzel / name
        basis = osd_goldmessung.messe_satz(satz, basis_lesen)
        kette = osd_goldmessung.messe_satz(satz, kette_lesen)
        basis_saetze.append(basis)
        ketten_saetze.append(kette)
        vergleiche.append(vergleiche_saetze(basis, kette))

    gpu_nach_kette = _gpu_snapshot()
    basis_gesamt = _summiere(basis_saetze)
    ketten_gesamt = _summiere(ketten_saetze)
    beitrag = {
        "neue_richtige": sum(v["neue_richtige"] for v in vergleiche),
        "neue_falsche": sum(v["neue_falsche"] for v in vergleiche),
    }

    print(f"OSD-Kandidat: {kandidat.candidate_id}")
    print(f"Gewicht: {kandidat.weights_sha256}  Schwelle: {osd_model_wrapper.SCHWELLE}")
    print(f"{'Satz':<14}{'Leser richtig/falsch':>23}{'Kette richtig/falsch':>24}{'neu richtig/falsch':>20}")
    for vergleich in vergleiche:
        print(
            f"{vergleich['satz']:<14}"
            f"{vergleich['basis']['richtig']:>9}/{vergleich['basis']['falsch']:<13}"
            f"{vergleich['kette']['richtig']:>10}/{vergleich['kette']['falsch']:<13}"
            f"{vergleich['neue_richtige']:>9}/{vergleich['neue_falsche']}"
        )
    print(
        f"{'gesamt':<14}{basis_gesamt['richtig']:>9}/{basis_gesamt['falsch']:<13}"
        f"{ketten_gesamt['richtig']:>10}/{ketten_gesamt['falsch']:<13}"
        f"{beitrag['neue_richtige']:>9}/{beitrag['neue_falsche']}"
    )

    code_dateien = {
        "osd_meter.py": Path(osd_meter.__file__),
        "osd_modell.py": Path(osd_modell.__file__),
        "osd_model_wrapper.py": Path(osd_model_wrapper.__file__),
        "yolo_wrapper.py": Path(yolo_wrapper.__file__),
        "osd_kettenmessung.py": Path(__file__),
    }
    bericht = {
        "schema": "osd_kettenmessung_v1",
        "erstellt_utc": datetime.now(timezone.utc).isoformat(),
        "status": "diagnostic_not_deployed",
        "produktiver_schalter": bool(settings.osd_model_fallback_enabled),
        "freigabe_ableitbar": freigabe_ableitbar(saetze_namen),
        "freigabe_hinweis": (
            "Alle gemessenen Saetze sind frisch: Kandidatenwahl und "
            "Kettenentscheidung haben sie nicht mitbestimmt. Diese Messung darf "
            "eine Produktfreigabe tragen. Der Standardschalter bleibt trotzdem "
            "eine ausdrueckliche Entscheidung."
            if freigabe_ableitbar(saetze_namen) else
            "Mindestens ein gemessener Satz hat Kandidatenwahl oder "
            "Kettenentscheidung mitbestimmt und ist damit verbraucht. Eine "
            "Produktfreigabe braucht einen frischen, unberuehrten Bestand."
        ),
        "verbrauchte_saetze_im_lauf": sorted(
            set(saetze_namen) & set(VERBRAUCHTE_SAETZE)),
        "kandidat_id": kandidat.candidate_id,
        "gewicht_sha256": kandidat.weights_sha256,
        "schwelle": osd_model_wrapper.SCHWELLE,
        "code_sha256": {name: _sha256(pfad) for name, pfad in code_dateien.items()},
        "gemessene_saetze": list(saetze_namen),
        "basis_gesamt": basis_gesamt,
        "kette_gesamt": ketten_gesamt,
        "beitrag": beitrag,
        "vergleiche": vergleiche,
        "laufzeit": {
            "basis_je_bild": laufzeit_statistik(basis_zeiten),
            "kette_je_bild": laufzeit_statistik(ketten_zeiten),
            "modell_nur_bei_rueckfall": laufzeit_statistik(modell_zeiten),
            "modell_kaltstart_ms": round(modell_zeiten[0], 2) if modell_zeiten else None,
            "modell_warm": laufzeit_statistik(modell_zeiten[1:]),
        },
        "vram": {
            "bcc_vorgeladen": args.mit_bcc_vram,
            "bcc": bcc,
            "vorher": gpu_vorher,
            "nach_bcc": gpu_nach_bcc,
            "nach_kette": gpu_nach_kette,
            "zusaetzlich_belegt_mb": (
                round(gpu_nach_kette["belegt_mb"] - gpu_nach_bcc["belegt_mb"], 1)
                if gpu_nach_kette is not None and gpu_nach_bcc is not None else None
            ),
            "zusaetzlich_reserviert_mb": (
                round(gpu_nach_kette["reserviert_mb"] - gpu_nach_bcc["reserviert_mb"], 1)
                if gpu_nach_kette is not None and gpu_nach_bcc is not None else None
            ),
        },
        "basis_saetze": basis_saetze,
        "ketten_saetze": ketten_saetze,
    }

    args.bericht_ordner.mkdir(parents=True, exist_ok=True)
    zeit = datetime.now().strftime("%Y%m%d_%H%M%S")
    ziel = args.bericht_ordner / f"osd_kettenmessung_{kandidat.candidate_id}_{zeit}.json"
    text = json.dumps(bericht, indent=1, ensure_ascii=False)
    arbeit = ziel.with_suffix(".json.arbeit")
    arbeit.write_bytes(text.encode("utf-8"))
    arbeit.replace(ziel)
    print(f"Bericht: {ziel}")
    print(f"Bericht-SHA-256: {hashlib.sha256(text.encode('utf-8')).hexdigest()}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
