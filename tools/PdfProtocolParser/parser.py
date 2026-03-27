#!/usr/bin/env python3
"""
PDF-Parser fuer Kanalinspektion-Protokolle.
Unterstuetzt zwei Formate:
  Format 1: Fretz Kanal-Service (Haltungsinspektion + Haltungsbilder, 2-Spalten)
  Format 2: IBAK Leitungsgrafik/Haltungsgrafik (Bildbericht-Bloecke)
Ausgabe: JSON pro Haltung + Gesamt-CSV.
"""

import csv
import json
import logging
import re
import sys
from pathlib import Path

import pdfplumber

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] %(message)s",
    datefmt="%H:%M:%S",
)
log = logging.getLogger(__name__)

# ══════════════════════════════════════════════════════════════════════════════
#  Gemeinsame Regex
# ══════════════════════════════════════════════════════════════════════════════

RE_UHR_VON_BIS = re.compile(r"von\s+(\d{1,2})\s+Uhr\s*,?\s*bis\s+(\d{1,2})\s+Uhr")
RE_UHR_BEI = re.compile(r"bei\s+(\d{1,2})\s+Uhr")

# ══════════════════════════════════════════════════════════════════════════════
#  Format-Erkennung
# ══════════════════════════════════════════════════════════════════════════════

def detect_format(page1_text: str) -> str:
    """Erkennt das PDF-Format anhand des Seite-1-Textes."""
    if "Haltungsinspektion" in page1_text:
        return "fretz"
    if "Leitungsgrafik" in page1_text or "Haltungsgrafik" in page1_text:
        return "ibak_bildbericht"
    if "Leitungsbildbericht" in page1_text or "Haltungsbildbericht" in page1_text:
        return "ibak_bildbericht"
    return "unknown"

# ══════════════════════════════════════════════════════════════════════════════
#  Format 1: Fretz Kanal-Service
# ══════════════════════════════════════════════════════════════════════════════

RE_FOTO_FULL = re.compile(
    r"([\d.]+[-][\d]+_[0-9a-f-]{36}_\d{8}_\d{6}_\d{3}\.jpg)"
    r",?\s*(\d{2}:\d{2}:\d{2}),?\s*(\d+\.\d{2})m"
)

RE_F1_HALTUNG = re.compile(r"Haltungsinspektion\s*[-–]\s*[\d.]+\s*[-–]\s*([\d.]+[-–]\d+)")
RE_F1_ORT = re.compile(r"Ort\s+(\d{4}\s+\S+)")
RE_F1_STRASSE = re.compile(r"Strasse\s+(.+?)(?:\s{2,}|Schacht)")
RE_F1_PROFIL = re.compile(r"Profil\s+(.*?)(?:\s{2,}|Grund)")
RE_F1_MATERIAL = re.compile(r"Material\s+(\S+)")
RE_F1_NUTZUNGSART = re.compile(r"Nutzungsart\s+(\S+)")
RE_F1_INSPRICHTUNG = re.compile(r"Inspektionsrichtung\s+(.+?)(?:\s{2,}|\n)")
RE_F1_SCHACHT_OBEN = re.compile(r"Schacht\s+oben\s+([\d.]+)")
RE_F1_SCHACHT_UNTEN = re.compile(r"Schacht\s+unten\s+(\d+)")
RE_F1_INSPLAENGE = re.compile(r"Insp\.L.nge\s*\[m\]\s+([\d.]+)")
RE_F1_ROHRLAENGE = re.compile(r"Rohrl.nge\s*\[m\]\s+(\d+)")
RE_F1_DATUM = re.compile(r"Haltungsinspektion\s*[-–]\s*(\d{2}\.\d{2}\.\d{4})")
RE_F1_GEREINIGT = re.compile(r"(?:ereinigt|Gereinigt)\s+(Ja|Nein)")


def _field(pattern, text, default=None):
    m = pattern.search(text)
    return m.group(1).strip() if m else default


def parse_fretz_header(text: str) -> dict:
    return {
        "haltung": _field(RE_F1_HALTUNG, text),
        "datum": _field(RE_F1_DATUM, text),
        "ort": _field(RE_F1_ORT, text),
        "strasse": _field(RE_F1_STRASSE, text),
        "profil": _field(RE_F1_PROFIL, text),
        "material": _field(RE_F1_MATERIAL, text),
        "nutzungsart": _field(RE_F1_NUTZUNGSART, text),
        "inspektionsrichtung": _field(RE_F1_INSPRICHTUNG, text),
        "schacht_oben": _field(RE_F1_SCHACHT_OBEN, text),
        "schacht_unten": _field(RE_F1_SCHACHT_UNTEN, text),
        "inspektionslaenge_m": _field(RE_F1_INSPLAENGE, text),
        "rohrlaenge_m": _field(RE_F1_ROHRLAENGE, text),
        "gereinigt": _field(RE_F1_GEREINIGT, text),
    }


def parse_fretz_schaeden(text: str) -> list[dict]:
    schaeden = []
    table_start = text.find("Stufe")
    if table_start < 0:
        return schaeden
    table_text = text[table_start:]
    lines = table_text.split("\n")

    groups = []
    for line in lines[1:]:
        stripped = line.strip()
        if not stripped:
            continue
        if re.match(r"^\d+\.\d{2}\s+[A-Z]{2}", stripped):
            groups.append([stripped])
        elif groups:
            groups[-1].append(stripped)

    for group_lines in groups:
        first_line = group_lines[0]
        m = re.match(r"^(\d+\.\d{2})\s+([A-Z]{2,6})\s+(.+)", first_line)
        if not m:
            continue

        meter = float(m.group(1))
        en_code = m.group(2)
        rest_first = m.group(3)

        stufe = None
        stufe_match = re.search(r"\s([1-4])\s*$", first_line)
        if stufe_match:
            stufe = int(stufe_match.group(1))

        ts_match = re.search(r"(\d{2}:\d{2}:\d{2})", rest_first)
        timestamp = ts_match.group(1) if ts_match else None

        if ts_match:
            beschreibung = rest_first[:ts_match.start()].strip().rstrip(",")
        else:
            beschreibung = rest_first.strip()

        for cont in group_lines[1:]:
            cs = cont.strip()
            if re.match(r"^[0-9a-f]{2,}", cs): continue
            if re.match(r"^[\d.]+-\d+", cs): continue
            if re.match(r"^Seite\s+\d", cs): continue
            beschreibung += ", " + cs

        beschreibung = re.sub(r",?\s*\d+_[0-9a-f]{2,}$", "", beschreibung)
        beschreibung = re.sub(r",\s*,", ",", beschreibung).strip().rstrip(",")

        uhr_von, uhr_bis = _extract_uhr(beschreibung)

        schaeden.append({
            "meter": meter, "en_code": en_code, "beschreibung": beschreibung,
            "uhr_von": uhr_von, "uhr_bis": uhr_bis, "stufe": stufe,
            "timestamp_video": timestamp, "foto_datei": None, "foto_nr": None,
        })

    return schaeden


def parse_fretz_fotos(page) -> dict:
    text = page.extract_text() or ""
    if "Haltungsbilder" not in text:
        return {}
    words = page.extract_words()
    if not words:
        return {}

    foto_starts = [w for w in words
                   if re.match(r"^\d+\.\d+[-]\d+_[0-9a-f]{8}", w["text"])]
    if not foto_starts:
        return {}

    x_vals = sorted(set(round(float(w["x0"])) for w in foto_starts))
    col_boundary = float("inf")
    if len(x_vals) >= 2:
        x_left = x_vals[0]
        x_right = next((x for x in x_vals if x - x_left > 50), None)
        if x_right:
            col_boundary = x_right - 20

    y_groups = {}
    for w in words:
        top = float(w["top"])
        matched = False
        for y_key in sorted(y_groups):
            if abs(y_key - top) < 4:
                y_groups[y_key].append(w)
                matched = True
                break
        if not matched:
            y_groups[top] = [w]

    left_lines, right_lines = [], []
    for _, row_words in sorted(y_groups.items()):
        rw = sorted(row_words, key=lambda w: float(w["x0"]))
        lp = [w["text"] for w in rw if float(w["x0"]) < col_boundary]
        rp = [w["text"] for w in rw if float(w["x0"]) >= col_boundary]
        if lp: left_lines.append(" ".join(lp))
        if rp: right_lines.append(" ".join(rp))

    foto_map = {}
    for col_lines in [left_lines, right_lines]:
        for i in range(len(col_lines)):
            for m in RE_FOTO_FULL.finditer(col_lines[i]):
                _add_foto_match(m, foto_map)
            if i + 1 < len(col_lines):
                for m in RE_FOTO_FULL.finditer(col_lines[i] + col_lines[i + 1]):
                    _add_foto_match(m, foto_map)
    return foto_map


def _add_foto_match(m, foto_map: dict):
    foto_datei = m.group(1)
    if len(foto_datei) < 40:
        return
    timestamp = m.group(2)
    meter = m.group(3)
    key = f"{timestamp}_{meter}"
    foto_map[key] = foto_datei
    if timestamp not in foto_map:
        foto_map[timestamp] = foto_datei


def parse_fretz(pdf) -> dict | None:
    page1_text = pdf.pages[0].extract_text() or ""
    header = parse_fretz_header(page1_text)
    schaeden = parse_fretz_schaeden(page1_text)

    foto_map = {}
    for page in pdf.pages[1:]:
        foto_map.update(parse_fretz_fotos(page))

    linked = 0
    for s in schaeden:
        if not s["timestamp_video"]:
            continue
        key = f"{s['timestamp_video']}_{s['meter']:.2f}"
        if key in foto_map:
            s["foto_datei"] = foto_map[key]
            linked += 1
        elif s["timestamp_video"] in foto_map:
            s["foto_datei"] = foto_map[s["timestamp_video"]]
            linked += 1

    return {**header, "schaeden": schaeden, "_linked_fotos": linked}


# ══════════════════════════════════════════════════════════════════════════════
#  Format 2: IBAK Leitungsgrafik / Haltungsgrafik + Bildbericht
# ══════════════════════════════════════════════════════════════════════════════

def parse_ibak_header(text: str) -> dict:
    """Parst Header aus Leitungsgrafik/Haltungsgrafik Seite 1."""
    # "Leitung 07.1026777-10.1064902" oder "Haltung 1042608-1042610"
    haltung = _field(re.compile(r"(?:Leitung|Haltung)\s+([\d.]+-[\d.]+)"), text)
    datum = _field(re.compile(r"Insp\.?\s*datum\s+(\d{2}\.\d{2}\.\d{4})"), text)
    ort = _field(re.compile(r"Ort\s+(\d{4}\s+\S+)"), text)
    strasse = _field(re.compile(r"Stra.e/\s*Standort\s+(\S+)"), text)
    material = _field(re.compile(r"Material\s+(\S+)"), text)
    profil = _field(re.compile(r"Profilart\s+(\S+)"), text)
    dimension = _field(re.compile(r"Dimension\s*\[mm\]\s+([\d\s/]+)"), text)
    nutzungsart = _field(re.compile(r"Nutzungsart\s+(\S+)"), text)
    insprichtung = _field(re.compile(r"Inspektionsrichtung\s+(.+?)(?:\n|$)"), text)
    insplaenge = _field(re.compile(r"Inspektionsl.nge\s+([\d,]+)"), text)
    rohrlaenge = _field(re.compile(r"Rohrl.nge\s+([\d,]+)"), text)
    schacht_oben = _field(re.compile(r"(?:Oberer\s+(?:Schacht|Punkt))\s+([\d.]+)"), text)
    schacht_unten = _field(re.compile(r"(?:Unterer\s+(?:Schacht|Punkt))\s+([\d.]+)"), text)

    # Profil + Dimension zusammenfuegen
    if profil and dimension:
        profil = f"{profil} {dimension.strip()}mm"

    # Komma-Dezimaltrenner normalisieren
    if insplaenge: insplaenge = insplaenge.replace(",", ".")
    if rohrlaenge: rohrlaenge = rohrlaenge.replace(",", ".")

    return {
        "haltung": haltung, "datum": datum, "ort": ort, "strasse": strasse,
        "profil": profil, "material": material, "nutzungsart": nutzungsart,
        "inspektionsrichtung": insprichtung, "schacht_oben": schacht_oben,
        "schacht_unten": schacht_unten, "inspektionslaenge_m": insplaenge,
        "rohrlaenge_m": rohrlaenge, "gereinigt": None,
    }


def parse_ibak_bildbericht(pages_text: list[str]) -> list[dict]:
    """Extrahiert Schaeden aus Leitungsbildbericht/Haltungsbildbericht-Seiten.

    Jeder Schadensblock hat:
      Beschreibung (mehrzeilig)
      Foto NNN
      Video HH:MM:SS (optional)
      Entf. in Fließr. XX,XX m
      Zustand CODE
      Position N (optional, Uhr)
    """
    schaeden = []
    re_zustand = re.compile(r"^Zustand\s+([A-Z][A-Z0-9.]+)")
    re_entf = re.compile(r"Entf\.\s*in\s+Flie.r\.\s+([\d,]+)\s*m")
    re_video = re.compile(r"^Video\s+(\d{2}:\d{2}:\d{2})")
    re_foto_nr = re.compile(r"^Foto\s+(\d+)")
    re_position = re.compile(r"^Position\s+(\d+)")

    for page_text in pages_text:
        if "bildbericht" not in page_text.lower():
            continue

        lines = page_text.split("\n")
        # Finde alle Zustand-Zeilen als Anker und arbeite rueckwaerts
        blocks = []
        current_block_lines = []
        in_block = False

        for line in lines:
            stripped = line.strip()
            # Ignoriere Header-Zeilen
            if any(kw in stripped for kw in ["bildbericht", "Oberer", "Unterer",
                                              "Dimension", "Kanalart", "Nutzungsart",
                                              "Gedruckt am", "Standort"]):
                if in_block and current_block_lines:
                    blocks.append(current_block_lines)
                    current_block_lines = []
                    in_block = False
                continue
            if not stripped or stripped.startswith("Datentr"):
                continue

            # Neuer Block beginnt bei Beschreibungstext (Grossbuchstabe, kein Keyword)
            if (not in_block and stripped and
                not re_zustand.match(stripped) and
                not re_entf.match(stripped) and
                not re_video.match(stripped) and
                not re_foto_nr.match(stripped) and
                not re_position.match(stripped) and
                re.match(r"^[A-ZÄÖÜ]", stripped)):
                if current_block_lines:
                    blocks.append(current_block_lines)
                current_block_lines = [stripped]
                in_block = True
            elif in_block or re_zustand.match(stripped) or re_entf.match(stripped):
                current_block_lines.append(stripped)
                in_block = True

        if current_block_lines:
            blocks.append(current_block_lines)

        # Blocks parsen
        for block in blocks:
            en_code = None
            meter = None
            timestamp = None
            foto_nr = None
            position = None
            beschreibung_parts = []

            for bline in block:
                m_z = re_zustand.match(bline)
                m_e = re_entf.search(bline)
                m_v = re_video.match(bline)
                m_f = re_foto_nr.match(bline)
                m_p = re_position.match(bline)

                if m_z:
                    en_code = m_z.group(1).replace(".", "")
                elif m_e:
                    meter = float(m_e.group(1).replace(",", "."))
                elif m_v:
                    timestamp = m_v.group(1)
                elif m_f:
                    foto_nr = int(m_f.group(1))
                elif m_p:
                    position = m_p.group(1)
                elif not bline.startswith("Video"):
                    beschreibung_parts.append(bline)

            if not en_code:
                continue

            beschreibung = " ".join(beschreibung_parts).strip()
            # "Pos: N; " Prefix entfernen
            beschreibung = re.sub(r"^Pos:\s*\d+;\s*", "", beschreibung)

            uhr_von, uhr_bis = _extract_uhr(beschreibung)
            # Position als Uhr verwenden wenn keine Uhr in Beschreibung
            if not uhr_von and position:
                uhr_von = position
                uhr_bis = position

            schaeden.append({
                "meter": meter if meter is not None else 0.0,
                "en_code": en_code,
                "beschreibung": beschreibung,
                "uhr_von": uhr_von,
                "uhr_bis": uhr_bis,
                "stufe": None,
                "timestamp_video": timestamp,
                "foto_datei": None,
                "foto_nr": foto_nr,
            })

    return schaeden


def parse_ibak(pdf) -> dict | None:
    page1_text = pdf.pages[0].extract_text() or ""
    header = parse_ibak_header(page1_text)

    pages_text = [p.extract_text() or "" for p in pdf.pages]
    schaeden = parse_ibak_bildbericht(pages_text)

    return {**header, "schaeden": schaeden, "_linked_fotos": 0}


# ══════════════════════════════════════════════════════════════════════════════
#  Gemeinsame Hilfsfunktionen
# ══════════════════════════════════════════════════════════════════════════════

def _extract_uhr(beschreibung: str):
    uhr_von, uhr_bis = None, None
    m = RE_UHR_VON_BIS.search(beschreibung)
    if m:
        uhr_von, uhr_bis = m.group(1), m.group(2)
    else:
        m = RE_UHR_BEI.search(beschreibung)
        if m:
            uhr_von = uhr_bis = m.group(1)
    return uhr_von, uhr_bis


# ══════════════════════════════════════════════════════════════════════════════
#  Hauptlogik
# ══════════════════════════════════════════════════════════════════════════════

def parse_pdf(pdf_path: Path) -> dict | None:
    try:
        pdf = pdfplumber.open(str(pdf_path))
    except Exception as e:
        log.error(f"Kann PDF nicht oeffnen: {pdf_path.name} - {e}")
        return None

    if not pdf.pages:
        log.warning(f"Leeres PDF: {pdf_path.name}")
        return None

    page1_text = pdf.pages[0].extract_text() or ""
    fmt = detect_format(page1_text)

    if fmt == "fretz":
        result = parse_fretz(pdf)
    elif fmt == "ibak_bildbericht":
        result = parse_ibak(pdf)
    else:
        # Pruefen ob Seite 2+ einen Bildbericht hat
        all_text = " ".join(p.extract_text() or "" for p in pdf.pages)
        if "bildbericht" in all_text.lower():
            result = parse_ibak(pdf)
            fmt = "ibak_bildbericht"
        else:
            log.warning(f"  {pdf_path.name}: Unbekanntes Format → uebersprungen")
            pdf.close()
            return None

    pdf.close()

    if result is None:
        return None

    linked = result.pop("_linked_fotos", 0)
    n_schaeden = len(result.get("schaeden", []))
    n_with_ts = sum(1 for s in result.get("schaeden", []) if s.get("timestamp_video"))

    log.info(
        f"  {pdf_path.name} [{fmt}]: {result.get('haltung', '?')} - "
        f"{n_schaeden} Schaeden, {n_with_ts} mit Timecode"
        f"{f', {linked} Fotos verknuepft' if linked else ''}"
    )
    return result


def write_csv(all_results: list[dict], output_dir: Path):
    csv_path = output_dir / "schaeden_gesamt.csv"
    fieldnames = [
        "haltung", "ort", "strasse", "profil", "material",
        "meter", "en_code", "beschreibung", "uhr_von", "uhr_bis",
        "stufe", "timestamp_video", "foto_datei", "foto_nr",
    ]

    rows = []
    for r in all_results:
        for s in r.get("schaeden", []):
            row = {
                "haltung": r.get("haltung"),
                "ort": r.get("ort"),
                "strasse": r.get("strasse"),
                "profil": r.get("profil"),
                "material": r.get("material"),
                **s,
            }
            rows.append(row)

    with open(csv_path, "w", newline="", encoding="utf-8-sig") as f:
        writer = csv.DictWriter(f, fieldnames=fieldnames, delimiter=";", extrasaction="ignore")
        writer.writeheader()
        writer.writerows(rows)

    log.info(f"CSV geschrieben: {csv_path} ({len(rows)} Zeilen)")


def main():
    if len(sys.argv) > 1:
        input_dir = Path(sys.argv[1])
    else:
        input_dir = Path(__file__).parent / "input_pdfs"

    output_json_dir = Path(__file__).parent / "output_json"
    output_csv_dir = Path(__file__).parent / "output_csv"
    output_json_dir.mkdir(exist_ok=True)
    output_csv_dir.mkdir(exist_ok=True)

    # Rekursiv alle PDFs suchen
    pdfs = sorted(input_dir.rglob("*.pdf")) if input_dir.is_dir() else [input_dir]
    if not pdfs:
        log.warning(f"Keine PDFs gefunden in: {input_dir}")
        sys.exit(1)

    log.info(f"Starte Parsing: {len(pdfs)} PDF(s) in {input_dir}")

    all_results = []
    total_schaeden = 0
    total_fotos = 0
    format_counts = {"fretz": 0, "ibak_bildbericht": 0, "unknown": 0}

    for pdf_path in pdfs:
        result = parse_pdf(pdf_path)
        if not result:
            format_counts["unknown"] = format_counts.get("unknown", 0) + 1
            continue

        # Format zaehlen
        page1 = ""
        try:
            with pdfplumber.open(str(pdf_path)) as p:
                page1 = p.pages[0].extract_text() or ""
        except:
            pass
        fmt = detect_format(page1)
        format_counts[fmt] = format_counts.get(fmt, 0) + 1

        all_results.append(result)

        n_schaeden = len(result.get("schaeden", []))
        n_fotos = sum(1 for s in result.get("schaeden", []) if s.get("foto_datei") or s.get("foto_nr"))
        total_schaeden += n_schaeden
        total_fotos += n_fotos

        haltung_name = result.get("haltung", pdf_path.stem) or pdf_path.stem
        json_name = re.sub(r"[^\w\-]", "_", haltung_name) + ".json"
        json_path = output_json_dir / json_name
        with open(json_path, "w", encoding="utf-8") as f:
            json.dump(result, f, ensure_ascii=False, indent=2)

    if all_results:
        write_csv(all_results, output_csv_dir)

    log.info("=" * 60)
    log.info(
        f"Fertig: {len(all_results)} Haltungen, "
        f"{total_schaeden} Schaeden, "
        f"{total_fotos} Fotos/Referenzen"
    )
    log.info(f"Formate: Fretz={format_counts.get('fretz',0)}, "
             f"IBAK-Bildbericht={format_counts.get('ibak_bildbericht',0)}, "
             f"Unbekannt={format_counts.get('unknown',0)}")


if __name__ == "__main__":
    main()
