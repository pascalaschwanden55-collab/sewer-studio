"""Route fuer Nemotron-Parse PDF-Tabellen-Extraktion."""

from __future__ import annotations

import logging

from fastapi import APIRouter

from ..schemas.parse import ParsePdfRequest, ParsePdfResponse
from ..models.nemotron_parse_wrapper import parse_pdf

logger = logging.getLogger(__name__)

router = APIRouter(prefix="/parse")


@router.post("/pdf-table", response_model=ParsePdfResponse)
async def parse_pdf_table(request: ParsePdfRequest) -> ParsePdfResponse:
    """Extrahiert Inspektionsprotokoll-Tabellen aus einem PDF."""
    logger.info(
        "PDF-Parse Request: pages=%s, format=%s",
        request.page_numbers or "alle",
        request.table_format,
    )
    return parse_pdf(
        pdf_base64=request.pdf_base64,
        page_numbers=request.page_numbers,
        table_format=request.table_format,
    )
