"""Gemeinsames, sicheres Bild-Dekodieren fuer Sidecar-Endpunkte."""

from __future__ import annotations

import base64
import binascii
import io

from fastapi import HTTPException, status
from PIL import Image


def decode_image_safe(
    image_base64: str,
    *,
    max_bytes: int,
    max_pixels: int,
) -> Image.Image:
    """Dekodiert base64-Bilder mit Groessen-, Format- und Pixel-Limit."""
    max_bytes = max(1, int(max_bytes))
    max_pixels = max(1, int(max_pixels))

    max_base64_chars = ((max_bytes + 2) // 3) * 4
    if len(image_base64) > max_base64_chars:
        raise HTTPException(
            status_code=status.HTTP_413_CONTENT_TOO_LARGE,
            detail="image exceeds size limit",
        )

    try:
        raw = base64.b64decode(image_base64, validate=True)
    except (binascii.Error, ValueError) as exc:
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST,
            detail="image is not valid base64",
        ) from exc

    if len(raw) > max_bytes:
        raise HTTPException(
            status_code=status.HTTP_413_CONTENT_TOO_LARGE,
            detail="image exceeds size limit",
        )

    try:
        with Image.open(io.BytesIO(raw)) as img:
            width, height = img.size
            if width <= 0 or height <= 0 or width * height > max_pixels:
                raise HTTPException(
                    status_code=status.HTTP_400_BAD_REQUEST,
                    detail="image exceeds pixel limit",
                )

            return img.convert("RGB")
    except HTTPException:
        raise
    except Exception as exc:
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST,
            detail="image is not a supported image",
        ) from exc
