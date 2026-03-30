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


def extract_images(pdf_path: str, output_dir: str, min_w: int = 400, min_h: int = 300) -> list:
    """Extrahiert alle Fotos (grosse Bilder) aus einem PDF."""
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

            # Seitenverhaeltnis (Kanalbilder: ~4:3 oder 16:9)
            aspect = w / h
            if aspect < 0.5 or aspect > 3.0:
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
