# AuswertungPro Video AI Tools
# Python CLI for video analysis, OCR, and dataset generation

from .extract_frames import extract_frames
from .ocr_chainage import run_ocr_chainage
from .xtf_to_events import parse_xtf_to_events
from .make_keyframes import generate_keyframes
from .dataset_builder import build_dataset
from .train_baseline import train_baseline_model

__version__ = "0.1.0"
__all__ = [
    "extract_frames",
    "run_ocr_chainage", 
    "parse_xtf_to_events",
    "generate_keyframes",
    "build_dataset",
    "train_baseline_model",
]
