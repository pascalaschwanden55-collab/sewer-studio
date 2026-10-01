"""Baut eine blinde Clip-Pruefung fuer alle Vorschlaege der BCC-PDF-Messung.

Die Warteschlange zeigt weder Konfidenz noch PDF-Zuordnung. So entscheidet der
Mensch nur am Bewegungsbild, ob wirklich ein Bogen sichtbar ist. Kundenoriginale
werden ausschliesslich gelesen. Das Ziel wird erst nach vollstaendig erstellten
Clips atomar veroeffentlicht und ein vorhandenes Ziel nie ueberschrieben.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import shutil
import sys
import uuid
from pathlib import Path
from typing import Sequence

sys.path.insert(0, str(Path(__file__).resolve().parent))

from bcc_copilot_durchlauf import zusammenfassen  # noqa: E402
from bcc_video_fehlalarm_queue import _ffmpeg_suchen, clip_schneiden  # noqa: E402

MESSBESTAND = Path(r"C:\KI_BRAIN\training\diagnostics\bcc_pdf_auswahl\messbestand_v1.json")
LAUF = Path(r"C:\KI_BRAIN\training\diagnostics\bcc_pdf_recall_20260809")
ZIEL = Path(r"C:\KI_BRAIN\training\diagnostics\bcc_pdf_precision_queue_v1")


def sha256_datei(pfad: Path) -> str:
    hasher = hashlib.sha256()
    with pfad.open("rb") as datei:
        for block in iter(lambda: datei.read(1024 * 1024), b""):
            hasher.update(block)
    return hasher.hexdigest()


def messhaltungen_laden(messbestand: Path) -> set[str]:
    daten = json.loads(messbestand.read_text(encoding="utf-8-sig"))
    return {
        eintrag["haltung"]
        for gruppe in ("sd", "hd")
        for eintrag in daten[gruppe]["eintraege"]
        if eintrag["haelfte"] == "messung"
    }


def vorschlaege_laden(messbestand: Path, lauf: Path, schwelle: float,
                      stark_ab: float) -> tuple[list[dict], list[dict]]:
    """Rekonstruiert den gemessenen Arbeitspunkt aus den gespeicherten Einzelbildern."""
    erlaubt = messhaltungen_laden(messbestand)
    faelle: list[dict] = []
    belege: list[dict] = []
    for pfad in sorted((lauf / "haltungen").glob("*.json")):
        daten = json.loads(pfad.read_text(encoding="utf-8-sig"))
        if daten.get("haltung") not in erlaubt or daten.get("zustand") != "ausgewertet":
            continue
        video = str(daten.get("video") or "")
        vorschlaege = zusammenfassen(
            [dict(eintrag) for eintrag in daten.get("einzelbilder") or []],
            schwelle,
            stark_ab,
        )
        belege.append({"datei": str(pfad), "sha256": sha256_datei(pfad)})
        for vorschlag in vorschlaege:
            start = int(vorschlag["zeit_min"])
            ende = int(vorschlag["zeit_max"])
            peak = int(round(vorschlag.get("peak_zeit", start)))
            roh = f"{daten['haltung']}|{start}|{ende}|{peak}|{schwelle:.3f}|{stark_ab:.3f}"
            faelle.append({
                "fall_id": hashlib.sha256(roh.encode("utf-8")).hexdigest()[:16],
                "haltung": daten["haltung"],
                "video": video,
                "video_dauer_s": 0.0,
                "start_s": start,
                "ende_s": ende,
                "peak_s": peak,
            })
    return faelle, belege


def blind_mischen(faelle: list[dict], saat: str) -> list[dict]:
    return sorted(
        faelle,
        key=lambda fall: hashlib.sha256(f"{saat}|{fall['fall_id']}".encode()).hexdigest(),
    )


def oeffentlicher_fall(fall: dict, nummer: int, clip: str,
                       clip_sha256: str | None = None) -> dict:
    """Nur Angaben ausgeben, die das menschliche Urteil nicht vorwegnehmen."""
    ergebnis = {
        "nummer": nummer,
        "fall_id": fall["fall_id"],
        "haltung": fall["haltung"],
        "start_s": fall["start_s"],
        "ende_s": fall["ende_s"],
        "clip": clip,
    }
    if clip_sha256 is not None:
        ergebnis["clip_sha256"] = clip_sha256
    return ergebnis


def main(argv: Sequence[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--messbestand", type=Path, default=MESSBESTAND)
    parser.add_argument("--lauf", type=Path, default=LAUF)
    parser.add_argument("--ziel", type=Path, default=ZIEL)
    parser.add_argument("--ffmpeg", type=Path, default=None)
    parser.add_argument("--schwelle", type=float, default=0.40)
    parser.add_argument("--stark-ab", type=float, default=0.70)
    parser.add_argument("--saat", default="bcc-pdf-precision-v1")
    parser.add_argument("--stichprobe", type=int, default=0,
                        help="0 prueft alle Vorschlaege; sonst zufaellige Anzahl")
    args = parser.parse_args(argv)

    if not args.messbestand.is_file():
        raise SystemExit(f"Messbestand fehlt: {args.messbestand}")
    if not (args.lauf / "haltungen").is_dir():
        raise SystemExit(f"Haltungsergebnisse fehlen: {args.lauf / 'haltungen'}")
    if args.ziel.exists():
        raise SystemExit(f"Ziel existiert bereits und wird nicht ueberschrieben: {args.ziel}")
    if args.stichprobe < 0:
        raise SystemExit("--stichprobe darf nicht negativ sein.")

    faelle, belege = vorschlaege_laden(
        args.messbestand, args.lauf, args.schwelle, args.stark_ab)
    faelle = blind_mischen(faelle, args.saat)
    population = len(faelle)
    if args.stichprobe:
        faelle = faelle[:args.stichprobe]
    if not faelle:
        raise SystemExit("Keine Vorschlaege fuer die Pruefung gefunden.")

    ffmpeg = _ffmpeg_suchen(args.ffmpeg)
    staging = args.ziel.with_name(f".{args.ziel.name}.staging-{uuid.uuid4().hex}")
    clips = staging / "clips"
    clips.mkdir(parents=True)
    fertig: list[dict] = []
    try:
        for nummer, fall in enumerate(faelle, start=1):
            name = f"fall_{nummer:03d}_{fall['fall_id']}.mp4"
            if not clip_schneiden(ffmpeg, fall, clips / name):
                raise RuntimeError(
                    f"Clip fehlgeschlagen: {fall['haltung']} @ {fall['start_s']} s")
            fertig.append(oeffentlicher_fall(
                fall, nummer, name, sha256_datei(clips / name)))
            print(f"  [{nummer:>3}/{len(faelle)}] {fall['haltung']}")

        queue = {
            "schema_version": 2,
            "zweck": "Blinde Precision-Pruefung des BCC-Archiv-Arbeitspunkts",
            "messbestand": str(args.messbestand),
            "messbestand_sha256": sha256_datei(args.messbestand),
            "quelle_haltungen": belege,
            "schwelle": args.schwelle,
            "stark_ab": args.stark_ab,
            "saat": args.saat,
            "population_vorschlaege": population,
            "stichprobe": len(fertig),
            "voller_bestand": len(fertig) == population,
            "faelle": fertig,
        }
        fehlende_clips = [
            fall["clip"] for fall in fertig
            if not (clips / fall["clip"]).is_file()
            or (clips / fall["clip"]).stat().st_size <= 0
        ]
        if fehlende_clips:
            raise RuntimeError(
                f"{len(fehlende_clips)} erzeugte Clips fehlen vor der Veroeffentlichung.")
        (staging / "queue.json").write_text(
            json.dumps(queue, indent=2, ensure_ascii=False), encoding="utf-8")
        staging.replace(args.ziel)
    except Exception:
        shutil.rmtree(staging, ignore_errors=True)
        raise

    print(f"\n{len(fertig)} von {population} Vorschlaegen vorbereitet.")
    print(f"Warteschlange: {args.ziel / 'queue.json'}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
