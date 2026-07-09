"""Training data export endpoint (YOLO format)."""

from __future__ import annotations

import base64
import binascii
import random
import shutil
import logging
from pathlib import Path

from PIL import Image
from fastapi import APIRouter, HTTPException, status

from ..config import settings
from ..models.image_decode import decode_image_safe
from ..schemas.segmentation import TrainingExportRequest, TrainingExportResponse

router = APIRouter()
logger = logging.getLogger(__name__)
SPLIT_SEED = 1337


@router.post("/training/export-yolo", response_model=TrainingExportResponse)
def export_yolo(req: TrainingExportRequest) -> TrainingExportResponse:
    """Export training samples to YOLO format (images + labels + data.yaml)."""
    out = _resolve_output_dir(req.output_dir)

    # Vorab-Validierung VOR dem Anlegen der Export-Ordner: fehlerhafte Requests
    # (zu gross / kein gueltiges base64) duerfen keinen halb angelegten Export
    # hinterlassen. Groessen-Formel wie decode_image_safe; dekodierte Bytes
    # werden sofort verworfen (ein Bild zur Zeit, kein RAM-Spike).
    max_base64_chars = ((max(1, settings.training_max_image_bytes) + 2) // 3) * 4
    for sample in req.samples:
        if len(sample.image_base64) > max_base64_chars:
            raise HTTPException(
                status_code=status.HTTP_413_CONTENT_TOO_LARGE,
                detail="image exceeds size limit",
            )
        try:
            base64.b64decode(sample.image_base64, validate=True)
        except (binascii.Error, ValueError) as exc:
            raise HTTPException(
                status_code=status.HTTP_400_BAD_REQUEST,
                detail="image is not valid base64",
            ) from exc

    img_train = out / "images" / "train"
    img_val = out / "images" / "val"
    lbl_train = out / "labels" / "train"
    lbl_val = out / "labels" / "val"

    _prepare_output_dir(out, req.overwrite)

    for d in [img_train, img_val, lbl_train, lbl_val]:
        d.mkdir(parents=True, exist_ok=True)

    # Collect all class names
    class_set: set[str] = set()
    for sample in req.samples:
        for lbl in sample.labels:
            class_set.add(lbl.get("class_name", "defect"))
    class_list = sorted(class_set)
    class_map = {name: idx for idx, name in enumerate(class_list)}

    # Shuffle and split
    train_indices = _split_indices(len(req.samples), req.train_split)

    train_count = 0
    val_count = 0

    for i, sample in enumerate(req.samples):
        is_train = i in train_indices
        img_dir = img_train if is_train else img_val
        lbl_dir = lbl_train if is_train else lbl_val

        # Save image - jedes Bild erst hier dekodieren (lazy), damit nie alle Bilder
        # gleichzeitig im RAM liegen (vermeidet RAM-Spike bei grossen Self-Training-Laeufen).
        img = _decode_training_image(sample.image_base64)
        img_path = img_dir / f"sample_{i:06d}.jpg"
        img.save(str(img_path), "JPEG", quality=95)

        # Save label (YOLO format: class x_center y_center width height)
        lbl_path = lbl_dir / f"sample_{i:06d}.txt"
        lines: list[str] = []
        for lbl in sample.labels:
            cls_name = lbl.get("class_name", "defect")
            cls_idx = class_map.get(cls_name, 0)
            xc = lbl.get("x_center", 0.5)
            yc = lbl.get("y_center", 0.5)
            w = lbl.get("width", 0.1)
            h = lbl.get("height", 0.1)
            lines.append(f"{cls_idx} {xc:.6f} {yc:.6f} {w:.6f} {h:.6f}")
        lbl_path.write_text("\n".join(lines), encoding="utf-8")

        if is_train:
            train_count += 1
        else:
            val_count += 1

    # Write data.yaml
    data_yaml = out / "data.yaml"
    yaml_lines = [
        f"path: {out.resolve()}",
        f"train: images/train",
        f"val: images/val",
        f"nc: {len(class_list)}",
        f"names: {class_list}",
    ]
    data_yaml.write_text("\n".join(yaml_lines), encoding="utf-8")

    return TrainingExportResponse(
        total_samples=len(req.samples),
        train_count=train_count,
        val_count=val_count,
        classes_used=class_list,
        data_yaml_path=str(data_yaml.resolve()),
    )


def _resolve_output_dir(output_dir: str) -> Path:
    root = Path(settings.training_export_root).expanduser().resolve()
    requested = Path(output_dir or ".").expanduser()
    candidate = requested if requested.is_absolute() else root / requested
    resolved = candidate.resolve()

    if resolved != root and root not in resolved.parents:
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST,
            detail="output_dir must stay inside the training export root",
        )

    return resolved


def _prepare_output_dir(out: Path, overwrite: bool) -> None:
    generated_paths = [out / "images", out / "labels", out / "data.yaml"]
    existing = [p for p in generated_paths if p.exists()]
    if existing and not overwrite:
        raise HTTPException(
            status_code=status.HTTP_409_CONFLICT,
            detail="training export already exists; set overwrite=true to replace generated files",
        )

    for path in generated_paths:
        if path.is_dir():
            shutil.rmtree(path)
        elif path.exists():
            path.unlink()


def _split_indices(sample_count: int, train_split: float) -> set[int]:
    indices = list(range(sample_count))
    random.Random(SPLIT_SEED).shuffle(indices)
    split_idx = int(len(indices) * train_split)
    return set(indices[:split_idx])


def _decode_training_image(image_base64: str) -> Image.Image:
    return decode_image_safe(
        image_base64,
        max_bytes=settings.training_max_image_bytes,
        max_pixels=settings.max_image_pixels,
    )
