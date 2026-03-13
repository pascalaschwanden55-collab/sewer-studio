"""Training data export endpoint (YOLO format)."""

from __future__ import annotations

import base64
import io
import os
import random
import logging
from pathlib import Path

from PIL import Image
from fastapi import APIRouter

from ..schemas.segmentation import TrainingExportRequest, TrainingExportResponse

router = APIRouter()
logger = logging.getLogger(__name__)


@router.post("/training/export-yolo", response_model=TrainingExportResponse)
async def export_yolo(req: TrainingExportRequest) -> TrainingExportResponse:
    """Export training samples to YOLO format (images + labels + data.yaml)."""
    out = Path(req.output_dir)
    img_train = out / "images" / "train"
    img_val = out / "images" / "val"
    lbl_train = out / "labels" / "train"
    lbl_val = out / "labels" / "val"

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
    indices = list(range(len(req.samples)))
    random.shuffle(indices)
    split_idx = int(len(indices) * req.train_split)
    train_indices = set(indices[:split_idx])

    train_count = 0
    val_count = 0

    for i, sample in enumerate(req.samples):
        is_train = i in train_indices
        img_dir = img_train if is_train else img_val
        lbl_dir = lbl_train if is_train else lbl_val

        # Save image
        raw = base64.b64decode(sample.image_base64)
        img = Image.open(io.BytesIO(raw)).convert("RGB")
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
