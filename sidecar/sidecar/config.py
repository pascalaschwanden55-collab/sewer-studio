"""Configuration loaded from environment variables with SEWER_SIDECAR_ prefix."""

from pydantic_settings import BaseSettings


class SidecarSettings(BaseSettings):
    """All settings are configurable via env vars prefixed SEWER_SIDECAR_."""

    host: str = "127.0.0.1"
    port: int = 8100
    models_dir: str = "./models"
    gpu_device: str = "cuda:0"

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
    yolo_model_name: str = "yolo26m.pt"
    require_custom_yolo: bool = False

    # Grounding DINO
    dino_box_threshold: float = 0.25
    dino_text_threshold: float = 0.20
    dino_labels: str = (
        "crack . fracture . break . deformation . "
        "corrosion . surface damage . erosion . "
        "root intrusion . roots . "
        "deposit . sediment . buildup . "
        "obstacle . blockage . "
        "infiltration . water ingress . leak . "
        "displaced joint . open joint . offset joint . "
        "hole . collapse . missing wall . "
        "connection defect . pipe defect . "
        "intruding connection . protruding seal"
    )

    # SAM
    sam_model_type: str = "vit_h"

    model_config = {"env_prefix": "SEWER_SIDECAR_"}


settings = SidecarSettings()
