"""Configuration loaded from environment variables with SEWER_SIDECAR_ prefix."""

import os

from pydantic_settings import BaseSettings


class SidecarSettings(BaseSettings):
    """All settings are configurable via env vars prefixed SEWER_SIDECAR_."""

    host: str = "127.0.0.1"
    port: int = 8100
    models_dir: str = "./models"
    gpu_device: str = "cuda:0"

    # Shadow / CPU utilization
    florence2_shadow_device: str = "cuda:0"
    shadow_worker_count: int = 2
    shadow_every_n: int = 5
    cpu_threads: int = max(1, os.cpu_count() or 4)

    # Video pipeline parallelism
    video_worker_count: int = min(8, max(2, os.cpu_count() or 4))
    video_queue_maxsize: int = 16

    # Per-model device overrides (empty = fallback to gpu_device)
    yolo_device: str = ""
    dino_device: str = ""
    sam_device: str = ""

    @property
    def effective_yolo_device(self) -> str:
        return self.yolo_device if self.yolo_device else self.gpu_device

    @property
    def effective_dino_device(self) -> str:
        return self.dino_device if self.dino_device else self.gpu_device

    @property
    def effective_sam_device(self) -> str:
        return self.sam_device if self.sam_device else self.gpu_device

    # YOLO
    yolo_confidence: float = 0.25
    yolo_model_name: str = "yolo26l-seg.pt"
    require_custom_yolo: bool = False

    # TensorRT-Beschleunigung fuer YOLO
    # Aktiviert automatischen Export .pt -> .engine beim ersten Start (dauert 2-5 Min).
    # Engine-Datei ist GPU-spezifisch und wird neben der .pt Datei gespeichert.
    # Fallback auf PyTorch wenn TensorRT nicht installiert oder Export fehlschlaegt.
    yolo_use_tensorrt: bool = True
    yolo_tensorrt_fp16: bool = True
    # Precision: "fp16" (Standard) oder "fp4" (NVFP4, RTX 50xx)
    yolo_precision: str = "fp16"

    # Florence-2 (ersetzt Grounding DINO)
    florence2_model_path: str = "models/florence-2"
    florence2_confidence: float = 0.25
    dino_box_threshold: float = 0.25   # Wird intern als florence2_confidence interpretiert
    dino_text_threshold: float = 0.20  # Wird ignoriert (Florence-2 braucht das nicht)
    dino_labels: str = (
        "crack . fracture . break . deformation . "
        "corrosion . surface damage . erosion . "
        "root intrusion . roots . "
        "deposit . sediment . buildup . scale . calcite . "
        "obstacle . blockage . grease . "
        "infiltration . water ingress . leak . "
        "displaced joint . open joint . offset joint . "
        "hole . collapse . missing wall . "
        "connection defect . pipe defect . "
        "intruding connection . protruding seal . "
        "lateral connection . pipe junction"
    )

    # SAM 2 (ersetzt SAM 3)
    # SAM 2 Config-Name (Hydra): mit configs/ Prefix
    sam_model_type: str = "configs/sam2.1/sam2.1_hiera_l.yaml"
    sam_model_path: str = "models/sam2"

    # Video Super Resolution (VSR) fuer alte PAL-Videos (768x576)
    # Aktiviert automatisches Upscaling auf vsr_min_resolution Hoehe vor YOLO.
    # Erfordert: pip install realesrgan basicsr + RealESRGAN_x4plus.pth in models/
    # Fallback: Lanczos-Bicubic (immer verfuegbar, kein ML)
    vsr_enabled: bool = False   # opt-in: aktivieren wenn Real-ESRGAN-Gewichte vorhanden
    vsr_min_resolution: int = 720  # Nur Frames unter dieser Hoehe werden hochskaliert

    model_config = {"env_prefix": "SEWER_SIDECAR_"}


settings = SidecarSettings()
