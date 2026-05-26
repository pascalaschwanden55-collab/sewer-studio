"""FastAPI application – Sewer-Studio Vision Sidecar."""

import logging
from contextlib import asynccontextmanager

from fastapi import FastAPI, Request
from fastapi.responses import JSONResponse

from .config import settings
from .gpu_manager import gpu_manager
from .routes import health, yolo, dino, sam, training

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] %(name)s: %(message)s",
)


@asynccontextmanager
async def lifespan(app: FastAPI):
    logging.getLogger("sidecar").info(
        "Sidecar starting on %s:%d  (models: %s)\n"
        "  Device routing: YOLO=%s  DINO=%s  SAM=%s",
        settings.host,
        settings.port,
        settings.models_dir,
        settings.effective_yolo_device,
        settings.effective_dino_device,
        settings.effective_sam_device,
    )
    yield
    logging.getLogger("sidecar").info("Sidecar shutting down — unloading all models ...")
    gpu_manager.unload_all()


app = FastAPI(
    title="Sewer-Studio Vision Sidecar",
    version="1.1.0",
    description="Multi-Model Vision Pipeline (YOLO / Grounding DINO / SAM)",
    lifespan=lifespan,
)


def _normalize_host(host_header: str | None) -> str:
    if not host_header:
        return ""

    host = host_header.strip().lower()
    if host.startswith("["):
        end = host.find("]")
        return host[1:end] if end > 0 else host

    if ":" in host:
        host = host.split(":", 1)[0]

    return host


def _trusted_hosts() -> set[str]:
    raw = settings.trusted_hosts or ""
    return {
        item.strip().lower()
        for item in raw.split(",")
        if item.strip()
    }


def _auth_token() -> str:
    return (settings.auth_token or "").strip()


@app.middleware("http")
async def enforce_loopback_security(request: Request, call_next):
    trusted = _trusted_hosts()
    host = _normalize_host(request.headers.get("host"))
    if "*" not in trusted and host not in trusted:
        return JSONResponse(
            {"detail": "Untrusted host."},
            status_code=403,
        )

    token = _auth_token()
    if token and request.headers.get("X-Sidecar-Token") != token:
        return JSONResponse(
            {"detail": "Invalid sidecar token."},
            status_code=401,
        )

    return await call_next(request)

# Register routes
app.include_router(health.router, tags=["health"])
app.include_router(yolo.router, tags=["yolo"])
app.include_router(dino.router, tags=["dino"])
app.include_router(sam.router, tags=["sam"])
app.include_router(training.router, tags=["training"])
