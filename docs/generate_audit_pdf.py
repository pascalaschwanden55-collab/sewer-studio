"""
SewerStudio 4.0 — KI-Training Audit PDF Generator
Erstellt am 2026-04-11
"""
from reportlab.lib.pagesizes import A4
from reportlab.lib.units import mm, cm
from reportlab.lib.styles import getSampleStyleSheet, ParagraphStyle
from reportlab.lib.colors import HexColor, black, white, Color
from reportlab.lib.enums import TA_LEFT, TA_CENTER, TA_RIGHT, TA_JUSTIFY
from reportlab.platypus import (
    SimpleDocTemplate, Paragraph, Spacer, Table, TableStyle,
    PageBreak, KeepTogether, HRFlowable
)
from reportlab.pdfgen import canvas
from reportlab.lib import colors
import os

# --- Farbpalette ---
DARK_BG = HexColor("#1a1a2e")
ACCENT_BLUE = HexColor("#0f3460")
ACCENT_TEAL = HexColor("#16213e")
HEADER_BG = HexColor("#e94560")
GREEN = HexColor("#2d6a4f")
YELLOW_WARN = HexColor("#b5651d")
RED_CRIT = HexColor("#c1121f")
LIGHT_GRAY = HexColor("#f0f0f5")
MID_GRAY = HexColor("#6c757d")
TABLE_HEADER_BG = HexColor("#16213e")
TABLE_ALT_ROW = HexColor("#f8f9fa")
BLOCKER_BG = HexColor("#fde8e8")
HIGH_BG = HexColor("#fff3e0")
MEDIUM_BG = HexColor("#fffde7")
GREEN_BG = HexColor("#e8f5e9")

OUTPUT_PATH = os.path.join(os.path.dirname(__file__), "SewerStudio_KI_Audit_2026-04-11.pdf")

# --- Styles ---
styles = getSampleStyleSheet()

style_title = ParagraphStyle(
    "AuditTitle", parent=styles["Title"],
    fontSize=22, leading=28, textColor=ACCENT_BLUE,
    spaceAfter=6, fontName="Helvetica-Bold"
)
style_subtitle = ParagraphStyle(
    "AuditSubtitle", parent=styles["Normal"],
    fontSize=11, leading=14, textColor=MID_GRAY,
    spaceAfter=16, fontName="Helvetica"
)
style_h1 = ParagraphStyle(
    "AuditH1", parent=styles["Heading1"],
    fontSize=16, leading=20, textColor=ACCENT_BLUE,
    spaceBefore=18, spaceAfter=8, fontName="Helvetica-Bold",
    borderWidth=0, borderPadding=0,
)
style_h2 = ParagraphStyle(
    "AuditH2", parent=styles["Heading2"],
    fontSize=13, leading=16, textColor=ACCENT_TEAL,
    spaceBefore=12, spaceAfter=6, fontName="Helvetica-Bold"
)
style_h3 = ParagraphStyle(
    "AuditH3", parent=styles["Heading3"],
    fontSize=11, leading=14, textColor=HexColor("#333"),
    spaceBefore=8, spaceAfter=4, fontName="Helvetica-Bold"
)
style_body = ParagraphStyle(
    "AuditBody", parent=styles["Normal"],
    fontSize=9, leading=12.5, textColor=HexColor("#222"),
    spaceAfter=4, fontName="Helvetica", alignment=TA_JUSTIFY
)
style_body_small = ParagraphStyle(
    "AuditBodySmall", parent=style_body,
    fontSize=8, leading=11
)
style_code = ParagraphStyle(
    "AuditCode", parent=styles["Code"],
    fontSize=7.5, leading=10, textColor=HexColor("#333"),
    backColor=LIGHT_GRAY, borderWidth=0.5, borderColor=HexColor("#ddd"),
    borderPadding=4, spaceAfter=6, fontName="Courier",
    leftIndent=8, rightIndent=8
)
style_bullet = ParagraphStyle(
    "AuditBullet", parent=style_body,
    leftIndent=16, bulletIndent=8, spaceBefore=1, spaceAfter=1
)
style_finding = ParagraphStyle(
    "Finding", parent=style_body,
    leftIndent=8, fontSize=8.5, leading=11.5
)
style_table_header = ParagraphStyle(
    "TableHeader", parent=styles["Normal"],
    fontSize=8, leading=10, textColor=white,
    fontName="Helvetica-Bold", alignment=TA_LEFT
)
style_table_cell = ParagraphStyle(
    "TableCell", parent=styles["Normal"],
    fontSize=8, leading=10, textColor=HexColor("#222"),
    fontName="Helvetica"
)
style_table_cell_small = ParagraphStyle(
    "TableCellSmall", parent=style_table_cell,
    fontSize=7, leading=9
)
style_footer = ParagraphStyle(
    "Footer", parent=styles["Normal"],
    fontSize=7, textColor=MID_GRAY, alignment=TA_CENTER
)


def make_table(headers, rows, col_widths=None, alt_colors=True):
    """Erstellt eine formatierte Tabelle."""
    data = [[Paragraph(h, style_table_header) for h in headers]]
    for row in rows:
        data.append([Paragraph(str(c), style_table_cell) for c in row])

    w = col_widths or [None] * len(headers)
    t = Table(data, colWidths=w, repeatRows=1)

    cmds = [
        ("BACKGROUND", (0, 0), (-1, 0), TABLE_HEADER_BG),
        ("TEXTCOLOR", (0, 0), (-1, 0), white),
        ("FONTNAME", (0, 0), (-1, 0), "Helvetica-Bold"),
        ("FONTSIZE", (0, 0), (-1, 0), 8),
        ("BOTTOMPADDING", (0, 0), (-1, 0), 6),
        ("TOPPADDING", (0, 0), (-1, 0), 6),
        ("LEFTPADDING", (0, 0), (-1, -1), 5),
        ("RIGHTPADDING", (0, 0), (-1, -1), 5),
        ("GRID", (0, 0), (-1, -1), 0.4, HexColor("#ccc")),
        ("VALIGN", (0, 0), (-1, -1), "TOP"),
    ]
    if alt_colors:
        for i in range(1, len(data)):
            if i % 2 == 0:
                cmds.append(("BACKGROUND", (0, i), (-1, i), TABLE_ALT_ROW))
    t.setStyle(TableStyle(cmds))
    return t


def severity_badge(sev):
    """Gibt farbigen Severity-Text zurueck."""
    colors_map = {
        "BLOCKER": ("#c1121f", "#fff"),
        "HIGH": ("#b5651d", "#fff"),
        "MEDIUM": ("#7b6d00", "#fff"),
        "LOW": ("#2d6a4f", "#fff"),
        "INFO": ("#555", "#fff"),
    }
    bg, fg = colors_map.get(sev, ("#555", "#fff"))
    return f'<font color="{bg}"><b>[{sev}]</b></font>'


def header_footer(canvas_obj, doc):
    """Seitenheader und -footer."""
    canvas_obj.saveState()
    # Header-Linie
    canvas_obj.setStrokeColor(ACCENT_BLUE)
    canvas_obj.setLineWidth(1.5)
    canvas_obj.line(20 * mm, A4[1] - 18 * mm, A4[0] - 20 * mm, A4[1] - 18 * mm)

    canvas_obj.setFont("Helvetica", 7)
    canvas_obj.setFillColor(MID_GRAY)
    canvas_obj.drawString(20 * mm, A4[1] - 16 * mm, "SewerStudio 4.0 — KI-Training Audit")
    canvas_obj.drawRightString(A4[0] - 20 * mm, A4[1] - 16 * mm, "11. April 2026")

    # Footer
    canvas_obj.setStrokeColor(HexColor("#ddd"))
    canvas_obj.setLineWidth(0.5)
    canvas_obj.line(20 * mm, 14 * mm, A4[0] - 20 * mm, 14 * mm)
    canvas_obj.setFont("Helvetica", 7)
    canvas_obj.drawString(20 * mm, 10 * mm, "Vertraulich — Nur fuer internen Gebrauch")
    canvas_obj.drawRightString(A4[0] - 20 * mm, 10 * mm, f"Seite {doc.page}")
    canvas_obj.restoreState()


def build_pdf():
    doc = SimpleDocTemplate(
        OUTPUT_PATH,
        pagesize=A4,
        topMargin=24 * mm,
        bottomMargin=20 * mm,
        leftMargin=20 * mm,
        rightMargin=20 * mm,
        title="SewerStudio 4.0 - KI-Training Audit",
        author="Claude Code Audit System",
        subject="Vollstaendiges Code-Audit der KI-Training-Pipeline"
    )

    story = []
    W = A4[0] - 40 * mm  # Verfuegbare Breite

    # =====================================================================
    # TITELSEITE
    # =====================================================================
    story.append(Spacer(1, 40 * mm))
    story.append(Paragraph("SewerStudio 4.0", style_title))
    story.append(Paragraph("KI-Training — Vollstaendiges Code-Audit", ParagraphStyle(
        "BigSub", parent=style_h1, fontSize=18, textColor=ACCENT_TEAL, spaceBefore=2
    )))
    story.append(Spacer(1, 8 * mm))
    story.append(HRFlowable(width="100%", thickness=1.5, color=ACCENT_BLUE))
    story.append(Spacer(1, 6 * mm))

    meta_data = [
        ["Datum", "11. April 2026"],
        ["Scope", "Self-Training, YOLO-Retrain, LoRA, Knowledge Base, Benchmark"],
        ["Methode", "6 parallele Code-Analyse-Agenten + Live-Runtime-Check"],
        ["Reviews", "Claude Opus (statisch) + Gemini (Architektur) + ChatGPT (Bugs) + Live-Audit"],
        ["Hardware", "Intel Core Ultra 9 285K, ASUS RTX 5090 32GB, 64GB DDR5"],
        ["Projekt", "Solo-Entwickler, keine kommerzielle Nutzung"],
    ]
    story.append(make_table(["Parameter", "Wert"], meta_data, col_widths=[35 * mm, W - 35 * mm]))
    story.append(Spacer(1, 10 * mm))

    story.append(Paragraph("<b>Go/No-Go Bewertung:</b>", style_h2))
    story.append(Paragraph(
        '<font color="#c1121f"><b>No-Go fuer Produktion</b></font> — '
        'Sidecar im Maintenance-Mode blockiert, 0 BBox-Daten, Benchmark nicht aktiv. '
        'Architektur und Konzept sind exzellent, '
        'Implementierung hat punktuelle Thread-Sicherheits- und Datenqualitaets-Luecken.',
        style_body
    ))

    story.append(PageBreak())

    # =====================================================================
    # INHALTSVERZEICHNIS
    # =====================================================================
    story.append(Paragraph("Inhaltsverzeichnis", style_h1))
    story.append(Spacer(1, 4 * mm))
    toc_items = [
        "1. Gesamtbewertung",
        "2. Live-Runtime-Status",
        "3. Severity-Uebersicht",
        "4. BLOCKER-Findings (12)",
        "5. HIGH-Findings (18)",
        "6. MEDIUM-Findings (16)",
        "7. Sicherheits-Findings",
        "8. Test-Abdeckung",
        "9. Konsolidierung: 4 Reviews im Vergleich",
        "10. Empfohlene Prioritaeten",
        "11. Positiv-Bewertung",
    ]
    for item in toc_items:
        story.append(Paragraph(item, ParagraphStyle(
            "TOC", parent=style_body, fontSize=10, leading=16, leftIndent=10
        )))
    story.append(PageBreak())

    # =====================================================================
    # 1. GESAMTBEWERTUNG
    # =====================================================================
    story.append(Paragraph("1. Gesamtbewertung", style_h1))

    bewertung = [
        ["Architektur und Konzept", "Exzellent", "MLOps-Level, Thin-AI sauber durchgesetzt"],
        ["Datenintegritaet (Stores)", "Gut", "Atomic Writes, Backup-Rotation vorhanden"],
        ["Fehlerbehandlung", "Mittel", "Viele Silent-Failures, fehlendes Logging"],
        ["Thread-Sicherheit", "Schwach", "Mehrere Race Conditions und Deadlock-Risiken"],
        ["Test-Abdeckung", "Schwach", "35+ kritische Klassen ohne Tests"],
        ["Sidecar (Python)", "Schwach", "Guard-Luecken, Path Traversal, VRAM-Risiken"],
        ["Trainingsqualitaet (Daten)", "Ungenuegend", "0 BBox-Samples, Benchmark nicht aktiv"],
    ]
    story.append(make_table(
        ["Kategorie", "Bewertung", "Details"],
        bewertung,
        col_widths=[40 * mm, 22 * mm, W - 62 * mm]
    ))
    story.append(Spacer(1, 6 * mm))

    # =====================================================================
    # 2. LIVE-RUNTIME-STATUS
    # =====================================================================
    story.append(Paragraph("2. Live-Runtime-Status (11.04.2026)", style_h1))
    story.append(Paragraph(
        "Direkter Health-Check gegen den laufenden Sidecar auf localhost:8100:",
        style_body
    ))
    story.append(Spacer(1, 2 * mm))

    runtime_data = [
        ["Sidecar Status", "maintenance_reason = training", "BLOCKIERT"],
        ["GPU VRAM", "0.54 GB allokiert (Modelle entladen)", "Keine Inferenz"],
        ["YOLO Modell", "resolved_model_path = null", "Kein Modell geladen"],
        ["Custom Weights", "custom_weights_present = false", "Nur Basis-YOLO"],
        ["Inferenz-Test", "/detect/yolo liefert 409 Conflict", "Komplett blockiert"],
        ["Smoke-Test", "161 Frames, 0 Detektionen", "Keine Erkennung"],
    ]
    story.append(make_table(
        ["Komponente", "Wert", "Auswirkung"],
        runtime_data,
        col_widths=[32 * mm, W - 62 * mm, 30 * mm]
    ))
    story.append(Spacer(1, 3 * mm))

    runtime_data2 = [
        ["Approved Samples", "15.417", "Status OK"],
        ["HasBbox = true", "0", "Auto-Retrain unmoeglich"],
        ["SourceType VideoTimestamp", "32", "Zu wenig fuer YOLO"],
        ["Benchmark-Set (aktiv)", "1 Haltung", "Braucht 20"],
        ["Benchmark-Metriken", "Datei fehlt", "Kein Verlauf"],
        ["Benchmark-Liste (tools/)", "20 Haltungen", "Nicht importiert"],
    ]
    story.append(make_table(
        ["Datenpunkt", "Wert", "Bewertung"],
        runtime_data2,
        col_widths=[42 * mm, 30 * mm, W - 72 * mm]
    ))

    story.append(PageBreak())

    # =====================================================================
    # 3. SEVERITY-UEBERSICHT
    # =====================================================================
    story.append(Paragraph("3. Severity-Uebersicht", style_h1))

    sev_data = [
        ["BLOCKER", "12", "Deadlocks, Race Conditions, fehlende Tests, Datenkorruption"],
        ["HIGH", "18", "Deploy-Risiken, VRAM-Probleme, stille Datenverluste"],
        ["MEDIUM", "25+", "Hardcoded Werte, fehlende Validierung, Code Smells"],
        ["LOW", "10+", "Kosmetik, fehlende Kommentare, Edge Cases"],
    ]
    story.append(make_table(
        ["Severity", "Anzahl", "Beschreibung"],
        sev_data,
        col_widths=[22 * mm, 15 * mm, W - 37 * mm]
    ))
    story.append(Spacer(1, 6 * mm))

    # =====================================================================
    # 4. BLOCKER-FINDINGS
    # =====================================================================
    story.append(Paragraph("4. BLOCKER-Findings (12)", style_h1))

    blockers = [
        ("B1", "Deadlock Pause/Resume", "VideoSelfTrainingOrchestrator.cs:188\nBatchSelfTrainingOrchestrator.cs:564",
         "Pause() ruft synchron Wait() auf SemaphoreSlim. Mehrere Threads haengen permanent bei nur einem Resume().",
         "ManualResetEventSlim verwenden"),
        ("B2", "Race Condition TryConsumeBudget", "SelfTrainingOrchestrator.cs:598",
         "Stack-lokaler int per ref an parallele Threads. Volatile/CAS auf Stack bietet keine Thread-Sicherheit.",
         "Interlocked.Decrement oder Klassenfeld"),
        ("B3", "Kein Mutex auf Retrain", "YoloRetrainOrchestrator.cs:47",
         "RunIfEligibleAsync ohne Semaphore. Parallele Retrains konkurrieren um VRAM und Manifest.",
         "SemaphoreSlim(1,1) um Methode"),
        ("B4", "LoRA Guard nicht released", "lora_training.py:313",
         "Thread-Start-Fehler setzt _active_job_id zurueck, aber end_training_guard() fehlt.",
         "Guard im Exception-Handler freigeben"),
        ("B5", "Race in _active_job_id", "lora_training.py:281",
         "Lock-Release vor begin_training_guard(). Zweiter Request kann durchkommen.",
         "Lock ueber Guard-Acquisition halten"),
        ("B6", "Persist-Fehler nach Reload", "YoloRetrainOrchestrator.cs:240",
         "Reload gelingt, SaveManifest schlaegt fehl. Manifest zeigt auf altes Modell nach Neustart.",
         "2-Phase-Commit oder Fehler propagieren"),
        ("B7", "Embedding-Modell nicht geprueft", "EmbeddingService.cs:44",
         "Requests an Ollama ohne Model-Availability-Check. Batch-Indexierung laeuft leer.",
         "Model-Check bei Startup"),
        ("B8", "SettingsStore nicht atomar", "TrainingCenterSettingsStore.cs:34",
         "File.Create + Serialize ohne Temp-File. Crash = korrupte Settings.",
         "Atomic-Write-Pattern kopieren"),
        ("B9", "DifferenceAnalyzer ungetestet", "DifferenceAnalyzer.cs (290 Zeilen)",
         "Komplexer Greedy-Matching mit potenziellem Double-Assignment. Kein einziger Test.",
         "Tests erstellen"),
        ("B10", "BenchmarkRunner ungetestet", "BenchmarkRunner.cs:182",
         "CodeMismatch als FN + FP gezaehlt. Doppelte Zaehlung bei gleichem Praefix moeglich.",
         "Tests erstellen"),
        ("B11", "Keine CI/CD Pipeline", "Kein .github/workflows/",
         "Tests werden nie automatisch ausgefuehrt. Regressionen bleiben unentdeckt.",
         "GitHub Actions einrichten"),
        ("B12", "FrameQualityFilter-State", "FrameQualityFilter.cs:68",
         "lastHash persistiert zwischen Videos. Erster Frame des neuen Videos faelschlich als Duplikat.",
         "Auto-Reset zwischen Videos"),
    ]

    for bid, title, loc, problem, fix in blockers:
        story.append(KeepTogether([
            Paragraph(f'{severity_badge("BLOCKER")} {bid}: {title}', style_h3),
            Paragraph(f'<font color="#666"><i>Datei: {loc}</i></font>', style_finding),
            Paragraph(f'<b>Problem:</b> {problem}', style_finding),
            Paragraph(f'<b>Fix:</b> {fix}', style_finding),
            Spacer(1, 3 * mm),
        ]))

    story.append(PageBreak())

    # =====================================================================
    # 5. HIGH-FINDINGS
    # =====================================================================
    story.append(Paragraph("5. HIGH-Findings (18)", style_h1))

    high_findings = [
        ["H1", "SelfTrainingOrchestrator.cs:319", "Auto-Approve prueft weder Meter noch Clock"],
        ["H2", "VideoSelfTrainingOrchestrator.cs:33", "Race Condition auf volatile _isPaused"],
        ["H3", "BatchSelfTrainingOrchestrator.cs:634", "Sidecar-Prozess nicht disposed"],
        ["H4", "BatchSelfTrainingOrchestrator.cs:754", "History-Rotation ohne Lock"],
        ["H5", "DifferenceAnalyzer.cs:42", "Greedy Assignment suboptimal bei nahen Befunden"],
        ["H6", "KnowledgeBaseManager.cs:59", "Embedding-Fehler: Sample-State inkonsistent"],
        ["H7", "EmbeddingService.cs:54", "Kein explizites Timeout fuer Ollama"],
        ["H8", "EmbeddingService.cs:70", "JSON-Parsing ohne Bounds-Check"],
        ["H9", "KbDeduplicationService.cs:60", "Leere Embeddings: Dedup deaktiviert"],
        ["H10", "KbEnrichmentService.cs:269", "CaseId-Kollision bei gleicher Tageszeit"],
        ["H11", "RetrievalService.cs:50", "Dimension-Mismatch still ignoriert"],
        ["H12", "RetrievalService.cs:186", "Rohrmaterial nicht im SQL-Query"],
        ["H13", "SampleQualityGateService.cs:34", "ZeroMeterCodes-Liste unvollstaendig"],
        ["H14", "YoloRetrainOrchestrator.cs:370", "Pruning kann aktives Modell loeschen"],
        ["H15", "YoloRetrainOrchestrator.cs:410", "Manifest-Korruption: stille leere Rueckgabe"],
        ["H16", "BenchmarkMetricsStore.cs:105", "Regression-Threshold bricht bei avgF1=0"],
        ["H17", "lora_training.py:179", "Keine VRAM-Pruefung vor LoRA-Training"],
        ["H18", "lora_training.py:345", "Path Traversal in deploy-lora Endpoint"],
    ]
    story.append(make_table(
        ["#", "Datei:Zeile", "Problem"],
        high_findings,
        col_widths=[10 * mm, 55 * mm, W - 65 * mm]
    ))

    story.append(PageBreak())

    # =====================================================================
    # 6. MEDIUM-FINDINGS
    # =====================================================================
    story.append(Paragraph("6. MEDIUM-Findings (Auswahl)", style_h1))

    medium_findings = [
        ["M1", "SelfTrainingComparisonService.cs:28", "Hardcoded Toleranzen (Meter=1.0, Clock=1)"],
        ["M2", "SelfTrainingComparisonService.cs:180", "Overly-breiter Praefix-Match"],
        ["M3", "SelfTrainingComparisonService.cs:277", "Clock-Parsing: '12:00 Uhr' nicht unterstuetzt"],
        ["M4", "DifferenceAnalyzer.cs:156", "Score-Bonus 0.075 bei fehlender Uhrlage"],
        ["M5", "DifferenceAnalyzer.cs:284", "Streckenschaden: Midpoint statt Range-Check"],
        ["M6", "KnowledgeBaseContext.cs:61", "Fehlender Index auf Embeddings JOIN"],
        ["M7", "KnowledgeBaseContext.cs:43", "PRAGMA foreign_keys nicht aktiviert"],
        ["M8", "TrainingCenterSettings.cs:50", "PartialAutoApproveMinScore=0.60 zu niedrig"],
        ["M9", "KnowledgeBackupService.cs:176", "Import-Rollback unvollstaendig"],
        ["M10", "PdfProtocolTableParser.cs:418", "Kein explizites Encoding fuer pdftotext"],
        ["M11", "VideoSelfTrainingOrchestrator.cs:210", "KillOrphanedFfmpeg killt ALLE ffmpeg > 5min"],
        ["M12", "BatchSelfTrainingOrchestrator.cs:537", "PDF-Zuordnung per Contains statt exakt"],
        ["M13", "lora_training.py:156", "Temp-Dateien nicht aufgeraeumt bei Fehler"],
        ["M14", "lora_training.py:179", "LoRA-Modell nicht explizit entladen"],
        ["M15", "training.py:149", "Dataset-Quality-Check ignoriert fehlerhafte Labels"],
        ["M16", "yolo_wrapper.py:25", "Globale Variablen ohne Lock bei Reload"],
    ]
    story.append(make_table(
        ["#", "Datei:Zeile", "Problem"],
        medium_findings,
        col_widths=[10 * mm, 55 * mm, W - 65 * mm]
    ))
    story.append(Spacer(1, 6 * mm))

    # =====================================================================
    # 7. SICHERHEIT
    # =====================================================================
    story.append(Paragraph("7. Sicherheits-Findings", style_h1))

    sec_findings = [
        ["S1", "HIGH", "lora_training.py:345", "Path Traversal: deploy-lora akzeptiert beliebige Pfade"],
        ["S2", "MEDIUM", "training.py:393", "Base64-Image-Decoding ohne Groessenlimit (DoS)"],
        ["S3", "MEDIUM", "SelfTrainingOrchestrator.cs:400", "Few-Shot Code-Normalisierung: ArgumentOutOfRange bei kurzen Codes"],
        ["S4", "INFO", "requirements.txt", "Dependencies nicht gepinnt (>= statt ==)"],
    ]
    story.append(make_table(
        ["#", "Severity", "Datei:Zeile", "Problem"],
        sec_findings,
        col_widths=[8 * mm, 16 * mm, 45 * mm, W - 69 * mm]
    ))

    story.append(PageBreak())

    # =====================================================================
    # 8. TEST-ABDECKUNG
    # =====================================================================
    story.append(Paragraph("8. Test-Abdeckung", style_h1))

    story.append(Paragraph("Getestet (gut):", style_h2))
    tested = [
        ["SampleQualityGateService", "277 Zeilen", "Komprehensiv, alle Hard-Red Kriterien"],
        ["VsaCodeResolver", "233 Zeilen", "Edge Cases, Umlaut-Handling, Clock-Normalisierung"],
        ["QualityGateService", "116 Zeilen", "Alle 3 Ampelfarben, Gewichtungen"],
        ["CalibrationMetrics", "78 Zeilen", "ECE-Berechnung, Bin-Grenzen"],
        ["AutoApproval", "102 Zeilen", "Alle 5 Ablehnungskriterien"],
    ]
    story.append(make_table(
        ["Klasse", "Umfang", "Qualitaet"],
        tested,
        col_widths=[42 * mm, 22 * mm, W - 64 * mm]
    ))
    story.append(Spacer(1, 4 * mm))

    story.append(Paragraph("NICHT getestet (kritisch):", style_h2))
    untested = [
        ["1", "DifferenceAnalyzer", "290 Zeilen", "Komplexer Matching-Algorithmus"],
        ["2", "BenchmarkRunner", "226 Zeilen", "Metrics-Aggregation"],
        ["3", "SelfTrainingOrchestrator", "200+ Zeilen", "Kernlogik Selbsttraining"],
        ["4", "VideoSelfTrainingOrchestrator", "200+ Zeilen", "Video-Pipeline"],
        ["5", "YoloRetrainOrchestrator", "100+ Zeilen", "YOLO-Retraining"],
        ["6", "QwenLoraOrchestrator", "100+ Zeilen", "LoRA Fine-Tuning"],
        ["7", "BenchmarkMetricsStore", "100+ Zeilen", "Regressions-Erkennung"],
        ["8", "SelfTrainingComparisonService", "100+ Zeilen", "Vergleichslogik"],
        ["9", "FrameQualityFilter", "50+ Zeilen", "Frame-Qualitaet"],
        ["10", "TrainingSamplesStore", "300+ Zeilen", "Datenintegritaet"],
    ]
    story.append(make_table(
        ["#", "Klasse", "Umfang", "Risiko"],
        untested,
        col_widths=[8 * mm, 45 * mm, 22 * mm, W - 75 * mm]
    ))

    story.append(PageBreak())

    # =====================================================================
    # 9. KONSOLIDIERUNG: 4 REVIEWS
    # =====================================================================
    story.append(Paragraph("9. Konsolidierung: 4 Reviews im Vergleich", style_h1))
    story.append(Paragraph(
        "Vier unabhaengige Perspektiven wurden zusammengefuehrt:",
        style_body
    ))
    story.append(Spacer(1, 3 * mm))

    review_comp = [
        ["Architektur", "Exzellent", "Gut", "Exzellent", "--"],
        ["Deploy-Reihenfolge", "--", "Problem", "Bereits gefixt", "Korrekt bestaetigt"],
        ["Thread-Sicherheit", "--", "--", "3 Blocker", "--"],
        ["BBox-Daten", "--", "--", "--", "0 von 15.417"],
        ["Sidecar-Status", "--", "--", "--", "Blockiert (training)"],
        ["Benchmark", "--", "--", "--", "1 statt 20"],
        ["Test-Abdeckung", "--", "1 Fehler", "35+ ungetestet", "--"],
        ["Auto-Approve", "--", "Risiko erkannt", "Bestaetigt", "Aggressiv"],
    ]
    story.append(make_table(
        ["Aspekt", "Gemini", "ChatGPT", "Claude Opus (statisch)", "Live-Runtime"],
        review_comp,
        col_widths=[30 * mm, 22 * mm, 22 * mm, 40 * mm, 30 * mm]
    ))
    story.append(Spacer(1, 4 * mm))

    story.append(Paragraph("Korrigierte Findings:", style_h3))
    corrections = [
        ["Deploy-Reihenfolge", "Manifest VOR Reload (B6)", "Bereits korrekt: Reload VOR Manifest"],
        ["VSA im Sidecar", "Test-Blocker", "Bereits gefixt: 'Schadenscodes' statt 'VSA-Codes'"],
    ]
    story.append(make_table(
        ["Finding", "Frueheres Audit", "Tatsaechlich (verifiziert)"],
        corrections,
        col_widths=[35 * mm, 45 * mm, W - 80 * mm]
    ))

    story.append(PageBreak())

    # =====================================================================
    # 10. EMPFOHLENE PRIORITAETEN
    # =====================================================================
    story.append(Paragraph("10. Empfohlene Prioritaeten", style_h1))

    story.append(Paragraph("Phase 1: Sofort (Runtime-Blocker)", style_h2))
    phase1 = [
        ["1", "Sidecar neustarten", "maintenance_reason aufloesen", "5 min"],
        ["2", "Benchmark-Set importieren", "20 Haltungen aus tools/ aktivieren", "10 min"],
        ["3", "Smoke-Test wiederholen", "Detektionen > 0 verifizieren", "15 min"],
    ]
    story.append(make_table(
        ["#", "Aktion", "Ziel", "Aufwand"],
        phase1,
        col_widths=[8 * mm, 35 * mm, W - 63 * mm, 20 * mm]
    ))
    story.append(Spacer(1, 4 * mm))

    story.append(Paragraph("Phase 2: Diese Woche (Code-Crashes)", style_h2))
    phase2 = [
        ["4", "Few-Shot Bug fixen", "ArgumentOutOfRange bei kurzen Codes", "10 min"],
        ["5", "LoRA Guard Release", "end_training_guard() im Exception-Handler", "5 min"],
        ["6", "Deadlock Pause/Resume", "ManualResetEventSlim statt SemaphoreSlim", "30 min"],
        ["7", "Retrain-Mutex", "SemaphoreSlim(1,1) um RunIfEligibleAsync", "10 min"],
        ["8", "SettingsStore atomar", "Temp+Rename Pattern kopieren", "20 min"],
    ]
    story.append(make_table(
        ["#", "Aktion", "Ziel", "Aufwand"],
        phase2,
        col_widths=[8 * mm, 35 * mm, W - 63 * mm, 20 * mm]
    ))
    story.append(Spacer(1, 4 * mm))

    story.append(Paragraph("Phase 3: Naechste Woche (Tests + Qualitaet)", style_h2))
    phase3 = [
        ["9", "DifferenceAnalyzerTests.cs", "Matching-Algorithmus verifizieren", "2h"],
        ["10", "BenchmarkRunnerTests.cs", "Metrics-Berechnung verifizieren", "1h"],
        ["11", "CI/CD Pipeline", "GitHub Actions einrichten", "1h"],
        ["12", "Auto-Approve konservativ", "MinScore 0.80, AutoApprove=false", "5 min"],
        ["13", "BBox-Pipeline planen", "DINO+SAM fuer BBox-Generierung", "4h"],
    ]
    story.append(make_table(
        ["#", "Aktion", "Ziel", "Aufwand"],
        phase3,
        col_widths=[8 * mm, 40 * mm, W - 68 * mm, 20 * mm]
    ))
    story.append(Spacer(1, 4 * mm))

    story.append(Paragraph("Phase 4: Mittelfristig (Robustheit)", style_h2))
    phase4 = [
        ["14", "Hardcoded Toleranzen", "Konfigurierbar via Settings", "2h"],
        ["15", "Hungarian Algorithm", "Optimale Zuordnung statt Greedy", "4h"],
        ["16", "KB-Performance", "Index auf Embeddings, PRAGMA foreign_keys", "1h"],
        ["17", "Path Traversal Fix", "Pfad-Validierung in deploy-lora", "30 min"],
    ]
    story.append(make_table(
        ["#", "Aktion", "Ziel", "Aufwand"],
        phase4,
        col_widths=[8 * mm, 40 * mm, W - 68 * mm, 20 * mm]
    ))

    story.append(PageBreak())

    # =====================================================================
    # 11. POSITIV-BEWERTUNG
    # =====================================================================
    story.append(Paragraph("11. Positiv-Bewertung", style_h1))
    story.append(Paragraph(
        "Die Gesamtarchitektur ist auf professionellem MLOps-Niveau. "
        "Die gefundenen Probleme sind Implementierungsdetails (Thread-Sicherheit, Edge Cases), "
        "keine Architekturmaengel.",
        style_body
    ))
    story.append(Spacer(1, 4 * mm))

    positives = [
        ["Thin-AI Prinzip", "Konsequent durchgesetzt via ArchitectureLayerGuardTests"],
        ["Atomic Writes", "TrainingSamplesStore: Temp-File + Validate + Rename + 3-Backup-Rotation"],
        ["QualityGate-Ampel", "Hard-Red Kriterien, gewichtete Issues, Batch-Evaluation"],
        ["KnowledgeBase Dedup", "Cosine-Similarity (0.92) + kanonische Signatur"],
        ["Few-Shot RAG", "nomic-embed-text Embeddings, Top-K Retrieval mit Hybrid-Scoring"],
        ["Benchmark Pipeline", "Regressions-Erkennung, Per-Code-F1, Zeitreihen-Speicher"],
        ["Graceful Degradation", "Sidecar-Ausfall: Fallback auf Qwen-Only"],
        ["Per-Frame Timeout", "Frame-Fehler ueberspringen statt Pipeline-Abbruch"],
        ["Path-Traversal-Schutz", "KnowledgeBackupService: GetFullPath + StartsWith-Check"],
        ["Deploy-Reihenfolge", "Write-After-Success: Reload vor Manifest (korrekt implementiert)"],
    ]
    story.append(make_table(
        ["Feature", "Implementierung"],
        positives,
        col_widths=[38 * mm, W - 38 * mm]
    ))

    story.append(Spacer(1, 10 * mm))
    story.append(HRFlowable(width="100%", thickness=1, color=MID_GRAY))
    story.append(Spacer(1, 4 * mm))
    story.append(Paragraph(
        "<i>Erstellt von Claude Code Audit System. "
        "Vier unabhaengige Reviews konsolidiert: "
        "Claude Opus (statische Analyse), Gemini (Architektur-Review), "
        "ChatGPT (Bug-Finding), Live-Runtime-Audit.</i>",
        ParagraphStyle("Disclaimer", parent=style_body, fontSize=8, textColor=MID_GRAY, alignment=TA_CENTER)
    ))

    # --- Build ---
    doc.build(story, onFirstPage=header_footer, onLaterPages=header_footer)
    return OUTPUT_PATH


if __name__ == "__main__":
    path = build_pdf()
    print(f"PDF erstellt: {path}")
