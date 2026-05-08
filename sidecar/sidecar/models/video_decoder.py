"""NVDEC Hardware-Video-Dekodierung mit Software-Fallback.

Bevorzugt PyNvVideoCodec (dedizierter NVIDIA Hardware-Decoder, 0% CPU-Last).
Fallback auf PyAV (Software-Dekodierung, stabil auf allen Systemen).
"""

from __future__ import annotations

import logging
from pathlib import Path
from typing import Iterator

import numpy as np

logger = logging.getLogger(__name__)

# NVDEC-Verfuegbarkeit (lazy-geprüft beim ersten Aufruf)
_nvdec_available: bool | None = None
_nvdec_check_error: str = ""


def is_nvdec_available() -> bool:
    """Prueft ob NVDEC Hardware-Dekodierung verfuegbar ist."""
    global _nvdec_available, _nvdec_check_error
    if _nvdec_available is not None:
        return _nvdec_available

    try:
        import PyNvVideoCodec as nvc  # pip install pynvvideocodec

        # Kurzer Funktionstest: Demuxer mit leerem Pfad → AttributeError wenn API fehlt
        _ = nvc.CreateDemuxer
        _nvdec_available = True
        logger.info("NVDEC Hardware-Dekodierung verfuegbar (PyNvVideoCodec)")
    except ImportError as ie:
        _nvdec_available = False
        # DLL-Ladefehler = PyNvVideoCodec installiert, aber NVIDIA Video Codec SDK DLL fehlt
        err_str = str(ie)
        if "DLL load failed" in err_str or "module" in err_str.lower():
            _nvdec_check_error = (
                "nvcuvid.dll nicht gefunden (NVIDIA Video Codec SDK fehlt)"
            )
            logger.info(
                "NVDEC: nvcuvid.dll nicht gefunden — "
                "NVIDIA Video Codec SDK 13 installieren oder von nvidia.com herunterladen. "
                "Software-Fallback (PyAV) aktiv."
            )
        else:
            _nvdec_check_error = "PyNvVideoCodec nicht installiert"
            logger.info(
                "NVDEC: PyNvVideoCodec nicht installiert — Software-Fallback aktiv"
            )
    except Exception as e:
        _nvdec_available = False
        _nvdec_check_error = str(e)
        logger.info("NVDEC: Initialisierung fehlgeschlagen (%s) — Software-Fallback", e)

    return _nvdec_available


def get_video_duration(video_path: str) -> float:
    """Videodauer in Sekunden via PyAV ermitteln."""
    try:
        import av

        with av.open(video_path) as container:
            if container.duration and container.duration > 0:
                return float(container.duration) / 1_000_000  # Microsekunden → Sekunden
            stream = container.streams.video[0]
            if stream.duration and stream.time_base:
                return float(stream.duration * stream.time_base)
    except Exception as e:
        logger.warning("Videodauer nicht ermittelbar fuer '%s': %s", video_path, e)
    return 0.0


# ── NVDEC-Pfad ────────────────────────────────────────────────────────────────


def _decode_frames_nvdec(
    video_path: str,
    step_seconds: float,
) -> Iterator[tuple[float, np.ndarray]]:
    """NVDEC Hardware-Dekodierung. Liefert (timestamp_sec, RGB_frame)."""
    import PyNvVideoCodec as nvc

    demuxer = nvc.CreateDemuxer(video_path)

    # Decoder-Konfiguration: GPU 0, kein Device-Memory (direkt CPU-Array)
    decoder = nvc.CreateDecoder(
        gpuId=0,
        codec=demuxer.GetNvCodecId(),
        cudaContext=0,
        cudaStream=0,
        useDeviceFrame=False,
    )

    w, h = decoder.Width(), decoder.Height()

    # NV12 → RGB Konvertierung via PIL (NVDEC liefert YUV NV12)
    from PIL import Image

    next_target = 0.0
    packet_data = nvc.PacketData()

    for packet in demuxer:
        surface = decoder.DecodeSinglePacket(packet)
        if surface.Empty():
            continue

        decoder.LastPacketData(packet_data)
        # PTS in Sekunden (NVDEC liefert nanosekunden-basierte PTS)
        pts_sec = packet_data.pts / 1_000_000_000.0 if packet_data.pts > 0 else 0.0

        if pts_sec < next_target:
            continue

        # NV12 Surface in numpy herunterladen
        downloader = nvc.PySurfaceDownloader(w, h, nvc.PixelFormat.NV12, 0)
        nv12 = np.empty((h * 3 // 2, w), dtype=np.uint8)
        if not downloader.DownloadSingleSurface(surface, nv12):
            continue

        # NV12 → RGB
        yuv = nv12.reshape((h * 3 // 2, w))
        rgb = np.array(Image.fromarray(yuv, "YCbCr").convert("RGB"))

        yield pts_sec, rgb
        next_target = pts_sec + step_seconds

    # Drain buffered Frames
    while True:
        surface = decoder.FlushSingleFrame()
        if surface.Empty():
            break
        decoder.LastPacketData(packet_data)
        pts_sec = (
            packet_data.pts / 1_000_000_000.0 if packet_data.pts > 0 else next_target
        )

        if pts_sec < next_target:
            continue

        downloader = nvc.PySurfaceDownloader(w, h, nvc.PixelFormat.NV12, 0)
        nv12 = np.empty((h * 3 // 2, w), dtype=np.uint8)
        if not downloader.DownloadSingleSurface(surface, nv12):
            continue

        from PIL import Image

        yuv = nv12.reshape((h * 3 // 2, w))
        rgb = np.array(Image.fromarray(yuv, "YCbCr").convert("RGB"))
        yield pts_sec, rgb
        next_target = pts_sec + step_seconds


# ── Software-Fallback (PyAV) ──────────────────────────────────────────────────


def _decode_frames_software(
    video_path: str,
    step_seconds: float,
) -> Iterator[tuple[float, np.ndarray]]:
    """Software-Dekodierung via PyAV. Liefert (timestamp_sec, RGB_frame)."""
    import av

    with av.open(video_path) as container:
        stream = container.streams.video[0]
        stream.codec_context.skip_frame = "NONREF"  # Spart CPU bei Step-Dekodierung

        next_target = 0.0

        for frame in container.decode(stream):
            if frame.pts is None:
                continue

            pts_sec = float(frame.pts * frame.time_base)

            if pts_sec < next_target:
                continue

            rgb = frame.to_ndarray(format="rgb24")
            yield pts_sec, rgb
            next_target = pts_sec + step_seconds


# ── Oeffentliche API ──────────────────────────────────────────────────────────


def decode_frames(
    video_path: str,
    step_seconds: float,
) -> Iterator[tuple[float, np.ndarray, str]]:
    """Dekodiert Video-Frames im step_seconds-Abstand.

    Liefert (timestamp_sec, rgb_frame_np, backend_name).
    Backend: "nvdec" oder "software".
    Versucht zuerst NVDEC, faellt bei Fehler auf Software-Dekodierung zurueck.
    """
    if is_nvdec_available():
        try:
            for ts, frame in _decode_frames_nvdec(video_path, step_seconds):
                yield ts, frame, "nvdec"
            return
        except Exception as e:
            logger.warning(
                "NVDEC-Dekodierung fehlgeschlagen fuer '%s': %s — Software-Fallback",
                Path(video_path).name,
                e,
            )

    # Software-Fallback
    for ts, frame in _decode_frames_software(video_path, step_seconds):
        yield ts, frame, "software"


def get_nvdec_status() -> dict:
    """NVDEC-Statusinformationen fuer den Health-Endpoint."""
    available = is_nvdec_available()
    return {
        "nvdec_available": available,
        "nvdec_backend": "pynvvideocodec" if available else "software_av",
        "nvdec_error": _nvdec_check_error if not available else None,
    }
