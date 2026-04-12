#!/usr/bin/env python3
"""
Generiert den KI-Audit-Bericht für SewerStudio als PDF.
"""
import os
import datetime
from reportlab.lib.pagesizes import A4
from reportlab.lib.units import mm
from reportlab.lib.styles import getSampleStyleSheet, ParagraphStyle
from reportlab.lib.colors import HexColor
from reportlab.platypus import SimpleDocTemplate, Paragraph, Spacer, HRFlowable

OUTPUT_DIR = os.path.dirname(os.path.abspath(__file__))
OUTPUT_PDF = os.path.join(OUTPUT_DIR, "SewerStudio_Audit_Report.pdf")

styles = getSampleStyleSheet()
DARK_BLUE = HexColor("#1a365d")
MID_BLUE = HexColor("#2b6cb0")
GREEN = HexColor("#16a34a")

styles.add(ParagraphStyle("Title", parent=styles["Heading1"], fontSize=22, textColor=DARK_BLUE, spaceAfter=6*mm))
styles.add(ParagraphStyle("H2", parent=styles["Heading2"], fontSize=14, textColor=MID_BLUE, spaceBefore=6*mm, spaceAfter=2*mm))
styles.add(ParagraphStyle("Body", parent=styles["Normal"], fontSize=10, leading=14, spaceAfter=2*mm))
styles.add(ParagraphStyle("Bullet", parent=styles["Normal"], fontSize=10, leading=14, leftIndent=8*mm, bulletIndent=4*mm, spaceAfter=1.5*mm))
styles.add(ParagraphStyle("Score", parent=styles["Heading2"], fontSize=14, textColor=GREEN, spaceBefore=4*mm, spaceAfter=4*mm))

def p(text, style="Body"): 
    return Paragraph(text, styles[style])

def b(text): 
    return Paragraph(f"• {text}", styles["Bullet"])

def hr(): 
    return HRFlowable(width="100%", thickness=0.5, color=HexColor("#e2e8f0"), spaceBefore=3*mm, spaceAfter=3*mm)

def build_story():
    s = []
    s.append(p("SewerStudio (KI 4.0) - Architektur Audit", "Title"))
    s.append(p(f"Datum: {datetime.date.today().strftime('%d.%m.%Y')} | Generiert durch KI-Code-Review", "Body"))
    s.append(hr())
    
    s.append(p("Gesamtbewertung: 7.6 / 10 (Professionelles Niveau)", "Score"))
    s.append(p("Mit der Umsetzung der kritischen Fixes kann das System problemlos auf eine 9/10 gehoben werden.", "Body"))
    s.append(hr())
    
    s.append(p("1. Architektur-Stärken (Unbedingt beibehalten!)", "H2"))
    s.append(b("<b>Thin-AI Prinzip:</b> Strikte Trennung von deterministischer C# Geschäftslogik und KI-Vision."))
    s.append(b("<b>Lokale Multi-Modell-Pipeline:</b> Schnelle Vorfilter (YOLO), Maskierung (DINO+SAM) und Qwen."))
    s.append(b("<b>Eskalations-Mechanismus:</b> Ressourcenhungriges 32B-Modell wird nur bei Unsicherheiten geladen."))
    s.append(b("<b>Quality Gate:</b> 9-dimensionale Konfidenz-Fusion zur Vermeidung von Data Poisoning."))
    s.append(b("<b>Transaktionssicherheit:</b> Saubere Merge-Engine beim Import; Nutzer-Eingaben priorisiert."))
    
    s.append(p("2. Kritische Handlungsfelder (Prio 1 - Sofort umsetzen)", "H2"))
    s.append(b("<font color='#dc2626'><b>FormelEvaluator Bugs:</b></font> Substring-Ersetzung (qty vs quantity) anpassen und Division durch 0 abfangen."))
    s.append(b("<font color='#dc2626'><b>Fehlende Retry-Logik:</b></font> OllamaClient braucht Polly (Circuit Breaker) bei Timeouts."))
    s.append(b("<font color='#dc2626'><b>Atomare Writes:</b></font> Im TrainingSamplesStore .tmp-Dateien und atomares Umbenennen ergänzen."))
    s.append(b("<font color='#dc2626'><b>Off-by-One Fehler:</b></font> Im Frame-Sampling-Algorithmus (modulo Logik korrigieren)."))
    
    s.append(p("3. Mittelfristige Verbesserungen (Prio 2 - Nächster Sprint)", "H2"))
    s.append(b("<font color='#d97706'><b>VRAM-Monitoring:</b></font> Auto-Eviction im Python Sidecar bei >80% Speicherauslastung."))
    s.append(b("<font color='#d97706'><b>UI UX-Erweiterung:</b></font> Statusleiste & echte Ladebalken für KI-Fortschritt einbauen."))
    s.append(b("<font color='#d97706'><b>Widerspruchserkennung:</b></font> Logik zur Vermeidung von YOLO-SAM-Konflikten implementieren."))
    
    s.append(p("Fazit für den Entwickler", "H2"))
    s.append(p("Das Projekt hat den Status eines Hobby-Tools weit hinter sich gelassen und ist eine professionelle Fachanwendung. Wenn du die 4 kritischen Punkte abarbeitest, hast du ein extrem robustes System.", "Body"))
    
    return s

def main():
    doc = SimpleDocTemplate(OUTPUT_PDF, pagesize=A4, topMargin=20*mm, bottomMargin=20*mm, leftMargin=20*mm, rightMargin=20*mm)
    doc.build(build_story())
    print(f"PDF erfolgreich erstellt unter: {OUTPUT_PDF}")

if __name__ == '__main__':
    main()