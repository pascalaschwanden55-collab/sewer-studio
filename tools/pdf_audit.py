"""
PDF-Audit: Scannt alle Haltungs-Ordner und klassifiziert PDF-Formate.
Ausgabe: CSV mit Haltung, PDF-Status, Format, Eintraege, Caesar-Shift noetig.
"""
import subprocess
import re
import sys
from pathlib import Path
from collections import Counter

ROOT = Path("D:/Haltungen")
OUT_CSV = Path("c:/Sewer-Studio_KI_4.1/tools/pdf_audit_result.csv")
PDFTOTEXT = r"C:\Program Files\Git\mingw64\bin\pdftotext.exe"

# Format-Signaturen (aus PdfProtocolExtractor bekannt)
SIGNATURES = {
    "WinCan_Standard":  [r"WinCan", r"VX", r"EN ?13508"],
    "IKAS_Caesar":      [r",QVSGDWXP", r",NTS_R", r"QVSGDZM", r"GDWXP"],  # verschobenes "Inspektion"/"Datum"
    "IKAS_Normal":      [r"IBAK", r"IBOS", r"Inspektionsprotokoll.*IBAK"],
    "VSA_Generic":      [r"VSA.KEK", r"VSA-KEK", r"Schadenskatalog"],
    "Scan_or_Empty":    [],  # bleibt leer wenn kein Text extrahierbar
    "Fretz_Format":     [r"Fretz", r"AG Fretz"],
}

# Meter-Zeilen-Regex (aus PdfProtocolTableParser)
METER_REGEX = re.compile(r"^\s*(\d{1,4}[.,]\d{1,2})(?!\d|\.\d)(?:\s|$)", re.MULTILINE)
CODE_REGEX = re.compile(r"\b([ABFI][A-Z]{1,4})\b")

KNOWN_WORDS = ["Leitung", "Video", "Foto", "Zustand", "Material", "Schacht",
               "Kanal", "Haltung", "Inspektion", "Dimension", "Profil",
               "Rohr", "Position", "Entf", "Strasse", "Wetter"]

def byte_shift(text, shift):
    """Byte-Level Shift wie C# ShiftAllChars — whitespace bleibt unveraendert."""
    out = []
    for c in text:
        if c in '\r\n\t ':
            out.append(c)
        else:
            out.append(chr(ord(c) + shift))
    return ''.join(out)

def count_word_hits(text):
    return sum(1 for w in KNOWN_WORDS if w.lower() in text.lower())

def caesar_decode(text):
    """Wie C# TryDecodeShiftedText: probiert shifts 1..60, nimmt besten."""
    if not text:
        return text
    existing = count_word_hits(text)
    if existing >= 3:
        return text
    best_shift = 0
    best_count = existing
    for shift in range(1, 61):
        try:
            decoded = byte_shift(text, shift)
            hits = count_word_hits(decoded)
            if hits > best_count:
                best_count = hits
                best_shift = shift
        except (ValueError, OverflowError):
            continue
    if best_shift > 0 and best_count >= 3:
        return byte_shift(text, best_shift)
    return text

def classify_format(text):
    """Sucht Signaturen, gibt erkanntes Format zurueck."""
    if not text or len(text.strip()) < 50:
        return "Scan_or_Empty", text
    decoded = caesar_decode(text)
    was_shifted = decoded != text
    for fmt, patterns in SIGNATURES.items():
        for pat in patterns:
            if re.search(pat, decoded, re.IGNORECASE):
                return (fmt + "_Caesar") if was_shifted else fmt, decoded
    return ("IKAS_Caesar" if was_shifted else "Unknown"), decoded

def count_entries(text):
    """Schaetzt wie viele Protokoll-Eintraege (Meter+Code-Kombis) im Text sind."""
    meters = len(METER_REGEX.findall(text))
    codes = len(CODE_REGEX.findall(text))
    return min(meters, codes)

def extract_pdf(pdf_path):
    """Versucht pdftotext (layout-preserving). Fallback: empty."""
    try:
        result = subprocess.run(
            [PDFTOTEXT, "-layout", "-nopgbrk", str(pdf_path), "-"],
            capture_output=True, text=True, timeout=15, encoding="utf-8", errors="replace"
        )
        if result.returncode == 0:
            return result.stdout
    except (subprocess.TimeoutExpired, Exception) as e:
        return ""
    return ""

def main():
    if not ROOT.exists():
        print(f"ERR: {ROOT} existiert nicht")
        sys.exit(1)

    folders = sorted([f for f in ROOT.iterdir() if f.is_dir()])
    print(f"Scanning {len(folders)} Haltungen...")

    results = []
    format_stats = Counter()
    fail_count = 0

    with open(OUT_CSV, "w", encoding="utf-8", newline="") as f:
        f.write("haltung,pdf_file,pdf_kb,text_len,format,entries,readable\n")

        for i, folder in enumerate(folders):
            if i % 100 == 0:
                print(f"  [{i}/{len(folders)}] {folder.name[:50]}")

            pdfs = list(folder.glob("*.pdf"))
            if not pdfs:
                f.write(f"{folder.name},,0,0,NoPdf,0,False\n")
                format_stats["NoPdf"] += 1
                fail_count += 1
                continue

            pdf = pdfs[0]
            try:
                pdf_kb = pdf.stat().st_size // 1024
            except Exception:
                pdf_kb = 0

            text = extract_pdf(pdf)
            fmt, decoded = classify_format(text)
            entries = count_entries(decoded) if decoded else 0
            readable = entries > 0

            format_stats[fmt] += 1
            if not readable:
                fail_count += 1

            # Escape quotes in name
            f.write(f"\"{folder.name}\",\"{pdf.name}\",{pdf_kb},{len(text)},{fmt},{entries},{readable}\n")
            results.append((folder.name, fmt, entries, readable))

    print(f"\n=== Zusammenfassung ===")
    print(f"Gesamt: {len(folders)} Haltungen")
    print(f"Nicht lesbar/leer: {fail_count} ({fail_count*100//len(folders)}%)")
    print(f"\nFormat-Verteilung:")
    for fmt, count in format_stats.most_common():
        print(f"  {fmt:35s} {count:5d}  ({count*100//len(folders)}%)")
    print(f"\nCSV: {OUT_CSV}")

if __name__ == "__main__":
    main()
