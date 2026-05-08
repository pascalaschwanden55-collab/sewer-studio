"""Schemas fuer Nemotron-Parse PDF-Tabellen-Extraktion."""

from __future__ import annotations

from pydantic import BaseModel


class ParsePdfRequest(BaseModel):
    """Request fuer PDF-Tabellen-Parsing."""

    pdf_base64: str
    page_numbers: list[int] | None = None  # None = alle Seiten
    table_format: str = "auto"  # "auto", "fretz", "kit", "uri"


class ParsedRow(BaseModel):
    """Eine einzelne Zeile aus einer extrahierten Tabelle."""

    meter_start: float | None = None
    meter_end: float | None = None
    code: str = ""
    char1: str = ""
    char2: str = ""
    clock_from: str = ""
    clock_to: str = ""
    remark: str = ""
    severity: int | None = None
    raw_text: str = ""


class ParsedTable(BaseModel):
    """Eine extrahierte Tabelle aus einer PDF-Seite."""

    page: int
    rows: list[ParsedRow]
    format_detected: str = "unknown"
    confidence: float = 0.0


class ParsePdfResponse(BaseModel):
    """Response mit extrahierten Tabellen."""

    tables: list[ParsedTable]
    total_rows: int = 0
    inference_time_ms: float = 0.0
    model_used: str = "nemotron-parse"
