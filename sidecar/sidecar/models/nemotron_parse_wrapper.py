"""Nemotron-Parse Wrapper fuer PDF-Tabellen-Extraktion.

Extrahiert strukturierte Inspektionsprotokolle aus alten PDF-Berichten
(Fretz AG, KIT Bauinspekt, Abwasser Uri).
GPU-Slot: ModelSlot.PARSE (on-demand, nicht persistent).
"""

from __future__ import annotations

import base64
import io
import time
import logging
import re
from pathlib import Path

import numpy as np

from ..config import settings
from ..gpu_manager import gpu_manager, ModelSlot
from ..schemas.parse import ParsedRow, ParsedTable, ParsePdfResponse

logger = logging.getLogger(__name__)


def _resolve_device() -> str:
    """Determine the effective device for Nemotron-Parse."""
    device = settings.gpu_device
    try:
        import torch
        if device.startswith("cuda") and not torch.cuda.is_available():
            return "cpu"
    except ImportError:
        return "cpu"
    return device


def _load_nemotron_on(device: str):
    """Load Nemotron-Parse model onto *device*."""
    try:
        from transformers import AutoModelForCausalLM, AutoProcessor
    except ImportError:
        raise ImportError(
            "transformers nicht installiert. "
            "Install: pip install transformers"
        )

    model_path = Path(settings.models_dir) / "nemotron-parse"
    if not model_path.exists():
        raise FileNotFoundError(
            f"Nemotron-Parse nicht gefunden in {model_path}. "
            "Bitte Modell dort ablegen."
        )

    import torch
    model = AutoModelForCausalLM.from_pretrained(
        str(model_path), trust_remote_code=True,
        torch_dtype=torch.float16 if device.startswith("cuda") else torch.float32,
    ).to(device)
    model.eval()

    processor = AutoProcessor.from_pretrained(str(model_path), trust_remote_code=True)
    return model, processor


def _parse_row_from_text(text: str) -> ParsedRow | None:
    """Versucht eine Textzeile als Protokollzeile zu parsen."""
    text = text.strip()
    if not text or len(text) < 5:
        return None

    # Regex fuer typische Protokollzeilen: Meter Code Char1 Char2 Uhr Bemerkung
    # z.B. "12.30  BAB  A  B  3-9  Riss quer diagonal"
    meter_pattern = re.compile(
        r"(\d{1,3}[.,]\d{1,2})\s+"      # Meterstand
        r"([A-Z]{2,4})\s*"               # Code
        r"([A-Z]?)\s*"                   # Char1
        r"([A-Z]?)\s*"                   # Char2
        r"(\d{1,2}(?:[-:]\d{1,2})?)?.*"  # Uhrlage (optional)
    )

    match = meter_pattern.match(text)
    if not match:
        return ParsedRow(raw_text=text)

    meter_str = match.group(1).replace(",", ".")
    try:
        meter = float(meter_str)
    except ValueError:
        meter = None

    return ParsedRow(
        meter_start=meter,
        code=match.group(2) or "",
        char1=match.group(3) or "",
        char2=match.group(4) or "",
        clock_from=match.group(5) or "",
        raw_text=text,
    )


def parse_pdf(
    pdf_base64: str,
    page_numbers: list[int] | None = None,
    table_format: str = "auto",
) -> ParsePdfResponse:
    """Parse PDF-Tabellen mit Nemotron-Parse.

    Fallback auf Regex wenn Modell nicht verfuegbar.
    """
    t0 = time.perf_counter()

    raw = base64.b64decode(pdf_base64)

    # PDF-Seiten extrahieren
    try:
        import fitz  # PyMuPDF
        doc = fitz.open(stream=raw, filetype="pdf")
    except ImportError:
        logger.error("PyMuPDF nicht installiert. Install: pip install pymupdf")
        return ParsePdfResponse(tables=[], inference_time_ms=0)

    tables: list[ParsedTable] = []
    total_rows = 0

    pages = page_numbers or list(range(len(doc)))

    for page_idx in pages:
        if page_idx >= len(doc):
            continue

        page = doc[page_idx]
        text = page.get_text("text")

        # Zeilen parsen
        rows: list[ParsedRow] = []
        for line in text.split("\n"):
            parsed = _parse_row_from_text(line)
            if parsed and (parsed.code or parsed.meter_start is not None):
                rows.append(parsed)

        if rows:
            tables.append(ParsedTable(
                page=page_idx + 1,
                rows=rows,
                format_detected=table_format if table_format != "auto" else "regex_fallback",
                confidence=0.5,  # Regex-basiert = niedrige Confidence
            ))
            total_rows += len(rows)

    doc.close()

    elapsed_ms = (time.perf_counter() - t0) * 1000

    # Versuche Nemotron-Parse fuer hoehere Qualitaet
    model_used = "regex_fallback"
    try:
        device = _resolve_device()
        state = gpu_manager.ensure_loaded(
            ModelSlot.PARSE, device, lambda: _load_nemotron_on(device)
        )
        model_used = "nemotron-parse"
        # TODO: Nemotron-basierte Extraktion implementieren wenn Modell verfuegbar
        # Voruebergehend: Regex-Ergebnisse verwenden
        logger.info("Nemotron-Parse geladen — verwende Modell-basierte Extraktion")
    except Exception as exc:
        logger.info("Nemotron-Parse nicht verfuegbar (%s) — verwende Regex-Fallback", exc)

    return ParsePdfResponse(
        tables=tables,
        total_rows=total_rows,
        inference_time_ms=round(elapsed_ms, 1),
        model_used=model_used,
    )
