"""Rohrachsen-Erkennung: Fluchtpunkt + Muffen-Detektion per Frame.

Leichtgewichtig (~5ms/Frame), kein GPU-Modell noetig.
Verwendet OpenCV fuer Ellipsen-Fitting am Rohrlumen.
"""

from __future__ import annotations

import base64
import io
import time
import logging

import numpy as np
from PIL import Image

from ..schemas.pipe_axis import PipeAxisResult

logger = logging.getLogger(__name__)

# cv2 ist verfuegbar (ultralytics-Abhaengigkeit)
try:
    import cv2

    _HAS_CV2 = True
except ImportError:
    _HAS_CV2 = False
    logger.warning("OpenCV nicht verfuegbar — Pipe-Axis Erkennung eingeschraenkt")


def _find_vanishing_point_cv2(gray: np.ndarray) -> tuple[float, float, float]:
    """Fluchtpunkt per Dunkelbereich-Zentroid (OpenCV).

    Das Rohrlumen (dunkelster Bereich) zeigt die Blickrichtung.
    Der Schwerpunkt des dunklen Bereichs = Fluchtpunkt.
    Returns: (vx_norm, vy_norm, confidence)
    """
    h, w = gray.shape

    # Leicht glaetten
    blurred = cv2.GaussianBlur(gray, (15, 15), 0)

    # Adaptiver Schwellwert: dunkelste 15% der Pixel
    threshold = np.percentile(blurred, 15)
    dark_mask = (blurred <= threshold).astype(np.uint8) * 255

    # Morphologie: kleine Artefakte entfernen, Lumen verbinden
    kernel = cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (9, 9))
    dark_mask = cv2.morphologyEx(dark_mask, cv2.MORPH_OPEN, kernel)
    dark_mask = cv2.morphologyEx(dark_mask, cv2.MORPH_CLOSE, kernel)

    # Groesste zusammenhaengende Dunkelregion finden
    contours, _ = cv2.findContours(
        dark_mask, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE
    )
    if not contours:
        return 0.5, 0.5, 0.0

    largest = max(contours, key=cv2.contourArea)
    area = cv2.contourArea(largest)
    image_area = h * w

    # Zu kleine Region → kein zuverlaessiger Fluchtpunkt
    if area < image_area * 0.005:
        return 0.5, 0.5, 0.1

    # Schwerpunkt der Dunkelregion
    M = cv2.moments(largest)
    if M["m00"] == 0:
        return 0.5, 0.5, 0.1

    cx = M["m10"] / M["m00"]
    cy = M["m01"] / M["m00"]

    # Konfidenz: groessere Dunkelregion → zuverlaessiger
    confidence = min(1.0, area / (image_area * 0.1))

    return cx / w, cy / h, confidence


def _find_pipe_ellipse_cv2(
    gray: np.ndarray,
) -> tuple[float, float, float, float, float]:
    """Rohroeffnung als Ellipse fitten (OpenCV).

    Sucht den hellen Ring (Rohrwand) um das dunkle Lumen.
    Returns: (cx_norm, cy_norm, rx_norm, ry_norm, confidence)
    """
    h, w = gray.shape

    blurred = cv2.GaussianBlur(gray, (7, 7), 0)

    # Canny-Kanten auf dem hellen Rohrrand
    median_val = np.median(blurred)
    low = int(max(0, 0.5 * median_val))
    high = int(min(255, 1.3 * median_val))
    edges = cv2.Canny(blurred, low, high)

    # Kanten verstaerken
    kernel = cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (3, 3))
    edges = cv2.dilate(edges, kernel, iterations=1)

    # Konturen finden
    contours, _ = cv2.findContours(edges, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)
    if not contours:
        return 0.5, 0.5, 0.3, 0.3, 0.0

    # Groesste Kontur mit genuegend Punkten fuer Ellipsen-Fit
    valid = [c for c in contours if len(c) >= 5]
    if not valid:
        return 0.5, 0.5, 0.3, 0.3, 0.0

    largest = max(valid, key=cv2.contourArea)
    area = cv2.contourArea(largest)
    if area < (h * w) * 0.02:
        return 0.5, 0.5, 0.3, 0.3, 0.2

    try:
        ellipse = cv2.fitEllipse(largest)
        (ecx, ecy), (axis_a, axis_b), angle = ellipse

        # Normieren
        cx_n = ecx / w
        cy_n = ecy / h
        rx_n = (axis_a / 2) / w
        ry_n = (axis_b / 2) / h

        # Plausibilitaet: Ellipse sollte grob kreisfoermig sein (Aspekt 0.5..2.0)
        aspect = max(axis_a, axis_b) / max(axis_b, axis_a, 1)
        if aspect > 3.0:
            return 0.5, 0.5, 0.3, 0.3, 0.1

        confidence = min(1.0, area / (h * w * 0.15))
        return cx_n, cy_n, rx_n, ry_n, confidence

    except cv2.error:
        return 0.5, 0.5, 0.3, 0.3, 0.0


def _detect_joint_cv2(gray: np.ndarray) -> bool:
    """Erkennt Rohrverbindungen (Muffen) anhand horizontaler Kanten.

    Muffen erzeugen einen hellen, ringfoermigen Streifen quer durchs Bild.
    """
    h, w = gray.shape

    # Horizontale Kanten betonen (Sobel Y-Richtung)
    sobel_y = cv2.Sobel(gray, cv2.CV_64F, 0, 1, ksize=5)
    abs_sobel = np.abs(sobel_y)

    # Mittlerer Bereich des Bildes (wo Muffen am deutlichsten sind)
    mid_band = abs_sobel[h // 4 : 3 * h // 4, w // 4 : 3 * w // 4]

    # Zeilenweise Summe → starke horizontale Kante = Muffe
    row_sums = mid_band.mean(axis=1)
    if len(row_sums) == 0:
        return False

    # Peak-Detektion: Muffe hat einen deutlichen Kanten-Peak
    mean_edge = row_sums.mean()
    max_edge = row_sums.max()

    # Muffe: max_edge >> mean_edge (Faktor 2.5+)
    return max_edge > mean_edge * 2.5 and max_edge > 30.0


def _fallback_numpy(gray: np.ndarray) -> PipeAxisResult:
    """Fallback ohne OpenCV: Gewichteter Dunkelbereich-Zentroid."""
    h, w = gray.shape
    inverted = 255.0 - gray.astype(np.float64)

    # Schwellwert: nur dunkelste Pixel
    threshold = np.percentile(inverted, 85)
    mask = inverted >= threshold

    ys, xs = np.where(mask)
    if len(xs) == 0:
        return PipeAxisResult(
            vanishing_x=0.5,
            vanishing_y=0.5,
            pipe_center_x=0.5,
            pipe_center_y=0.5,
            pipe_radius_x=0.3,
            pipe_radius_y=0.3,
            confidence=0.0,
        )

    weights = inverted[mask]
    vx = np.average(xs, weights=weights) / w
    vy = np.average(ys, weights=weights) / h

    return PipeAxisResult(
        vanishing_x=round(vx, 4),
        vanishing_y=round(vy, 4),
        pipe_center_x=round(vx, 4),
        pipe_center_y=round(vy, 4),
        pipe_radius_x=0.3,
        pipe_radius_y=0.3,
        confidence=0.3,
        has_joint=False,
    )


def analyze_pipe_axis(image_base64: str) -> PipeAxisResult:
    """Analysiert ein Frame: Fluchtpunkt, Rohrmitte, Muffen-Erkennung."""
    t0 = time.perf_counter()

    raw = base64.b64decode(image_base64)
    img = Image.open(io.BytesIO(raw)).convert("RGB")
    gray = np.array(img.convert("L"))

    if not _HAS_CV2:
        result = _fallback_numpy(gray)
        result.inference_time_ms = round((time.perf_counter() - t0) * 1000, 1)
        return result

    # Fluchtpunkt (Dunkelbereich-Zentroid)
    vx, vy, v_conf = _find_vanishing_point_cv2(gray)

    # Rohroeffnung (Ellipsen-Fit)
    pcx, pcy, prx, pry, p_conf = _find_pipe_ellipse_cv2(gray)

    # Muffen-Erkennung
    has_joint = _detect_joint_cv2(gray)

    # Gesamt-Konfidenz
    confidence = (v_conf + p_conf) / 2.0

    elapsed_ms = (time.perf_counter() - t0) * 1000

    return PipeAxisResult(
        vanishing_x=round(vx, 4),
        vanishing_y=round(vy, 4),
        pipe_center_x=round(pcx, 4),
        pipe_center_y=round(pcy, 4),
        pipe_radius_x=round(prx, 4),
        pipe_radius_y=round(pry, 4),
        confidence=round(confidence, 3),
        has_joint=has_joint,
        inference_time_ms=round(elapsed_ms, 1),
    )
