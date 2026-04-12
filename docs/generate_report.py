#!/usr/bin/env python3
"""
SewerStudio — Vollstaendiger Programmbericht als PDF
Generiert mit reportlab, April 2026
"""

from reportlab.lib.pagesizes import A4
from reportlab.lib.units import mm, cm
from reportlab.lib.styles import getSampleStyleSheet, ParagraphStyle
from reportlab.lib.colors import HexColor, black, white
from reportlab.lib.enums import TA_LEFT, TA_CENTER, TA_JUSTIFY
from reportlab.platypus import (
    SimpleDocTemplate, Paragraph, Spacer, Table, TableStyle,
    PageBreak, KeepTogether, HRFlowable
)
from reportlab.platypus.tableofcontents import TableOfContents
from reportlab.lib import colors
import os
import datetime

# ── Farben ──
DARK_BLUE = HexColor("#1a365d")
MID_BLUE = HexColor("#2b6cb0")
LIGHT_BLUE = HexColor("#bee3f8")
ACCENT = HexColor("#ed8936")
LIGHT_GRAY = HexColor("#f7fafc")
BORDER_GRAY = HexColor("#e2e8f0")
GREEN = HexColor("#38a169")
YELLOW_BG = HexColor("#fefcbf")
RED_SOFT = HexColor("#fc8181")

OUTPUT_DIR = os.path.dirname(os.path.abspath(__file__))
OUTPUT_PDF = os.path.join(OUTPUT_DIR, "SewerStudio_Programmbericht_2026.pdf")

# ── Styles ──
styles = getSampleStyleSheet()

styles.add(ParagraphStyle(
    "CoverTitle", parent=styles["Title"],
    fontSize=28, leading=34, textColor=DARK_BLUE,
    spaceAfter=6*mm, alignment=TA_CENTER
))
styles.add(ParagraphStyle(
    "CoverSubtitle", parent=styles["Normal"],
    fontSize=14, leading=18, textColor=MID_BLUE,
    spaceAfter=4*mm, alignment=TA_CENTER
))
styles.add(ParagraphStyle(
    "H1", parent=styles["Heading1"],
    fontSize=18, leading=22, textColor=DARK_BLUE,
    spaceBefore=12*mm, spaceAfter=4*mm,
    borderWidth=0, borderPadding=0
))
styles.add(ParagraphStyle(
    "H2", parent=styles["Heading2"],
    fontSize=14, leading=17, textColor=MID_BLUE,
    spaceBefore=8*mm, spaceAfter=3*mm
))
styles.add(ParagraphStyle(
    "H3", parent=styles["Heading3"],
    fontSize=11, leading=14, textColor=DARK_BLUE,
    spaceBefore=5*mm, spaceAfter=2*mm
))
styles.add(ParagraphStyle(
    "Body", parent=styles["Normal"],
    fontSize=9.5, leading=13, alignment=TA_JUSTIFY,
    spaceAfter=2*mm
))
styles.add(ParagraphStyle(
    "BodySmall", parent=styles["Normal"],
    fontSize=8.5, leading=11, alignment=TA_JUSTIFY,
    spaceAfter=1.5*mm
))
styles.add(ParagraphStyle(
    "BulletCustom", parent=styles["Normal"],
    fontSize=9.5, leading=13, leftIndent=12*mm,
    bulletIndent=6*mm, spaceAfter=1*mm
))
styles.add(ParagraphStyle(
    "TableCell", parent=styles["Normal"],
    fontSize=8, leading=10, alignment=TA_LEFT
))
styles.add(ParagraphStyle(
    "TableHeader", parent=styles["Normal"],
    fontSize=8, leading=10, textColor=white,
    alignment=TA_LEFT
))
styles.add(ParagraphStyle(
    "Footer", parent=styles["Normal"],
    fontSize=7, leading=9, textColor=HexColor("#718096"),
    alignment=TA_CENTER
))
styles.add(ParagraphStyle(
    "Caption", parent=styles["Normal"],
    fontSize=8, leading=10, textColor=HexColor("#4a5568"),
    alignment=TA_CENTER, spaceAfter=3*mm, spaceBefore=1*mm
))

def p(text, style="Body"):
    return Paragraph(text, styles[style])

def bullet(text):
    return Paragraph(f"\u2022  {text}", styles["BulletCustom"])

def hr():
    return HRFlowable(width="100%", thickness=0.5, color=BORDER_GRAY,
                       spaceBefore=2*mm, spaceAfter=2*mm)

def spacer(h=4):
    return Spacer(1, h*mm)

def make_table(headers, rows, col_widths=None):
    """Erzeugt eine formatierte Tabelle."""
    header_cells = [Paragraph(f"<b>{h}</b>", styles["TableHeader"]) for h in headers]
    data = [header_cells]
    for row in rows:
        data.append([Paragraph(str(c), styles["TableCell"]) for c in row])

    t = Table(data, colWidths=col_widths, repeatRows=1)
    t.setStyle(TableStyle([
        ("BACKGROUND", (0, 0), (-1, 0), DARK_BLUE),
        ("TEXTCOLOR", (0, 0), (-1, 0), white),
        ("FONTSIZE", (0, 0), (-1, 0), 8),
        ("BOTTOMPADDING", (0, 0), (-1, 0), 4),
        ("TOPPADDING", (0, 0), (-1, 0), 4),
        ("BACKGROUND", (0, 1), (-1, -1), white),
        ("ROWBACKGROUNDS", (0, 1), (-1, -1), [white, LIGHT_GRAY]),
        ("GRID", (0, 0), (-1, -1), 0.4, BORDER_GRAY),
        ("VALIGN", (0, 0), (-1, -1), "TOP"),
        ("LEFTPADDING", (0, 0), (-1, -1), 4),
        ("RIGHTPADDING", (0, 0), (-1, -1), 4),
        ("TOPPADDING", (0, 1), (-1, -1), 3),
        ("BOTTOMPADDING", (0, 1), (-1, -1), 3),
    ]))
    return t


def build_story():
    story = []

    # ══════════════════════════════════════════════
    # DECKBLATT
    # ══════════════════════════════════════════════
    story.append(Spacer(1, 50*mm))
    story.append(p("SewerStudio", "CoverTitle"))
    story.append(p("KI-gestuetzte Kanalinspektion", "CoverSubtitle"))
    story.append(hr())
    story.append(Spacer(1, 10*mm))
    story.append(p("Vollstaendiger Programmbericht", "CoverSubtitle"))
    story.append(Spacer(1, 6*mm))
    story.append(p("Hardware \u2022 KI-Modelle \u2022 Funktionen \u2022 Trainingsplan", "CoverSubtitle"))
    story.append(Spacer(1, 30*mm))
    story.append(p(f"Stand: {datetime.date.today().strftime('%d. %B %Y')}", "CoverSubtitle"))
    story.append(p("Version 3.1 \u2014 .NET 8 / WPF / MVVM", "CoverSubtitle"))
    story.append(Spacer(1, 10*mm))
    story.append(p("Plattform: Windows 11 \u2022 Lokal (kein Cloud-Zwang)", "CoverSubtitle"))
    story.append(PageBreak())

    # ══════════════════════════════════════════════
    # INHALTSVERZEICHNIS
    # ══════════════════════════════════════════════
    story.append(p("Inhaltsverzeichnis", "H1"))
    toc_items = [
        "1. Systemuebersicht",
        "2. Hardware-Architektur",
        "3. KI-Modelle im Detail",
        "4. KI-Pipeline: Ablauf pro Frame",
        "5. Alle Programmfunktionen",
        "6. KI-Trainingsplan (Self-Training)",
        "7. Nutzen und Ergebnisse",
        "8. Umgebungsvariablen",
    ]
    for item in toc_items:
        story.append(p(item, "Body"))
    story.append(PageBreak())

    # ══════════════════════════════════════════════
    # 1. SYSTEMUEBERSICHT
    # ══════════════════════════════════════════════
    story.append(p("1. Systemuebersicht", "H1"))
    story.append(p(
        "SewerStudio ist eine Desktop-Anwendung fuer die automatisierte Kanalinspektion. "
        "Die Software analysiert Inspektionsvideos mittels einer lokalen KI-Pipeline, "
        "erkennt Schaeden nach DIN EN 13508-2 / VSA-KEK 2023 und erstellt vollstaendige "
        "Inspektionsprotokolle. Alle Modelle laufen lokal auf der Workstation \u2014 "
        "keine Cloud-Abhaengigkeit, keine Daten verlassen den Rechner."
    ))
    story.append(p(
        "Die Anwendung verarbeitet ca. 3000 WinCan/IBAK-IBSK Inspektionsvideos und "
        "unterstuetzt den gesamten Workflow: Import, Videoanalyse, Protokollierung, "
        "VSA-Bewertung, Sanierungsplanung und Kostenofferte."
    ))
    story.append(spacer(2))
    story.append(p("<b>Technologie-Stack</b>", "Body"))
    story.append(make_table(
        ["Komponente", "Technologie"],
        [
            ["Frontend", "WPF / XAML / MVVM (CommunityToolkit.Mvvm)"],
            ["Backend", ".NET 8, C#"],
            ["KI-Sidecar", "Python FastAPI (YOLO, DINO, SAM)"],
            ["LLM-Server", "Ollama (Qwen2.5-VL, nomic-embed-text)"],
            ["Datenbank", "SQLite (KnowledgeBase, Projekte)"],
            ["Video", "ffmpeg/ffprobe (persistent streaming)"],
            ["Standard", "DIN EN 13508-2 / VSA-KEK 2023 / SIA 405"],
        ],
        col_widths=[45*mm, 120*mm]
    ))
    story.append(PageBreak())

    # ══════════════════════════════════════════════
    # 2. HARDWARE-ARCHITEKTUR
    # ══════════════════════════════════════════════
    story.append(p("2. Hardware-Architektur", "H1"))

    story.append(p("2.1 Entwicklungs-Workstation", "H2"))
    story.append(make_table(
        ["Komponente", "Spezifikation", "Rolle"],
        [
            ["CPU", "Intel Core Ultra 9 285K (24 Kerne / 24 Threads)", "Post-Processing, ffmpeg, Aggregation"],
            ["GPU", "ASUS NVIDIA RTX 5090 (32 GB GDDR7)", "YOLO, DINO, SAM, Qwen Inferenz"],
            ["RAM", "64 GB DDR5-6400", "Projektdaten, Frame-Buffer, Ollama-Context"],
            ["Storage", "NVMe SSD", "Video-I/O, SQLite KnowledgeBase"],
            ["OS", "Windows 11 Pro", "WPF-Runtime, CUDA 12+"],
        ],
        col_widths=[30*mm, 70*mm, 65*mm]
    ))
    story.append(spacer(4))

    story.append(p("2.2 GPU-Auto-Erkennung (GpuModelSelector)", "H2"))
    story.append(p(
        "SewerStudio erkennt beim Start automatisch den verfuegbaren GPU-VRAM via "
        "<font face='Courier' size='8'>nvidia-smi</font> und waehlt das passende KI-Profil:"
    ))
    story.append(make_table(
        ["VRAM", "Profil", "Vision-Modell", "NumCtx", "Modus"],
        [
            ["\u2265 24 GB", "Workstation", "Qwen2.5-VL-32B (Q5)", "8192", "Dual-Modell (8B + 32B Eskalation)"],
            ["\u2265 8 GB", "Laptop", "Qwen2.5-VL-7B (Q4)", "4096", "Single-Modell (nur 8B)"],
            ["&lt; 8 GB", "Deaktiviert", "\u2014", "\u2014", "Keine KI-Vision"],
        ],
        col_widths=[22*mm, 25*mm, 45*mm, 18*mm, 55*mm]
    ))
    story.append(spacer(4))

    story.append(p("2.3 VRAM-Budget pro Modell", "H2"))
    story.append(make_table(
        ["Modell", "Groesse (Disk)", "VRAM (GPU)", "Modus", "Verweildauer"],
        [
            ["YOLO11m-seg", "~25 MB", "~100 MB", "Permanent auf GPU", "Gesamte Session"],
            ["Grounding DINO 1.5 (SwinT)", "~300 MB", "~600 MB", "On-Demand", "Nur bei YOLO-relevant"],
            ["SAM ViT-H", "~2.5 GB", "~2.5 GB", "On-Demand", "Nur bei DINO-Detection"],
            ["Qwen2.5-VL-32B (Q5)", "~22 GB", "~26 GB", "Permanent (Ollama)", "Keep-Alive 24h"],
            ["Qwen2.5-VL-7B (Q4)", "~5 GB", "~6 GB", "Permanent (Ollama)", "Keep-Alive 24h"],
            ["nomic-embed-text", "~270 MB", "~500 MB", "On-Demand (Ollama)", "KB-Retrieval"],
        ],
        col_widths=[40*mm, 25*mm, 22*mm, 35*mm, 43*mm]
    ))
    story.append(p(
        "<b>Gesamtbudget Workstation (RTX 5090):</b> YOLO + DINO + SAM + Qwen32B = ~29 GB. "
        "Verbleiben ~3 GB fuer CUDA-Overhead und Ollama-Context. "
        "DINO und SAM werden nie gleichzeitig mit Qwen geladen (QualityGate-gesteuert).",
        "BodySmall"
    ))
    story.append(spacer(4))

    story.append(p("2.4 Parallelitaets-Einstellungen (Self-Training)", "H2"))
    story.append(make_table(
        ["Setting", "Default (5090)", "Env-Variable", "Beschreibung"],
        [
            ["GpuConcurrency", "6", "SEWERSTUDIO_GPU_CONCURRENCY", "Parallele Ollama-GPU-Requests"],
            ["CpuPreExtractParallelism", "ProcessorCount-2", "SEWERSTUDIO_SELFTRAIN_PREEXTRACT_PARALLELISM", "CPU-Threads fuer PDF-Foto-Vorabextraktion"],
            ["CaseParallelism", "3", "SEWERSTUDIO_SELFTRAIN_CASE_PARALLELISM", "Gleichzeitig verarbeitete Inspektionsfaelle"],
        ],
        col_widths=[38*mm, 25*mm, 55*mm, 47*mm]
    ))
    story.append(PageBreak())

    # ══════════════════════════════════════════════
    # 3. KI-MODELLE IM DETAIL
    # ══════════════════════════════════════════════
    story.append(p("3. KI-Modelle im Detail", "H1"))

    # YOLO
    story.append(p("3.1 YOLO \u2014 Pre-Screening und Objekterkennung", "H2"))
    story.append(p(
        "YOLO (You Only Look Once) ist das erste Modell in der Pipeline. Es entscheidet in "
        "Millisekunden ob ein Frame relevant ist oder uebersprungen werden kann."
    ))
    story.append(make_table(
        ["Eigenschaft", "Wert"],
        [
            ["Modell", "YOLO11m-seg (Custom fine-tuned fuer Kanalinspektion)"],
            ["Fallback", "COCO-Gewichte (yolo11m.pt)"],
            ["Confidence-Schwelle", "0.25 (konfigurierbar)"],
            ["Device", "cuda:0 (Fallback: CPU)"],
            ["Inferenzzeit", "50\u2013200 ms pro Frame"],
            ["Funktion", "Frame-Relevanz, Schadens-Detektion, Bounding-Boxes"],
        ],
        col_widths=[38*mm, 127*mm]
    ))
    story.append(spacer(2))
    story.append(p("<b>CPU-seitiger Qualitaetsfilter (vor GPU-Inferenz):</b>", "Body"))
    story.append(p(
        "Bevor YOLO den Frame an die GPU schickt, prueft ein CPU-Filter die Bildqualitaet. "
        "Unbrauchbare Frames werden mit 0 ms Inferenzzeit uebersprungen:"
    ))
    story.append(make_table(
        ["Metrik", "Schwelle", "Bedeutung"],
        [
            ["Mittlere Helligkeit", "&lt; 15", "Zu dunkel (Linsenkappe, kein Licht)"],
            ["Mittlere Helligkeit", "&gt; 245", "Ueberbelichtet (Blendung)"],
            ["Helligkeits-Stdabw.", "&lt; 8", "Keine Textur (uniforme Flaeche)"],
            ["Laplacian-Varianz", "&lt; 5", "Zu unscharf (Bewegung, Defokus)"],
        ],
        col_widths=[40*mm, 25*mm, 100*mm]
    ))
    story.append(spacer(4))

    # DINO
    story.append(p("3.2 Grounding DINO 1.5 \u2014 Text-basierte Detektion", "H2"))
    story.append(p(
        "Grounding DINO kombiniert Bildverstaendnis mit Text-Prompts. Es findet Objekte "
        "basierend auf natuerlichsprachlichen Beschreibungen \u2014 ideal fuer die offene "
        "Palette an Kanalschaeden."
    ))
    story.append(make_table(
        ["Eigenschaft", "Wert"],
        [
            ["Modell", "Grounding DINO 1.5 (SwinT Backbone)"],
            ["Architektur", "6 Encoder + 6 Decoder Layers, 256 Hidden Dim, 8 Attention Heads"],
            ["Box-Schwelle", "0.30 (konfigurierbar)"],
            ["Text-Schwelle", "0.25 (konfigurierbar)"],
            ["Inferenzzeit", "200\u2013500 ms pro Frame"],
            ["Einsatz", "Nur bei YOLO-relevanten Frames (On-Demand)"],
        ],
        col_widths=[38*mm, 127*mm]
    ))
    story.append(spacer(2))
    story.append(p("<b>Text-Prompt (Schaden-Vokabular):</b>", "BodySmall"))
    story.append(p(
        "<font face='Courier' size='7'>crack . fracture . break . deformation . corrosion . surface damage . "
        "erosion . root intrusion . roots . deposit . sediment . buildup . obstacle . blockage . "
        "infiltration . water ingress . leak . displaced joint . open joint . offset joint . "
        "hole . collapse . missing wall . connection defect . pipe defect . "
        "intruding connection . protruding seal</font>",
        "BodySmall"
    ))
    story.append(spacer(4))

    # SAM
    story.append(p("3.3 SAM (Segment Anything Model) \u2014 Pixel-genaue Segmentierung", "H2"))
    story.append(p(
        "SAM verfeinert die DINO-Bounding-Boxes zu pixel-genauen Masken. "
        "Daraus werden exakte Masse berechnet: Hoehe, Breite, Flaeche, Querschnittsreduktion."
    ))
    story.append(make_table(
        ["Eigenschaft", "Wert"],
        [
            ["Modell", "SAM ViT-H (Vision Transformer Huge)"],
            ["Gewichte", "~2.5 GB"],
            ["Input", "Bild + Bounding-Boxes (von DINO)"],
            ["Output", "Pixel-Masken (RLE-kodiert) + Quantifizierung"],
            ["Multi-Box", "Ja \u2014 alle DINO-Detections in einem Request"],
            ["Inferenzzeit", "100\u2013300 ms pro Frame (alle Boxes)"],
        ],
        col_widths=[38*mm, 127*mm]
    ))
    story.append(spacer(2))
    story.append(p("<b>Quantifizierte Masse (MaskQuantificationService):</b>", "Body"))
    story.append(make_table(
        ["Mass", "Einheit", "Verwendung"],
        [
            ["HeightMm / WidthMm", "mm", "Rissbreite, Deformationshoehe"],
            ["ExtentPercent", "%", "Ausdehnung relativ zum Rohrdurchmesser"],
            ["ClockPosition", "Uhr (1\u201312)", "Lage im Rohrquerschnitt"],
            ["IntrusionPercent", "%", "Einragung bei Stutzn/Wurzeln"],
            ["CrossSectionReductionPercent", "%", "Querschnittsverengung"],
            ["MaskAreaPixels", "px", "Absolute Schadenflaeche"],
        ],
        col_widths=[48*mm, 20*mm, 97*mm]
    ))
    story.append(PageBreak())

    # Qwen
    story.append(p("3.4 Qwen2.5-VL \u2014 Vision-Language-Modell", "H2"))
    story.append(p(
        "Qwen2.5-VL ist das zentrale Klassifikationsmodell. Es sieht den Frame als Bild, "
        "erhaelt den Kontext der vorherigen Modelle (DINO-Labels, SAM-Masse) und "
        "liefert strukturierte VSA-Codes im JSON-Format."
    ))
    story.append(make_table(
        ["Eigenschaft", "32B (Workstation)", "7B (Laptop)"],
        [
            ["VRAM", "~26 GB (Q5 Quantisierung)", "~6 GB (Q4 Quantisierung)"],
            ["Context Window", "8192 Tokens", "4096 Tokens"],
            ["Keep-Alive", "24h (bleibt im VRAM)", "24h"],
            ["Inferenzzeit", "1\u20135 Sekunden pro Frame", "0.5\u20132 Sekunden"],
            ["Genauigkeit", "Hoch (Reference-Modell)", "Gut (Fast-Modell)"],
            ["Einsatz", "Primaer + Eskalation", "Primaer (kein Fallback)"],
        ],
        col_widths=[35*mm, 65*mm, 65*mm]
    ))
    story.append(spacer(2))
    story.append(p("<b>Funktionen von Qwen2.5-VL:</b>", "Body"))
    story.append(bullet("VSA-Code-Zuordnung (DIN EN 13508-2 konforme Codes)"))
    story.append(bullet("Meter-OSD-Erkennung (liest Meterzaehler aus dem Videobild)"))
    story.append(bullet("Material-Erkennung (Beton, Steinzeug, PVC, PE, GFK, Stahl)"))
    story.append(bullet("Schweregrad-Bewertung (1\u20135 nach VSA-Skala)"))
    story.append(bullet("Uhrlage-Bestimmung (Position im Rohrquerschnitt)"))
    story.append(bullet("Quantifizierung (Ausdehnung in %, Masse in mm)"))
    story.append(spacer(4))

    # nomic-embed-text
    story.append(p("3.5 nomic-embed-text \u2014 Embedding-Modell", "H2"))
    story.append(p(
        "Fuer die KnowledgeBase wird nomic-embed-text verwendet. Es erzeugt 768-dimensionale "
        "Vektoren aus Textbeschreibungen von Schaeden. Damit findet das System aehnliche "
        "Faelle aus der Vergangenheit (Few-Shot Retrieval)."
    ))
    story.append(make_table(
        ["Eigenschaft", "Wert"],
        [
            ["Modell", "nomic-embed-text (via Ollama)"],
            ["Dimensionen", "768"],
            ["Speicher", "SQLite BLOB (float32-Array)"],
            ["Similarity", "Cosine Similarity"],
            ["Top-K Retrieval", "Default K=5 (konfigurierbar)"],
        ],
        col_widths=[38*mm, 127*mm]
    ))
    story.append(spacer(4))

    # QualityGate
    story.append(p("3.6 QualityGate \u2014 Konfidenz-Fusion (Green/Yellow/Red)", "H2"))
    story.append(p(
        "Die QualityGate fusioniert alle KI-Signale zu einem Composite-Score. "
        "Je nach Ergebnis wird der Fund automatisch akzeptiert (Green), zur Pruefung "
        "vorgelegt (Yellow) oder verworfen (Red)."
    ))
    story.append(make_table(
        ["Signal", "Gewicht", "Quelle"],
        [
            ["YOLO Confidence", "10%", "YOLO Pre-Screening"],
            ["DINO Confidence", "15%", "Grounding DINO Detection"],
            ["SAM Mask Stability", "10%", "SAM Segmentierung"],
            ["Qwen Vision Conf.", "15%", "Qwen2.5-VL Analyse"],
            ["LLM Code Confidence", "20%", "Code-Mapping LLM"],
            ["KB Similarity", "10%", "KnowledgeBase Retrieval"],
            ["KB Code Agreement", "10%", "KB-Code = KI-Code?"],
            ["Plausibility Score", "10%", "Regelbasierte Pruefung"],
        ],
        col_widths=[38*mm, 20*mm, 107*mm]
    ))
    story.append(spacer(2))
    story.append(make_table(
        ["Ampel", "Composite Score", "Aktion"],
        [
            ["Green", "\u2265 0.75", "Automatisch akzeptiert"],
            ["Yellow", "0.45 \u2013 0.74", "Mensch pruefen (Review Queue)"],
            ["Red", "&lt; 0.45", "Verworfen / Eskalation"],
        ],
        col_widths=[25*mm, 40*mm, 100*mm]
    ))
    story.append(PageBreak())

    # ══════════════════════════════════════════════
    # 4. KI-PIPELINE ABLAUF
    # ══════════════════════════════════════════════
    story.append(p("4. KI-Pipeline: Ablauf pro Frame", "H1"))
    story.append(p(
        "Die Multi-Model-Pipeline verarbeitet jeden Frame sequenziell durch bis zu 5 Modelle. "
        "Irrelevante Frames werden frueh uebersprungen (Early Exit)."
    ))
    story.append(spacer(2))

    pipeline_steps = [
        ["1", "Frame-Extraktion", "ffmpeg (persistent)", "CPU", "~0 ms", "PNG-Streaming via stdout, 1 Frame / 3 Sek."],
        ["2", "Base64-Kodierung", "Convert.ToBase64String", "CPU", "~1\u20135 ms", "Bild fuer HTTP-Transport vorbereiten"],
        ["3", "Qualitaetsfilter", "_is_frame_usable()", "CPU", "~5\u201310 ms", "Helligkeit, Schaerfe, Uniformitaet pruefen"],
        ["4", "YOLO Pre-Screen", "YOLO11m-seg", "GPU", "50\u2013200 ms", "Relevanz-Entscheidung (skip wenn irrelevant)"],
        ["5", "DINO Detection", "Grounding DINO 1.5", "GPU", "200\u2013500 ms", "Text-basierte Schadens-Lokalisierung"],
        ["6", "SAM Segmentation", "SAM ViT-H", "GPU", "100\u2013300 ms", "Pixel-genaue Masken + Quantifizierung"],
        ["7", "Qwen Klassifikation", "Qwen2.5-VL", "GPU", "1\u20135 s", "VSA-Code, Meter, Material, Schweregrad"],
        ["8", "Temporal Dedup", "ActiveFindingState", "CPU", "~0.1 ms", "Gleicher Schaden ueber mehrere Frames gruppieren"],
    ]
    story.append(make_table(
        ["#", "Schritt", "Modell/Tool", "Device", "Dauer", "Beschreibung"],
        pipeline_steps,
        col_widths=[8*mm, 28*mm, 32*mm, 12*mm, 22*mm, 63*mm]
    ))
    story.append(spacer(4))

    story.append(p("<b>Pipeline-Modi:</b>", "Body"))
    story.append(make_table(
        ["Modus", "Schritte", "Voraussetzung"],
        [
            ["Multi-Model (empfohlen)", "Alle 8 Schritte (YOLO+DINO+SAM+Qwen)", "Python-Sidecar auf Port 8100 + Ollama"],
            ["Ollama-Only", "Nur Qwen2.5-VL direkt auf Frame", "Nur Ollama (kein Sidecar noetig)"],
            ["Auto", "Versucht Multi-Model, Fallback auf Ollama-Only", "Standard-Einstellung"],
        ],
        col_widths=[38*mm, 62*mm, 65*mm]
    ))
    story.append(spacer(4))

    story.append(p("<b>Post-Processing (nach allen Frames):</b>", "Body"))
    story.append(p(
        "Nach der Frame-Analyse durchlaufen die aggregierten Detections eine zweite Phase:"
    ))
    story.append(bullet("KB-Retrieval: Aehnliche Faelle aus der KnowledgeBase suchen (Embedding + Cosine Similarity)"))
    story.append(bullet("LLM Code-Mapping: Qwen ordnet den endgueltigen VSA-Code zu (mit KB-Beispielen als Kontext)"))
    story.append(bullet("QualityGate: Composite-Score berechnen, Ampel zuweisen (Green/Yellow/Red)"))
    story.append(bullet("Plausibilitaetspruefung: Regelbasierte Validierung gegen erlaubte VSA-Codes"))
    story.append(PageBreak())

    # ══════════════════════════════════════════════
    # 5. ALLE PROGRAMMFUNKTIONEN
    # ══════════════════════════════════════════════
    story.append(p("5. Alle Programmfunktionen", "H1"))

    # 5.1 Projektverwaltung
    story.append(p("5.1 Projektverwaltung", "H2"))
    story.append(p(
        "SewerStudio organisiert Inspektionsdaten in Projekten. Jedes Projekt enthaelt "
        "Haltungen (Kanalabschnitte), Schaechte und zugehoerige Protokolle."
    ))
    story.append(make_table(
        ["Funktion", "Beschreibung"],
        [
            ["Neues Projekt", "Leeres Projekt mit Metadaten erstellen (Zone, Gemeinde, Firma)"],
            ["Projekt oeffnen", "Bestehendes .awpro-Projektfile laden"],
            ["Speichern / Speichern unter", "Projekt persistieren (JSON-basiert)"],
            ["Wiederherstellungspunkte", "Automatische Snapshots (konfigurierbar)"],
            ["Backup / Restore", "Komplettes Projekt-Backup exportieren/importieren"],
            ["Projektmetadaten", "Zone, Gemeinde, Strasse, Firma, Bearbeiter, Auftraggeber"],
        ],
        col_widths=[40*mm, 125*mm]
    ))
    story.append(spacer(4))

    # 5.2 Datenimport
    story.append(p("5.2 Datenimport (5 Formate)", "H2"))
    story.append(make_table(
        ["Format", "Service", "Beschreibung"],
        [
            ["PDF", "PdfImportServiceAdapter", "Inspektionsprotokolle (PDF) mit OCR-Unterstuetzung"],
            ["XTF / SIA 405", "XtfImportServiceAdapter", "Schweizer Standard-Datenaustausch"],
            ["WinCan DB3", "WinCanDbImportService", "WinCan-Projektdatenbanken (SQLite)"],
            ["IBAK", "IbakExportImportService", "IBAK-Inspektions-Exportdateien (Daten.txt)"],
            ["KINS", "KinsImportService", "KINS-Datenformat (kiDVDaten.txt)"],
        ],
        col_widths=[28*mm, 42*mm, 95*mm]
    ))
    story.append(spacer(2))
    story.append(p("<b>Import-Optionen:</b>", "Body"))
    story.append(bullet("Dry-Run Vorschau: Zeigt was importiert wird, ohne Daten zu aendern"))
    story.append(bullet("FillMissingOnly: Nur leere Felder auffuellen, bestehende nicht ueberschreiben"))
    story.append(bullet("Import-Bericht: Zusammenfassung mit Found/Created/Updated/Errors"))
    story.append(spacer(4))

    # 5.3 Haltungsverwaltung
    story.append(p("5.3 Haltungsverwaltung (Kanalabschnitte)", "H2"))
    story.append(p(
        "Die zentrale Datentabelle mit 33 Feldern pro Haltung. "
        "Hier werden alle Inspektionsdaten verwaltet, Videos verknuepft und KI-Analysen gestartet."
    ))
    story.append(make_table(
        ["Funktion", "Beschreibung"],
        [
            ["Hinzufuegen / Entfernen", "Haltungen manuell erstellen oder loeschen"],
            ["Video abspielen", "Integrierter Videoplayer mit Zeitcode-Anzeige"],
            ["KI-Videoanalyse", "Multi-Model-Pipeline auf Video starten"],
            ["Protokoll oeffnen", "Inspektionsprotokoll anzeigen und bearbeiten"],
            ["Video neu verknuepfen", "Fehlende Videolinks manuell oder automatisch korrigieren"],
            ["Massnahmen-Empfehlung", "KI schlaegt Sanierungsmassnahmen vor"],
            ["Sanierungs-Optimierung", "KI optimiert Massnahmen ueber alle Haltungen"],
            ["Kosten oeffnen", "Kostenkalkulator mit Drag-Drop-Interface"],
            ["Dossier drucken", "Gesamtbericht als PDF generieren"],
            ["Live-Suche", "Freitext-Suche ueber alle Felder"],
            ["Zoom / Zeilenhoehe", "Tabellenansicht anpassen (0.5x\u20132.0x)"],
        ],
        col_widths=[38*mm, 127*mm]
    ))
    story.append(spacer(4))

    # 5.4 Schachtverwaltung
    story.append(p("5.4 Schachtverwaltung", "H2"))
    story.append(p(
        "Verwaltung von Kanalschaechten mit eigenen Feldern und optionalem Protokoll. "
        "Gleiche Bedienung wie Haltungen (Hinzufuegen, Entfernen, Suchen, Speichern)."
    ))
    story.append(spacer(4))

    # 5.5 Videoplayer
    story.append(p("5.5 Integrierter Videoplayer", "H2"))
    story.append(make_table(
        ["Funktion", "Beschreibung"],
        [
            ["Video-Formate", "MP4, MKV, AVI und weitere (via VLC/LibVLC)"],
            ["Frame-Navigation", "Frame-fuer-Frame vor/zurueck"],
            ["Zeitcode / Meter", "Anzeige von Videozeit und geschaetztem Meter"],
            ["Zoom und Pan", "Bild vergroessern und verschieben"],
            ["Geschwindigkeit", "Wiedergabegeschwindigkeit anpassen"],
            ["Vollbild", "Maximierte Ansicht"],
            ["Overlay-Werkzeuge", "Messstab, Defekt-Annotation direkt im Video"],
            ["Hardware-Dekodierung", "GPU-beschleunigtes Video-Decoding (konfigurierbar)"],
        ],
        col_widths=[38*mm, 127*mm]
    ))
    story.append(PageBreak())

    # 5.6 Protokollverwaltung
    story.append(p("5.6 Protokollverwaltung und VSA-Code-Katalog", "H2"))
    story.append(make_table(
        ["Funktion", "Beschreibung"],
        [
            ["Code-Katalog (VSA)", "Hierarchische Navigation: Gruppe \u2192 Code \u2192 Char1 \u2192 Char2"],
            ["VSA-Code-Explorer", "Tile-basierte Code-Auswahl mit Live-Validierung"],
            ["Protokoll-Editor", "Einzelne Beobachtung erfassen/aendern (Meter, Code, Uhrlage)"],
            ["Protokoll-Historie", "Original vs. KI-Revision vergleichen, Audit-Trail"],
            ["Beobachtungen-Fenster", "Alle Beobachtungen einer Haltung filtern und bearbeiten"],
            ["Streckenschaden", "Start-/Endmeter fuer ausgedehnte Schaeden"],
            ["Quantifizierung", "Parameter Q1/Q2 je nach Schadensart (%, mm, Anzahl)"],
        ],
        col_widths=[38*mm, 127*mm]
    ))
    story.append(spacer(4))

    # 5.7 VSA-Bewertung
    story.append(p("5.7 VSA-Zustandsbewertung", "H2"))
    story.append(p(
        "Berechnet nach VSA-KEK 2023 die Zustandsnoten fuer jede Haltung:"
    ))
    story.append(make_table(
        ["Note", "Bedeutung", "Berechnung"],
        [
            ["EZS (Strukturell)", "Baulicher Zustand der Leitung", "Gewichteter Schweregrad aller Schaeden"],
            ["EZB (Betrieblich)", "Betriebliche Beeintraechtigung", "Ablagerungen, Hindernisse, Querschnitt"],
            ["EZD (Dringlichkeit)", "Handlungsbedarf", "Kombination aus S und B mit Umfeldfaktoren"],
            ["Zustandsklasse", "Gesamtbewertung 0\u20134", "0=Sehr gut, 1=Gut, 2=Befriedigend, 3=Schlecht, 4=Sehr schlecht"],
        ],
        col_widths=[32*mm, 45*mm, 88*mm]
    ))
    story.append(spacer(4))

    # 5.8 Sanierung & Kosten
    story.append(p("5.8 Sanierung, Kosten und Offerten", "H2"))
    story.append(make_table(
        ["Funktion", "Beschreibung"],
        [
            ["KI-Massnahmen-Empfehlung", "Basierend auf Schadencodes: Inliner, Manschette, Kurzliner, Neubau etc."],
            ["Sanierungs-Optimierung", "KI optimiert Massnahmen ueber das gesamte Projekt"],
            ["Kostenkatalog", "Editierbarer Preiskatalog mit MwSt (JSON)"],
            ["Kostenkalkulator", "Drag-Drop: Massnahmen zuweisen, Preise berechnen"],
            ["Massnahmen-Templates", "Vordefinierte Sanierungspakete"],
            ["Eigendevis", "Automatische Offerte mit Baugruppen und Positionen"],
            ["Export ohne Preis", "Offerte ohne Preisangaben (fuer Ausschreibung)"],
            ["Export mit KV", "Vollstaendiger Kostenvoranschlag als PDF/Excel"],
        ],
        col_widths=[40*mm, 125*mm]
    ))
    story.append(spacer(4))

    # 5.9 Druckcenter
    story.append(p("5.9 Druckcenter und Berichtswesen", "H2"))
    story.append(make_table(
        ["Funktion", "Beschreibung"],
        [
            ["Filter", "Nach Eigentuemer, Sanierungsart, Material, Status, Jahr"],
            ["Protokoll-PDF", "Inspektionsprotokoll als PDF exportieren"],
            ["Dossier", "Gesamtbericht: Daten + Kosten + Massnahmen + Hydraulik"],
            ["Hydraulik-Druck", "Fliessmengen und Druckverluste berechnen und drucken"],
            ["Statistiken", "Inliner (GFK/Nadelfilz), Manschetten, LEM zaehlen"],
            ["Kostenausgabe", "Netto, MwSt, Brutto pro Auswahl"],
        ],
        col_widths=[35*mm, 130*mm]
    ))
    story.append(spacer(4))

    # 5.10 Export
    story.append(p("5.10 Datenexport", "H2"))
    story.append(make_table(
        ["Format", "Beschreibung"],
        [
            ["Excel (Haltungen)", "Alle Haltungsdaten als Haltungen.xlsx (Template-basiert)"],
            ["Excel (Schaechte)", "Alle Schachtdaten als Schaechte.xlsx"],
            ["Ordner-Verteilung", "Haltungen/Schaechte nach Eigentuemer oder Sanierungsart in Ordner verteilen"],
            ["PDF-Protokoll", "Einzelprotokoll oder Sammelprotokoll als PDF"],
            ["Eigendevis Excel", "Offerte als Excel-Arbeitsmappe"],
        ],
        col_widths=[38*mm, 127*mm]
    ))
    story.append(spacer(4))

    # 5.11 Spezial-Funktionen
    story.append(p("5.11 Spezialfunktionen", "H2"))
    story.append(make_table(
        ["Funktion", "Beschreibung"],
        [
            ["Kodiermodus", "Live-Erfassung: Overlay-Werkzeuge, Auto-Codevorschlaege, Scan-Modi"],
            ["Bild-Annotation", "Polygon-/Rechteck-Markierung auf Screenshots mit Farbkodierung"],
            ["Fotomessung", "Laengenmessung in Videos mit Kalibrierung (mm/px)"],
            ["Hydraulik-Panel", "Fliessmengen, Druckverluste, Rohrtyp, Neigung"],
            ["Medienkonflikte", "Fehlende/mehrdeutige Video-Verknuepfungen automatisch aufloesen"],
            ["Medien-Suche", "Videos automatisch suchen und Haltungen zuordnen"],
            ["Guide-System", "Interaktives Tutorial fuer neue Benutzer"],
            ["Fokusmodus (F11)", "Sidebar/Menue ausblenden fuer konzentriertes Arbeiten"],
            ["System-Monitor", "Live-Anzeige: CPU%, RAM%, GPU%, Temperatur"],
            ["Dark/Light Mode", "UI-Theme umschaltbar"],
            ["Diagnose-Seite", "Logdateien, Systeminfo, Debug-Optionen"],
        ],
        col_widths=[35*mm, 130*mm]
    ))
    story.append(PageBreak())

    # ══════════════════════════════════════════════
    # 6. KI-TRAININGSPLAN
    # ══════════════════════════════════════════════
    story.append(p("6. KI-Trainingsplan (Self-Training)", "H1"))
    story.append(p(
        "SewerStudio trainiert sich selbst durch Vergleich mit bestehenden Inspektionsprotokollen. "
        "Die KI analysiert Frames blind (ohne Protokoll-Kontext) und vergleicht ihr Ergebnis "
        "mit dem Protokoll des Inspekteurs. Daraus lernt das System kontinuierlich."
    ))
    story.append(spacer(4))

    story.append(p("6.1 Trainingsablauf im Detail", "H2"))
    story.append(spacer(2))

    story.append(p("<b>Phase 1: PDF-Protokoll-Extraktion (CPU-parallel)</b>", "Body"))
    story.append(p(
        "Aus dem PDF-Inspektionsprotokoll werden Beobachtungen und eingebettete Fotos extrahiert. "
        "Die Fotos dienen als Ground Truth: Das Bild zeigt den Schaden, der Code im Protokoll "
        "ist die korrekte Antwort. Die Extraktion laeuft CPU-parallel (bis 24 Threads)."
    ))
    story.append(spacer(2))

    story.append(p("<b>Phase 2: Blinddurchlauf (GPU)</b>", "Body"))
    story.append(p(
        "Die KI analysiert jedes extrahierte Foto einzeln \u2014 ohne zu wissen was der Inspekteur "
        "codiert hat. Sie durchlaeuft die volle Pipeline: YOLO \u2192 DINO \u2192 SAM \u2192 Qwen "
        "und liefert ihren eigenen VSA-Code-Vorschlag mit Confidence-Score."
    ))
    story.append(spacer(2))

    story.append(p("<b>Phase 3: Deterministischer Vergleich (CPU)</b>", "Body"))
    story.append(p(
        "Der DifferenceAnalyzer vergleicht KI-Ergebnis mit Protokoll-Eintrag und klassifiziert:"
    ))
    story.append(make_table(
        ["Ergebnis", "Bedeutung", "Aktion"],
        [
            ["ExactMatch", "Code stimmt exakt ueberein", "Direkt in KnowledgeBase indexieren"],
            ["PartialMatch", "Code teilweise korrekt (z.B. BAA statt BAB)", "Auto-Approve wenn Score \u2265 0.60"],
            ["Mismatch", "Code falsch", "In Review-Queue, Mensch entscheidet"],
            ["NoFindings", "KI hat nichts gefunden, Protokoll hat Eintrag", "FN-Analyse, Schwierigkeitsgrad pruefen"],
        ],
        col_widths=[28*mm, 50*mm, 87*mm]
    ))
    story.append(spacer(2))

    story.append(p("<b>Phase 4: Qualitaetskontrolle (QualityGate)</b>", "Body"))
    story.append(p(
        "Jedes Sample durchlaeuft die QualityGate. Nur Samples mit genuegend Konfidenz "
        "werden automatisch gelernt. Der Rest geht in die Review-Queue:"
    ))
    story.append(make_table(
        ["Kriterium", "Schwelle", "Beschreibung"],
        [
            ["Confidence", "\u2265 0.92", "KI ist sich sehr sicher"],
            ["QualityGate", "Green", "Composite-Score \u2265 0.75"],
            ["KB Agreement", "true", "KnowledgeBase bestaetigt den Code"],
            ["Epistemic Uncertainty", "&lt; 0.15", "Geringe Modellunsicherheit"],
        ],
        col_widths=[38*mm, 25*mm, 102*mm]
    ))
    story.append(spacer(2))

    story.append(p("<b>Phase 5: Review-Queue (Human-in-the-Loop)</b>", "Body"))
    story.append(p(
        "Samples in der Yellow-Zone werden nach Prioritaet sortiert. "
        "Der Mensch prueft und genehmigt oder verwirft. Die Priorisierung nutzt Active Learning:"
    ))
    story.append(bullet("60% Uncertainty Sampling: Samples mit hoechster Unsicherheit zuerst"))
    story.append(bullet("40% Diversity Sampling: Seltene Codes (unterrepraesentierte Kategorien) bevorzugt"))
    story.append(spacer(2))

    story.append(p("<b>Phase 6: KnowledgeBase-Indexierung</b>", "Body"))
    story.append(p(
        "Genehmigte Samples werden in die KnowledgeBase aufgenommen: "
        "Text-Embedding via nomic-embed-text, Speicherung in SQLite. "
        "Vor der Indexierung prueft der KbDeduplicationService auf Duplikate "
        "(Cosine Similarity &gt; 0.95 = Duplikat)."
    ))
    story.append(spacer(2))

    story.append(p("<b>Phase 7: Benchmark-Tracking</b>", "Body"))
    story.append(p(
        "Nach jedem Trainingsblock wird ein Benchmark-Score berechnet: "
        "ExactMatch-Rate, PartialMatch-Rate, Mismatch-Rate. "
        "Diese Zeitreihe zeigt ob das System ueber die Zeit besser wird."
    ))
    story.append(spacer(4))

    story.append(p("6.2 Batch-Selbsttraining (ueber Nacht)", "H2"))
    story.append(p(
        "Im Batch-Modus verarbeitet SewerStudio automatisch hunderte Inspektionsfaelle. "
        "Das System laeuft ueber Nacht und lernt aus dem gesamten Archiv:"
    ))
    story.append(bullet("PDF-Fotos aller Faelle vorab extrahieren (CPU-parallel)"))
    story.append(bullet("Mehrere Faelle gleichzeitig analysieren (CaseParallelism)"))
    story.append(bullet("Automatische KB-Indexierung bei hoher Konfidenz"))
    story.append(bullet("Review-Queue fuer unsichere Faelle (morgens pruefen)"))
    story.append(bullet("Benchmark-Report am Ende des Batch-Laufs"))
    story.append(spacer(4))

    story.append(p("6.3 Few-Shot Learning", "H2"))
    story.append(p(
        "Die KnowledgeBase dient als Few-Shot-Speicher. Bei jeder neuen Analyse "
        "sucht das System aehnliche Faelle aus der KB und gibt sie als Beispiele "
        "in den Qwen-Prompt. So lernt die KI aus vergangenen Entscheidungen:"
    ))
    story.append(bullet("Query: Aktueller Schaden (Label, Meter, Schweregrad, Uhrlage, Masse)"))
    story.append(bullet("Retrieval: Top-3 aehnliche Faelle per Cosine Similarity"))
    story.append(bullet("Kontext: Beispiele werden als In-Context-Learning dem LLM gegeben"))
    story.append(bullet("Meter-Gewichtung: Naehere Schaeden werden hoeher gewichtet"))
    story.append(spacer(4))

    story.append(p("6.4 Gewichts-Lernen (CategoryWeights)", "H2"))
    story.append(p(
        "Die QualityGate-Gewichte werden pro Schadenskategorie optimiert. "
        "Wenn z.B. fuer Risse (BAA\u2013BAF) DINO besonders zuverlaessig ist, "
        "erhoecht das System automatisch das DINO-Gewicht fuer diese Kategorie. "
        "Bayesianische Optimierung mit Validierung."
    ))
    story.append(PageBreak())

    # ══════════════════════════════════════════════
    # 7. NUTZEN UND ERGEBNISSE
    # ══════════════════════════════════════════════
    story.append(p("7. Nutzen und Ergebnisse", "H1"))

    story.append(p("7.1 Zeitersparnis", "H2"))
    story.append(make_table(
        ["Aufgabe", "Manuell", "Mit SewerStudio KI", "Ersparnis"],
        [
            ["Video-Protokollierung (1 Haltung)", "15\u201330 Min", "3\u20135 Min (Review)", "~80%"],
            ["VSA-Bewertung (100 Haltungen)", "1\u20132 Tage", "Minuten (automatisch)", "~95%"],
            ["Sanierungsplanung", "Stunden", "Sekunden (KI-Vorschlag)", "~90%"],
            ["Kostenofferte", "30\u201360 Min", "5 Min (Template + KI)", "~85%"],
        ],
        col_widths=[42*mm, 28*mm, 42*mm, 23*mm]
    ))
    story.append(spacer(4))

    story.append(p("7.2 Qualitaetsverbesserung", "H2"))
    story.append(bullet("Konsistenz: KI codiert gleiche Schaeden immer gleich (kein Inspekteur-Bias)"))
    story.append(bullet("Vollstaendigkeit: Kein Schaden wird uebersprungen (Frame-fuer-Frame Analyse)"))
    story.append(bullet("Nachvollziehbarkeit: QualityGate-Score, Evidence-Vektor, KB-Referenzen dokumentiert"))
    story.append(bullet("Kontinuierliche Verbesserung: Self-Training-Loop verbessert Genauigkeit mit jedem Batch"))
    story.append(spacer(4))

    story.append(p("7.3 Nutzen des Self-Training", "H2"))
    story.append(make_table(
        ["Aspekt", "Ohne Self-Training", "Mit Self-Training"],
        [
            ["Genauigkeit", "Basis-Genauigkeit der LLM-Modelle", "Steigt mit jedem verarbeiteten Fall"],
            ["Kontextwissen", "Nur allgemeines Wissen", "Lernt projektspezifische Muster"],
            ["Seltene Codes", "Oft falsch klassifiziert", "KB-Beispiele verbessern Erkennung"],
            ["Inspekteur-Stil", "Nicht angepasst", "Adaptiert sich an lokale Konventionen"],
            ["Aufwand", "Kein Aufwand", "Laeuft ueber Nacht, Review ~30 Min/Morgen"],
        ],
        col_widths=[30*mm, 55*mm, 80*mm]
    ))
    story.append(spacer(4))

    story.append(p("7.4 Datenschutz und Unabhaengigkeit", "H2"))
    story.append(bullet("Alle Daten bleiben lokal \u2014 keine Cloud, kein Internet noetig"))
    story.append(bullet("Alle KI-Modelle laufen auf eigener Hardware"))
    story.append(bullet("Kein Vendor-Lock-in: Open-Source Modelle (YOLO, SAM, Qwen via Ollama)"))
    story.append(bullet("Volle Kontrolle ueber Modelle, Schwellen und Gewichtungen"))
    story.append(PageBreak())

    # ══════════════════════════════════════════════
    # 8. UMGEBUNGSVARIABLEN
    # ══════════════════════════════════════════════
    story.append(p("8. Umgebungsvariablen (Referenz)", "H1"))
    story.append(p(
        "SewerStudio wird ueber Umgebungsvariablen konfiguriert. "
        "Alle Variablen sind optional \u2014 Defaults sind fuer die meisten Setups ausreichend."
    ))
    story.append(spacer(2))

    story.append(p("<b>Ollama / KI-Konfiguration:</b>", "Body"))
    story.append(make_table(
        ["Variable", "Default", "Beschreibung"],
        [
            ["SEWERSTUDIO_AI_ENABLED", "false", "KI-Features aktivieren"],
            ["SEWERSTUDIO_OLLAMA_URL", "http://localhost:11434", "Ollama-Server-Adresse"],
            ["SEWERSTUDIO_AI_VISION_MODEL", "auto", "Vision-Modell (auto = GPU-basierte Auswahl)"],
            ["SEWERSTUDIO_AI_EMBED_MODEL", "nomic-embed-text", "Embedding-Modell fuer KB"],
            ["SEWERSTUDIO_AI_TIMEOUT_MIN", "5", "Ollama Request-Timeout (Minuten)"],
            ["SEWERSTUDIO_OLLAMA_KEEP_ALIVE", "24h", "Modell im VRAM halten"],
            ["SEWERSTUDIO_OLLAMA_NUM_CTX", "8192", "Context Window (Tokens)"],
            ["OLLAMA_NUM_PARALLEL", "4", "Parallele Ollama-Slots"],
        ],
        col_widths=[52*mm, 35*mm, 78*mm]
    ))
    story.append(spacer(4))

    story.append(p("<b>Pipeline / Sidecar:</b>", "Body"))
    story.append(make_table(
        ["Variable", "Default", "Beschreibung"],
        [
            ["SEWERSTUDIO_PIPELINE_MODE", "ollamaonly", "auto / multimodel / ollamaonly"],
            ["SEWERSTUDIO_MULTIMODEL_ENABLED", "false", "Multi-Model-Pipeline aktivieren"],
            ["SEWERSTUDIO_SIDECAR_URL", "http://localhost:8100", "Python-Sidecar-Adresse"],
            ["SEWERSTUDIO_SIDECAR_TIMEOUT_SEC", "300", "Sidecar Request-Timeout"],
            ["SEWERSTUDIO_YOLO_CONFIDENCE", "0.25", "YOLO Confidence-Schwelle"],
            ["SEWERSTUDIO_DINO_BOX_THRESHOLD", "0.30", "DINO Box-Schwelle"],
            ["SEWERSTUDIO_DINO_TEXT_THRESHOLD", "0.25", "DINO Text-Schwelle"],
            ["SEWERSTUDIO_PIPE_DIAMETER_MM", "\u2014", "Rohrdurchmesser-Override fuer SAM"],
        ],
        col_widths=[52*mm, 35*mm, 78*mm]
    ))
    story.append(spacer(4))

    story.append(p("<b>Self-Training:</b>", "Body"))
    story.append(make_table(
        ["Variable", "Default", "Beschreibung"],
        [
            ["SEWERSTUDIO_GPU_CONCURRENCY", "auto (VRAM)", "Parallele GPU-Requests"],
            ["SEWERSTUDIO_SELFTRAIN_PREEXTRACT_PARALLELISM", "CPU-2", "CPU-Threads fuer PDF-Extraktion"],
            ["SEWERSTUDIO_SELFTRAIN_CASE_PARALLELISM", "auto (VRAM)", "Gleichzeitige Inspektionsfaelle"],
        ],
        col_widths=[62*mm, 28*mm, 75*mm]
    ))

    # Schluss
    story.append(Spacer(1, 20*mm))
    story.append(hr())
    story.append(p(
        "<i>Dieser Bericht wurde automatisch aus der SewerStudio-Codebasis generiert. "
        "Stand: April 2026, Version 3.1.</i>",
        "Caption"
    ))

    return story


def on_first_page(canvas_obj, doc):
    canvas_obj.saveState()
    canvas_obj.setFont("Helvetica", 7)
    canvas_obj.setFillColor(HexColor("#718096"))
    canvas_obj.drawCentredString(A4[0] / 2, 15*mm,
        "SewerStudio Programmbericht \u2014 Vertraulich")
    canvas_obj.restoreState()


def on_later_pages(canvas_obj, doc):
    canvas_obj.saveState()
    canvas_obj.setFont("Helvetica", 7)
    canvas_obj.setFillColor(HexColor("#718096"))
    canvas_obj.drawCentredString(A4[0] / 2, 15*mm,
        f"SewerStudio Programmbericht \u2014 Seite {doc.page}")
    canvas_obj.restoreState()


def main():
    doc = SimpleDocTemplate(
        OUTPUT_PDF,
        pagesize=A4,
        topMargin=20*mm,
        bottomMargin=22*mm,
        leftMargin=18*mm,
        rightMargin=18*mm,
        title="SewerStudio Programmbericht",
        author="SewerStudio KI",
        subject="Hardware, KI-Modelle, Funktionen, Trainingsplan",
    )

    story = build_story()
    doc.build(story, onFirstPage=on_first_page, onLaterPages=on_later_pages)
    print(f"PDF erstellt: {OUTPUT_PDF}")
    print(f"Groesse: {os.path.getsize(OUTPUT_PDF) / 1024:.0f} KB")


if __name__ == "__main__":
    main()
