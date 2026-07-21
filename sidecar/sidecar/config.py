"""Configuration loaded from environment variables with SEWER_SIDECAR_ prefix."""

from ipaddress import ip_address

from pydantic import field_validator
from pydantic_settings import BaseSettings


class SidecarSettings(BaseSettings):
    """All settings are configurable via env vars prefixed SEWER_SIDECAR_."""

    host: str = "127.0.0.1"
    port: int = 8100
    models_dir: str = "./models"
    gpu_device: str = "cuda:0"
    trusted_hosts: str = "127.0.0.1,localhost"
    auth_token: str = ""
    # Geteilte Token-Datei mit dem C#-Client (Default: %LOCALAPPDATA%/SewerStudio/.sidecar_token).
    # Leer -> Standardpfad wird zur Laufzeit ermittelt. Token wird beim Start erzeugt, falls keiner da.
    auth_token_file: str = ""
    telemetry_enabled: bool = True
    telemetry_dir: str = ""

    @field_validator("host")
    @classmethod
    def require_loopback_host(cls, value: str) -> str:
        host = value.strip()
        if host.lower() == "localhost":
            return host

        try:
            if ip_address(host).is_loopback:
                return host
        except ValueError:
            pass

        raise ValueError(
            "SEWER_SIDECAR_HOST muss eine Loopback-Adresse sein "
            "(localhost, 127.x.x.x oder ::1)."
        )

    # Fester Sandbox-Root fuer plan-gesteuerte Trainings-Exporte. Muss mit
    # <KnowledgeRoot>/training/datasets der Windows-App uebereinstimmen.
    training_export_root: str = r"C:\KI_BRAIN\training\datasets"
    training_max_image_bytes: int = 25 * 1024 * 1024
    inference_max_image_bytes: int = 25 * 1024 * 1024
    max_image_pixels: int = 50_000_000

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
    yolo_imgsz: int = 1280  # Inferenz-Aufloesung: 1280 statt Default 640 -> kleine Schaeden besser sichtbar
    yolo_model_name: str = "yolo26m.pt"
    require_custom_yolo: bool = False

    # YOLO-cls (VSA-Klassifikator). Modellwahl primaer ueber models/active.json
    # (einziger Schreiber: model-promotion-warden); diese Settings sind der
    # manuelle Override bzw. die Defaults fuer Eintraege ohne Metadaten.
    yolo_cls_model_path: str = ""
    yolo_cls_imgsz: int = 1024            # v5_nocrop wurde mit imgsz 1024 trainiert
    yolo_cls_preprocessing: str = "letterbox"  # nur fuer yolo_cls_model_path-Override
    yolo_cls_device: str = ""             # leer = gpu_device (CPU-Fallback wenn kein CUDA)

    @property
    def effective_cls_device(self) -> str:
        return self.yolo_cls_device if self.yolo_cls_device else self.gpu_device

    # Frame-Quality-Gate (_is_frame_usable) -- env-konfigurierbar, entschaerft fuer
    # dunkle Kanaele: gueltige dunkle Frames mit Inhalt wurden frueher hart verworfen.
    frame_min_brightness: float = 4.0    # frueher hart 10 -> nur echtes Schwarz/lens-cap raus
    frame_max_brightness: float = 250.0  # frueher hart 245
    frame_min_std: float = 2.0           # frueher hart 5 -> Glattrohr nicht als "leer" verwerfen
    frame_min_edge_var: float = 1.0      # frueher hart 3 -> dunkle, aber scharfe Frames behalten

    # Grounding DINO
    # Lokal, kein API-Backend: auto bevorzugt grounding_dino_swinb, wenn vorhanden,
    # und faellt sonst auf den bestehenden grounding_dino_1.5-Ordner (Swin-T OGC) zurueck.
    dino_model_dir: str = "auto"
    dino_box_threshold: float = 0.25
    dino_text_threshold: float = 0.20
    dino_labels: str = (
        "crack . fracture . break . deformation . "
        "corrosion . surface damage . erosion . "
        "root intrusion . roots . root mass . root ball . "
        "deposit . sediment . buildup . incrustation . scale . grease . "
        "obstacle . blockage . "
        "infiltration . water ingress . leak . "
        "displaced joint . open joint . offset joint . "
        "hole . collapse . missing wall . "
        "connection defect . pipe defect . "
        "intruding connection . protruding seal . "
        "lateral connection . junction . inlet . branch . side opening . "
        "pipe bend . bend"
    )

    # SAM
    # Lokal, kein API-Backend: auto nutzt ausschliesslich SAM-2.1-Gewichte unter models/sam2.1.
    # SAM 1 und aeltere SAM-2-Gewichte sind bewusst entfernt und werden nicht mehr als Fallback geladen.
    sam_backend: str = "auto"  # auto | sam2.1
    sam2_weights_path: str = ""
    sam2_model_cfg: str = "auto"
    # Score-Gate: Masken mit Predictor-Score darunter werden verworfen (skipped/degraded)
    # statt still als Befund-Basis akzeptiert. 0.0 = Gate aus (Alt-Verhalten).
    sam_min_score: float = 0.5

    # Leichter Bogen-Veto fuer YOLO-cls: verhindert, dass ein Bogen als BCE/Rohrende
    # durchrutscht. Bleibt unabhaengig vom alten SAM/Bogen-Overlay aktiv.
    bend_veto_enabled: bool = True

    # Test-/Rueckfall-Schalter fuer die seit 18.06. hinzugekommene geometrische
    # Bogen-Erkennung im SAM/Overlay-Pfad. Default AUS entspricht dem Verhalten der
    # Sicherung vom 14.06.2026: keine Bogen-Sondermarker aus der SAM-Antwort.
    bend_geometry_enabled: bool = False

    # ── SAM 3 (Text-Konzept-Segmentierung) ──────────────────────────────────
    # Experimentell und per Default AUS. SAM3 segmentiert direkt aus Text-Konzepten ("pipe bend")
    # und umgeht damit, dass YOLO-cls keine Bogen-Klasse hat und DINO Boegen als
    # "infiltration" labelt. Gewichte sind gated (facebook/sam3) -> manuell ablegen.
    # Hat keinen SAM1-/SAM2-Fallback; der produktive Prompt-Segmenter oben ist SAM2.1.
    sam3_enabled: bool = False
    # Pfad zur gated sam3-Gewichtsdatei (Dateiname MUSS 'sam3' enthalten). Leer -> Fehler
    # statt stillem Fallback, wenn sam3_enabled=true.
    sam3_weights_path: str = ""
    # Default-Konzeptliste (serverseitig an ' . ' gesplittet zu list[str]).
    sam3_concept_labels: str = "pipe bend . lateral connection . crack . root . deposit . water"
    sam3_conf: float = 0.25
    sam3_device: str = ""

    model_config = {"env_prefix": "SEWER_SIDECAR_"}


settings = SidecarSettings()
