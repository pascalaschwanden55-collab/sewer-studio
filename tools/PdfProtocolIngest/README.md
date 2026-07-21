# PdfProtocolIngest — Lernen aus Inspektions-PDFs

Wandelt die VSA-KEK-Inspektionsprotokolle in `D:\Haltungen` in **experten-gelabelte Frames** um: PDF-Befund (Meter, Code, Uhrlage, Zeitstempel) → exakter Video-Frame.

**Rein lesend beim Parsen. Kundendaten bleiben ausserhalb des Repos** — `--out` zeigt auf `C:\KI_BRAIN\...`.

## Voraussetzungen (auf deiner Maschine)
- Python 3.10+
- `poppler` (liefert `pdftotext`, `pdfinfo`) im PATH
- `ffmpeg` im PATH

## Nutzung

```powershell
# 1) Katalog erzeugen (rein lesend): schreibt ingest.jsonl
python pdf_ingest.py parse   --root D:\Haltungen --out C:\KI_BRAIN\training\pdf_ingest

# 2) Frames ziehen (schreibt PNGs nach OUT\frames\<klasse>\ + labels.jsonl)
python pdf_ingest.py extract --root D:\Haltungen --out C:\KI_BRAIN\training\pdf_ingest
```

Beide Modi sind **resumierbar** (einfach erneut starten). `extract` überspringt bereits gezogene Frames.

## Was rauskommt
- `ingest.jsonl` — ein Eintrag je PDF: Template, Video, alle Befunde (Meter, Code, Klasse, Uhrlage, Zeitstempel).
- `labels.jsonl` — ein Eintrag je Schadensframe: Bildpfad, Klasse, Code, Meter, Uhrlage, Beschreibung, Haltung.
- `frames\<klasse>\*.png` — die extrahierten Bilder, nach Detektor-Klasse sortiert.

## Stand & Grenzen (ehrlich)
- **Abgedeckt:** combit (KIT Bauinspekt), pdf24 (Fretz), ncreport (Abwasser Uri) — gleiche Tabellenlogik — **plus KINS** („Leitungsbildbericht", eigener Block-Parser).
- **Noch offen:** ~256 PDFs sind reine Grafik-/Deckblätter (Inspektionslänge 0 m) oder gescannt — daraus ist ohne OCR nichts zu gewinnen (bewusst ausgelassen).
- **Frame-Genauigkeit v1:** Extraktion exakt am Protokoll-Zeitstempel. Der OSD-Meterstand im Bild deckt sich erfahrungsgemäss (Stichprobe 9,20 m ↔ OSD 9,27 m), aber ein automatischer OSD-Abgleich per OCR ist als v2 offen.
- **Labels sind frame-basiert (kein Bounding-Box):** ideal direkt für **YOLO-cls** (Gate) und **Qwen**; für **YOLO-Detect** dienen sie als sichere Vorlage fürs klassenbekannte Auto-Boxing (DINO/SAM).

## Split-Regel
Frames einer Haltung nie über Train/Dev-Val/Gold streuen (Leakage). Gold-Haltungen vor dem Training sperren.
