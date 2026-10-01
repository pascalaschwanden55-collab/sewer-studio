#!/usr/bin/env python3
"""Kandidatenliste je Detektklasse aus XTF- und WinCan-Quellen (schreibfrei).

Liest die XTF-Dateien (beide Modellvarianten: VSA_KEK_2020_LV95 und das aeltere
VSA_KEK — gematcht wird modellunabhaengig ueber das lokale Elementsegment) und
die WinCan-Projektdatenbanken (``.db3``, SQLite) unter einem Projektestamm.
Ein einziger Scan sammelt alle Befunde mit Fotoverweis; die Auswertung erfolgt
je Klasse (Standard: alle 14 Detektklassen).

Filter: bestehendes Gold (CaseId, beide Richtungen), echte Schutzquellen
(Holdouts, Eval-Sets, Negativsaetze, Gold-Audit-Testrollen — NICHT die
Diagnose-Warteschlangen), Byte-Dubletten. Maximal ``--max-per-holding``
Bilder je physischer Haltung und Klasse. Leitungsinspektionen werden
markiert, nicht verworfen. Es wird nichts kopiert oder veraendert.
"""

from __future__ import annotations

import argparse
import json
import os
import re
import sqlite3
import xml.etree.ElementTree as ET
from collections import Counter, defaultdict
from pathlib import Path

from PIL import Image

from repair_gold_holding_ids import _is_link_or_reparse, _sha256_file
from repair_pdf_gold_holding_ids import comparison_key, load_protection_keys

IMAGE_SUFFIXES = {".jpg", ".jpeg", ".png", ".bmp", ".tif", ".tiff"}
SKIP_DB_DIR_PARTS = ("/misc/backup/", "/backup/", "/misc/", "/_alt/")
ALL_CLASSES = ["BCA", "BAB", "BAC", "BAA", "BAF", "BAH", "BAI", "BAJ",
               "BBA", "BBB", "BBC", "BBD", "BBF", "BCC"]


def _localname(tag: str) -> str:
    # "{ns}VSA_KEK_2020_LV95.KEK.Kanalschaden" -> "Kanalschaden"
    return tag.rsplit("}", 1)[-1].rsplit(".", 1)[-1]


def _text(element, name: str, default: str = "") -> str:
    for child in element:
        if _localname(child.tag) == name:
            return (child.text or "").strip()
    return default


def _ref(element, name: str, default: str = "") -> str:
    # INTERLIS-Referenzen stehen im REF-Attribut, nicht im Text.
    for child in element:
        if _localname(child.tag) == name:
            return (child.get("REF") or (child.text or "")).strip()
    return default


# ---------------------------------------------------------------------------
# XTF (beide Modellvarianten)
# ---------------------------------------------------------------------------

def parse_xtf(path: Path) -> tuple[list[dict], Counter]:
    """Alle Befunde mit Fotoverweis + Zaehler je Klassenpraeffix."""
    try:
        tree = ET.parse(path)
    except ET.ParseError:
        return [], Counter()
    untersuchungen: dict[str, tuple[str, str]] = {}
    schaeden: dict[str, dict] = {}
    dateien: list[dict] = []
    for element in tree.iter():
        name = _localname(element.tag)
        if name == "Untersuchung":
            untersuchungen[element.get("TID", "")] = (
                _text(element, "vonPunktBezeichnung"),
                _text(element, "bisPunktBezeichnung"),
            )
        elif name == "Kanalschaden":
            schaeden[element.get("TID", "")] = {
                "code": _text(element, "KanalSchadencode"),
                "distanz": _text(element, "Distanz"),
                "lage_anfang": _text(element, "SchadenlageAnfang"),
                "lage_ende": _text(element, "SchadenlageEnde"),
                "videozaehler": _text(element, "Videozaehlerstand"),
                "untersuchung_ref": _ref(element, "UntersuchungRef"),
                "ref": element.get("TID", ""),
            }
        elif name == "Datei":
            dateien.append({
                "bezeichnung": _text(element, "Bezeichnung"),
                "objekt_ref": _ref(element, "Objekt"),
            })

    totals: Counter = Counter()
    for schaden in schaeden.values():
        if schaden["code"]:
            totals[schaden["code"][:3].upper()] += 1

    ausgabe = []
    for datei in dateien:
        schaden = schaeden.get(datei["objekt_ref"])
        if schaden is None or not schaden["code"] or not datei["bezeichnung"]:
            continue
        von, bis = untersuchungen.get(schaden["untersuchung_ref"], ("", ""))
        ausgabe.append({
            "quelle": "xtf",
            "haltung_von": von,
            "haltung_bis": bis,
            "code": schaden["code"],
            "meter": schaden["distanz"],
            "uhrlage": f"{schaden['lage_anfang']}-{schaden['lage_ende']}",
            "videozaehler": schaden["videozaehler"],
            "datei_name": datei["bezeichnung"],
            "quell_datei": str(path),
        })
    return ausgabe, totals


# ---------------------------------------------------------------------------
# WinCan db3
# ---------------------------------------------------------------------------

def parse_db3(path: Path) -> tuple[list[dict], Counter]:
    try:
        connection = sqlite3.connect(f"file:{path.as_posix()}?mode=ro", uri=True)
    except sqlite3.Error:
        return [], Counter()
    try:
        tables = {row[0].upper() for row in connection.execute(
            "SELECT name FROM sqlite_master WHERE type='table'")}
        if not {"SECOBS", "SECOBSMM", "SECINSP", "SECTION"} <= tables:
            return [], Counter()
        rows = connection.execute(
            """
            SELECT o.OBS_OpCode, o.OBS_Distance, o.OBS_ClockPos1, o.OBS_ClockPos2,
                   o.OBS_VideoCtr, o.OBS_Observation,
                   m.OMM_FilePath, m.OMM_FileName, sec.OBJ_Key
            FROM SECOBS o
            JOIN SECOBSMM m ON m.OMM_Observation_FK = o.OBS_PK
            JOIN SECINSP i ON o.OBS_Inspection_FK = i.INS_PK
            JOIN SECTION sec ON i.INS_Section_FK = sec.OBJ_PK
            """).fetchall()
        totals: Counter = Counter()
        for (code,) in connection.execute(
                "SELECT OBS_OpCode FROM SECOBS WHERE OBS_OpCode IS NOT NULL"):
            if code:
                totals[str(code)[:3].upper()] += 1
    except sqlite3.Error:
        return [], Counter()
    finally:
        connection.close()

    ausgabe = []
    for code, distanz, clock1, clock2, videoctr, bemerkung, filepath, filename, obj_key in rows:
        if not filename or not code:
            continue
        ausgabe.append({
            "quelle": "db3",
            "haltung_von": str(obj_key or ""),
            "haltung_bis": "",
            "code": str(code),
            "meter": str(distanz or ""),
            "uhrlage": f"{clock1 or ''}-{clock2 or ''}",
            "videozaehler": str(videoctr or ""),
            "bemerkung": str(bemerkung or "")[:120],
            "datei_name": str(filename),
            "quell_datei": str(path),
        })
    return ausgabe, totals


# ---------------------------------------------------------------------------
# Aufloesung / Pruefung
# ---------------------------------------------------------------------------

def build_image_index(root: Path) -> dict[str, list[Path]]:
    index: dict[str, list[Path]] = {}
    for dirpath, dirnames, filenames in os.walk(root):
        dirnames[:] = [d for d in dirnames if not _is_link_or_reparse(Path(dirpath) / d)]
        for name in filenames:
            if Path(name).suffix.casefold() in IMAGE_SUFFIXES:
                index.setdefault(name.casefold(), []).append(Path(dirpath) / name)
    return index


def resolve_image(name: str, index: dict[str, list[Path]]) -> Path | None:
    treffer = index.get(Path(name).name.casefold(), [])
    return treffer[0] if treffer else None


def decodable_size(path: Path) -> tuple[int, int] | None:
    try:
        with Image.open(path) as img:
            img.load()
            return img.size
    except Exception:
        return None


def holding_label(eintrag: dict) -> str:
    von = eintrag.get("haltung_von") or ""
    bis = eintrag.get("haltung_bis") or ""
    return f"{von}-{bis}" if bis else von


def is_line_inspection(haltung: str, eintrag: dict) -> bool:
    text = (f"{haltung} {eintrag.get('bemerkung', '')} {eintrag.get('quell_datei', '')} "
            f"{eintrag.get('datei_name', '')}")
    return bool(re.search(r"(^|[/\\_\-\s])L[_\-]", text)) or "DN160" in text.upper().replace(" ", "")


def collect_sources(root: Path) -> tuple[list[dict], Counter, Counter, int, int]:
    """Ein einziger Scan: XTF-Dateien und datenfuehrende db3 einlesen."""
    xtf_paths: dict[str, Path] = {}
    for dirpath, _dirs, filenames in os.walk(root):
        for name in filenames:
            if name.casefold().endswith(".xtf"):
                pfad = Path(dirpath) / name
                xtf_paths.setdefault(_sha256_file(pfad), pfad)

    db3_paths: list[Path] = []
    seen_db: set[tuple[str, int]] = set()
    for dirpath, _dirs, filenames in os.walk(root):
        for name in filenames:
            if not name.casefold().endswith(".db3") or name.casefold().endswith("_meta.db3"):
                continue
            pfad = Path(dirpath) / name
            norm = pfad.as_posix().casefold()
            if any(part in norm for part in SKIP_DB_DIR_PARTS):
                continue
            marker = (name.casefold(), pfad.stat().st_size)
            if marker in seen_db:
                continue
            seen_db.add(marker)
            db3_paths.append(pfad)

    befunde: list[dict] = []
    totals_xtf: Counter = Counter()
    totals_db3: Counter = Counter()
    for pfad in sorted(xtf_paths.values()):
        teil, zaehler = parse_xtf(pfad)
        befunde.extend(teil)
        totals_xtf.update(zaehler)
    for pfad in sorted(db3_paths):
        teil, zaehler = parse_db3(pfad)
        befunde.extend(teil)
        totals_db3.update(zaehler)
    return befunde, totals_xtf, totals_db3, len(xtf_paths), len(db3_paths)


def evaluate_class(
    class_prefix: str,
    befunde: list[dict],
    totals_xtf: Counter,
    totals_db3: Counter,
    image_index: dict[str, list[Path]],
    gold_keys: set,
    protection: dict,
    max_per_holding: int,
) -> dict:
    klassen_befunde = [b for b in befunde if b["code"][:3].upper() == class_prefix]
    aufloesungen: Counter = Counter()
    kandidaten: list[dict] = []
    stats: Counter = Counter()
    seen_bytes: set[str] = set()
    per_holding: dict[str, int] = defaultdict(int)

    for eintrag in klassen_befunde:
        bild = resolve_image(eintrag["datei_name"], image_index)
        if bild is None:
            stats["bild_fehlt"] += 1
            continue
        size = decodable_size(bild)
        if size is None:
            stats["nicht_dekodierbar"] += 1
            continue
        stats["dekodierbar"] += 1
        aufloesungen[f"{size[0]}x{size[1]}"] += 1
        byte_hash = _sha256_file(bild)
        if byte_hash in seen_bytes:
            stats["byte_dublette"] += 1
            continue
        seen_bytes.add(byte_hash)

        haltung = holding_label(eintrag)
        schluessel = comparison_key(haltung)
        if schluessel in gold_keys:
            stats["in_gold"] += 1
            continue
        if schluessel in protection:
            stats["geschuetzt"] += 1
            continue
        if per_holding[schluessel or haltung] >= max_per_holding:
            stats["ueber_limit_haltung"] += 1
            continue
        per_holding[schluessel or haltung] += 1
        kandidaten.append({
            "bildpfad": str(bild),
            "bild_sha256": byte_hash,
            "code": eintrag["code"],
            "meter": eintrag["meter"],
            "uhrlage": eintrag["uhrlage"],
            "haltung": haltung,
            "quelle": eintrag["quelle"],
            "videozaehler": eintrag.get("videozaehler") or "",
            "leitungsinspektion": is_line_inspection(haltung, eintrag),
            "quell_datei": eintrag["quell_datei"],
        })

    haltungen_mit_foto = {
        comparison_key(holding_label(b)) or holding_label(b) for b in klassen_befunde}
    haltungen_final = {comparison_key(k["haltung"]) or k["haltung"] for k in kandidaten}
    return {
        "befunde_gesamt": int(totals_xtf.get(class_prefix, 0) + totals_db3.get(class_prefix, 0)),
        "befunde_xtf": int(totals_xtf.get(class_prefix, 0)),
        "befunde_db3": int(totals_db3.get(class_prefix, 0)),
        "mit_fotoverweis": len(klassen_befunde),
        "haltungen_mit_foto": len(haltungen_mit_foto),
        "dekodierbar": stats["dekodierbar"],
        "filter": dict(stats),
        "haltungen_final": len(haltungen_final),
        "kandidaten_anzahl": len(kandidaten),
        "leitungsinspektionen": sum(1 for k in kandidaten if k["leitungsinspektion"]),
        "aufloesungen": dict(aufloesungen.most_common(8)),
        "kandidaten": kandidaten,
    }


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--classes", default=",".join(ALL_CLASSES),
                        help="Kommaliste der Klassenpraeffixe (Standard: alle 14)")
    parser.add_argument("--projects-root", type=Path, default=Path(r"D:\Videoprojekte"))
    parser.add_argument("--knowledge-root", type=Path, default=Path(r"C:\KI_BRAIN"))
    parser.add_argument("--max-per-holding", type=int, default=2)
    parser.add_argument("--out", type=Path, default=Path(
        r"C:\Sewer-Studio_KI_4.5\artifacts\klassen-messung-20260804\messung.json"))
    args = parser.parse_args(argv)

    classes = [c.strip().upper() for c in args.classes.split(",") if c.strip()]
    print(f"Klassen: {', '.join(classes)}")
    print("Scan laeuft (XTF + db3, einmalig) ...")
    befunde, totals_xtf, totals_db3, n_xtf, n_db3 = collect_sources(args.projects_root)
    print(f"Quellen: {n_xtf} verschiedene XTF, {n_db3} datenfuehrende db3")
    print(f"Befunde mit Fotoverweis gesamt: {len(befunde)}")

    print("Baue Bild-Index auf (kann dauern) ...")
    image_index = build_image_index(args.projects_root)
    print(f"Bilddateien im Index: {sum(len(v) for v in image_index.values())}")

    samples = json.loads(
        (args.knowledge_root / "training_samples.json").read_text(encoding="utf-8-sig"))
    gold_keys = {comparison_key(s.get("CaseId")) for s in samples}
    gold_keys.discard(None)
    protection = load_protection_keys(args.knowledge_root)

    ergebnis: dict[str, dict] = {}
    kopf = f"{'Klasse':6s} {'Befunde':>8s} {'mitFoto':>8s} {'HaltFoto':>8s} {'dekod.':>7s} {'HaltEnde':>8s} {'Kandid.':>8s}"
    print(kopf)
    for class_prefix in classes:
        auswertung = evaluate_class(
            class_prefix, befunde, totals_xtf, totals_db3,
            image_index, gold_keys, protection, args.max_per_holding)
        ergebnis[class_prefix] = auswertung
        print(f"{class_prefix:6s} {auswertung['befunde_gesamt']:8d} "
              f"{auswertung['mit_fotoverweis']:8d} {auswertung['haltungen_mit_foto']:8d} "
              f"{auswertung['dekodierbar']:7d} {auswertung['haltungen_final']:8d} "
              f"{auswertung['kandidaten_anzahl']:8d}")

    args.out.parent.mkdir(parents=True, exist_ok=True)
    args.out.write_text(json.dumps({
        "schema_version": "class-candidates-v2",
        "quellen": {"xtf_verschieden": n_xtf, "db3_datenfuehrend": n_db3},
        "max_per_holding": args.max_per_holding,
        "klassen": ergebnis,
    }, ensure_ascii=False, indent=1), encoding="utf-8")
    print(f"\nMessung: {args.out}")
    print("Schreibfrei — es wurde nichts kopiert oder veraendert.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
