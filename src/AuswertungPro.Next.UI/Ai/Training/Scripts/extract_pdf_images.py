#!/usr/bin/env python3
"""
Extrahiert Fotos aus einem Kanalinspektion-PDF mit korrekter Farbraum-Konvertierung.
Manche WinCan/IKAS-PDFs speichern JPEGs im CMYK-Farbraum — PyMuPDF konvertiert
korrekt zu RGB, im Gegensatz zu PdfPig/WPF.

Aufruf:
    python extract_pdf_images.py <pdf_path> <output_dir> [min_width] [min_height]

Gibt JSON auf stdout zurueck:
    [{"page": 1, "index": 0, "path": "...", "width": 788, "height": 576}, ...]
"""
import sys
import os
import json
import re

try:
    import fitz  # PyMuPDF
except ImportError:
    print(json.dumps({"error": "PyMuPDF nicht installiert. Bitte: pip install PyMuPDF"}))
    sys.exit(1)


def is_likely_photo(pix) -> bool:
    """
    Prueft ob ein Bild wahrscheinlich ein Kanalfoto ist (dunkel, farbig)
    und keine technische Zeichnung/Haltungsgrafik (hell, weisser Hintergrund).

    Kanalfotos: durchschnittliche Luminanz < 180, Farbvarianz > 10
    Haltungsgrafiken: weisser Hintergrund, avgLum > 200, wenig Farbvarianz
    """
    # Kleines Sampling fuer Geschwindigkeit (jeden 10. Pixel)
    samples = pix.samples
    n_channels = pix.n
    step = max(1, len(samples) // (n_channels * 500)) * n_channels  # ~500 Samples

    total_r, total_g, total_b = 0, 0, 0
    count = 0

    for i in range(0, len(samples) - n_channels + 1, step):
        if n_channels >= 3:
            r, g, b = samples[i], samples[i + 1], samples[i + 2]
        else:
            r = g = b = samples[i]  # Graustufen
        total_r += r
        total_g += g
        total_b += b
        count += 1

    if count == 0:
        return True  # Im Zweifel behalten

    avg_r = total_r / count
    avg_g = total_g / count
    avg_b = total_b / count
    avg_lum = (avg_r * 299 + avg_g * 587 + avg_b * 114) / 1000

    # Haltungsgrafiken: weisser Hintergrund (avgLum > 200)
    if avg_lum > 200:
        return False

    # Sehr helle Bilder mit wenig Farbvarianz = Diagramm/Grafik
    color_range = max(avg_r, avg_g, avg_b) - min(avg_r, avg_g, avg_b)
    if avg_lum > 180 and color_range < 15:
        return False

    return True


def extract_images(pdf_path: str, output_dir: str, min_w: int = 400, min_h: int = 300) -> list:
    """Extrahiert Kanalfotos aus einem PDF (filtert Haltungsgrafiken/Diagramme)."""
    os.makedirs(output_dir, exist_ok=True)
    doc = fitz.open(pdf_path)
    safe_name = re.sub(r'[^\w\-]', '_', os.path.splitext(os.path.basename(pdf_path))[0])

    results = []
    seen_sizes = set()  # Deduplizierung: Logos wiederholen sich

    for page_num in range(doc.page_count):
        page = doc[page_num]
        images = page.get_images()

        for img_idx, img in enumerate(images):
            xref = img[0]
            try:
                base = doc.extract_image(xref)
            except Exception:
                continue

            w, h = base['width'], base['height']

            # Dimensionsfilter
            if w < min_w or h < min_h:
                continue
            if w > 5000 or h > 5000:
                continue

            # Seitenverhaeltnis: Kanalfotos sind querformat (~4:3 oder 16:9)
            # Haltungsgrafiken sind oft hochformat (A4-artig, aspect < 1.0)
            aspect = w / h
            if aspect < 0.8 or aspect > 3.0:
                continue

            # Deduplizierung: gleiche Byte-Laenge = wahrscheinlich Duplikat (Logo)
            img_size = len(base['image'])
            if img_size in seen_sizes:
                continue
            seen_sizes.add(img_size)

            # Korrekte Farbraum-Konvertierung (CMYK → RGB)
            pix = fitz.Pixmap(doc, xref)
            if pix.n - pix.alpha > 3:  # CMYK oder aehnlich
                pix = fitz.Pixmap(fitz.csRGB, pix)

            # Foto-Filter: helle Bilder (Grafiken/Diagramme) ausschliessen
            if not is_likely_photo(pix):
                continue

            out_name = f"{safe_name}_p{page_num + 1}_{img_idx}.png"
            out_path = os.path.join(output_dir, out_name)
            pix.save(out_path)

            results.append({
                "page": page_num + 1,
                "index": img_idx,
                "path": out_path,
                "width": pix.width,
                "height": pix.height
            })

    doc.close()
    return results


if __name__ == '__main__':
    if len(sys.argv) < 3:
        print(json.dumps({"error": "Aufruf: extract_pdf_images.py <pdf_path> <output_dir> [min_w] [min_h]"}))
        sys.exit(1)

    pdf_path = sys.argv[1]
    output_dir = sys.argv[2]
    min_w = int(sys.argv[3]) if len(sys.argv) > 3 else 400
    min_h = int(sys.argv[4]) if len(sys.argv) > 4 else 300

    if not os.path.exists(pdf_path):
        print(json.dumps({"error": f"PDF nicht gefunden: {pdf_path}"}))
        sys.exit(1)

    try:
        images = extract_images(pdf_path, output_dir, min_w, min_h)
        print(json.dumps(images))
    except Exception as e:
        print(json.dumps({"error": str(e)}))
        sys.exit(1)
